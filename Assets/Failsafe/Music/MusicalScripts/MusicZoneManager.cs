using System.Collections;
using UnityEngine;
using FMODUnity;     // EventReference, RuntimeManager
using FMOD.Studio;  // EventInstance, EventDescription

// Алиасы, чтобы не путаться с одноимёнными enum из FMODUnity
using STOP_MODE = FMOD.Studio.STOP_MODE;
using PLAYBACK_STATE = FMOD.Studio.PLAYBACK_STATE;

[DisallowMultipleComponent]
public class MusicZoneManager : MonoBehaviour
{
    [Header("Default playlist (optional)")]
    [SerializeField] private EventReference[] defaultPlaylist;
    [SerializeField] private bool defaultShuffle = false;
    [SerializeField] private bool defaultLoop = true;
    [SerializeField] private float defaultFadeIn = 0.5f;
    [SerializeField] private float defaultFadeOut = 0.5f;

    [Header("Advanced")]
    [Tooltip("Если true — уважаем внутренние лупы события FMOD и не форсим переключение на wrap.")]
    [SerializeField] private bool respectEventInternalLoop = false;

    [Header("Fallback timing")]
    [Tooltip("Фолбэк: форсировать переключение, если событие не стопается (луп/бесконечное). 0 = выкл.")]
    [SerializeField] private float fallbackMaxTrackSeconds = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private EventInstance currentMusicInstance;
    private MusicZone currentZone;

    private Coroutine fadeCoroutine;
    private Coroutine monitorCoroutine;

    // активный плейлист
    private EventReference[] activePlaylist;
    private bool activeShuffle;
    private bool activeLoop;
    private int activeIndex = 0;

    // таймлайн/таймер
    private int currentEventLengthMs = -1;
    private int lastTimelinePosMs = 0;
    private float playtimeSeconds = 0f;

    private void Awake()
    {
        Log("[Awake] Manager alive");
    }

    private void Start()
    {
        Log("[Start]");
        if (defaultPlaylist != null && defaultPlaylist.Length > 0)
        {
            ActivatePlaylist(defaultPlaylist, defaultShuffle, defaultLoop);
            PlayCurrent(defaultFadeIn);
            StartMonitor();
        }
        else
        {
            Log("[Start] defaultPlaylist пуст — ждём входа в зону.");
        }
    }

    // Вызвать из MusicZone.OnTriggerEnter(Player)
    public void EnterMusicZone(MusicZone zone)
    {
        Log(zone ? $"[EnterMusicZone] {zone.name}" : "[EnterMusicZone] zone=null");
        if (zone == null) return;

        if (currentZone == zone)
        {
            Log("[EnterMusicZone] Уже в этой зоне — пропуск");
            return;
        }

        StopRunningCoroutines();
        StartCoroutine(SwitchMusicCoroutine(zone));
    }

    // Вызвать из MusicZone.OnTriggerExit(Player)
    public void ExitMusicZone(MusicZone zone)
    {
        Log(zone ? $"[ExitMusicZone] {zone.name}" : "[ExitMusicZone] zone=null");
        if (zone == null || currentZone != zone) return;

        StopRunningCoroutines();

        if (zone.stopOnExit)
        {
            Log($"[ExitMusicZone] stopOnExit=true — fadeOut={zone.fadeOutDuration}");
            fadeCoroutine = StartCoroutine(FadeOutAndStop(zone.fadeOutDuration));
            currentZone = null;
            activePlaylist = null;
        }
        else
        {
            Log($"[ExitMusicZone] stopOnExit=false — вернёмся к дефолту, если он задан");
            currentZone = null;
            if (defaultPlaylist != null && defaultPlaylist.Length > 0)
            {
                ActivatePlaylist(defaultPlaylist, defaultShuffle, defaultLoop);
                StartMonitor();
            }
        }
    }

    private IEnumerator SwitchMusicCoroutine(MusicZone zone)
    {
        Log($"[Switch] -> Зона '{zone.name}'. fadeOutPrev={zone.fadeOutDuration}, fadeInNext={zone.fadeInDuration}");

        if (currentMusicInstance.isValid())
            yield return StartCoroutine(FadeOutAndStop(zone.fadeOutDuration));

        currentZone = zone;

        if (!string.IsNullOrEmpty(zone.musicParameter))
        {
            SetMusicParameter(zone.musicParameter, zone.parameterValue);
            Log($"[Switch] Параметр '{zone.musicParameter}'={zone.parameterValue}");
        }

        ActivatePlaylist(zone.zonePlaylist, zone.shuffle, zone.loop);
        PlayCurrent(zone.fadeInDuration);
        StartMonitor();
    }

    // ---------- Плейлист ----------

    private void ActivatePlaylist(EventReference[] playlist, bool shuffle, bool loop)
    {
        activePlaylist = (playlist != null && playlist.Length > 0) ? playlist : null;
        activeShuffle  = shuffle;
        activeLoop     = loop;
        activeIndex    = 0;

        if (activePlaylist == null)
        {
            Log("[ActivatePlaylist] Пустой список.");
            return;
        }

        if (activeShuffle) activeIndex = Random.Range(0, activePlaylist.Length);

        Log($"[ActivatePlaylist] count={activePlaylist.Length}, shuffle={activeShuffle}, loop={activeLoop}, startIndex={activeIndex}");
    }

    private void PlayCurrent(float fadeIn)
    {
        if (activePlaylist == null || activePlaylist.Length == 0)
        {
            Log("[PlayCurrent] activePlaylist пуст");
            return;
        }

        var evt = activePlaylist[Mathf.Clamp(activeIndex, 0, activePlaylist.Length - 1)];
        PlayMusic(evt, fadeIn);
    }

    private void PlayNext(float fadeIn)
    {
        if (activePlaylist == null || activePlaylist.Length == 0) return;

        if (activeShuffle)
        {
            if (activePlaylist.Length > 1)
            {
                int next;
                do { next = Random.Range(0, activePlaylist.Length); }
                while (next == activeIndex);
                activeIndex = next;
            }
        }
        else
        {
            activeIndex++;
            if (activeIndex >= activePlaylist.Length)
            {
                if (activeLoop) activeIndex = 0;
                else
                {
                    Log("[PlayNext] Конец плейлиста, loop=false — стоп.");
                    return;
                }
            }
        }

        Log($"[PlayNext] -> index={activeIndex}");
        PlayCurrent(fadeIn);
    }

    // ---------- FMOD ----------

    private void PlayMusic(EventReference eventRef, float fadeInDuration)
    {
        if (eventRef.IsNull)
        {
            Log("[PlayMusic] EventReference.IsNull");
            return;
        }

        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.stop(STOP_MODE.IMMEDIATE);
            currentMusicInstance.release();
            currentMusicInstance = default;
        }

        currentMusicInstance = RuntimeManager.CreateInstance(eventRef);

        // таймлайн
        currentEventLengthMs = GetEventLengthMs(currentMusicInstance);
        lastTimelinePosMs = 0;
        playtimeSeconds = 0f;

        Log($"[PlayMusic] '{eventRef.Path}' lenMs={currentEventLengthMs}");

        if (fadeInDuration > 0f)
            StartCoroutine(FadeIn(currentMusicInstance, fadeInDuration));
        else
            currentMusicInstance.setVolume(1f);

        currentMusicInstance.start();
    }

    private int GetEventLengthMs(EventInstance instance)
    {
        if (!instance.isValid()) return -1;
        var r1 = instance.getDescription(out EventDescription desc);
        if (r1 != FMOD.RESULT.OK || !desc.isValid()) return -1;

        var r2 = desc.getLength(out int lenMs); // 0 — бесконечные/луповые
        if (r2 != FMOD.RESULT.OK) return -1;
        return lenMs;
    }

    private IEnumerator FadeOutAndStop(float fadeDuration)
    {
        if (!currentMusicInstance.isValid())
            yield break;

        if (fadeDuration <= 0f)
        {
            currentMusicInstance.stop(STOP_MODE.IMMEDIATE);
            currentMusicInstance.release();
            currentMusicInstance = default;
            yield break;
        }

        currentMusicInstance.getVolume(out float startVol, out _);

        float t = 0f;
        while (t < fadeDuration && currentMusicInstance.isValid())
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(startVol, 0f, t / fadeDuration);
            currentMusicInstance.setVolume(v);
            yield return null;
        }

        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.stop(STOP_MODE.ALLOWFADEOUT);
            currentMusicInstance.release();
            currentMusicInstance = default;
        }
    }

    private IEnumerator FadeIn(EventInstance instance, float fadeDuration)
    {
        if (!instance.isValid()) yield break;

        instance.setVolume(0f);

        float t = 0f;
        while (t < fadeDuration && instance.isValid())
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(0f, 1f, t / fadeDuration);
            instance.setVolume(v);
            yield return null;
        }

        if (instance.isValid())
            instance.setVolume(1f);
    }

    public void SetMusicParameter(string parameterName, float value, bool ignoreSeekSpeed = false)
    {
        if (currentMusicInstance.isValid())
        {
            var res = currentMusicInstance.setParameterByName(parameterName, value, ignoreSeekSpeed);
            Log($"[SetMusicParameter] '{parameterName}'={value}, res={res}");
        }
    }

    // ---------- Мониторинг таймлайна/таймера ----------

    private void StartMonitor()
    {
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
        monitorCoroutine = StartCoroutine(MonitorPlaybackLoop());
    }

    private IEnumerator MonitorPlaybackLoop()
    {
        var wait = new WaitForSeconds(0.1f);

        while (true)
        {
            if (currentMusicInstance.isValid())
            {
                currentMusicInstance.getPlaybackState(out PLAYBACK_STATE s);

                if (s == PLAYBACK_STATE.PLAYING)
                {
                    playtimeSeconds += 0.1f;

                    int pos;
                    currentMusicInstance.getTimelinePosition(out pos);

                    if (currentEventLengthMs > 0)
                    {
                        const int epsilonMs = 80;
                        if (pos >= currentEventLengthMs - epsilonMs)
                        {
                            Log($"[Monitor] конец по length: pos={pos} len={currentEventLengthMs}");
                            StopAndAdvance();
                            yield return wait;
                            continue;
                        }
                    }

                    if (!respectEventInternalLoop && pos < lastTimelinePosMs)
                    {
                        Log($"[Monitor] wrap detected: {lastTimelinePosMs}->{pos}");
                        StopAndAdvance();
                        yield return wait;
                        continue;
                    }

                    if (fallbackMaxTrackSeconds > 0f && playtimeSeconds >= fallbackMaxTrackSeconds)
                    {
                        Log($"[Monitor] fallbackMaxTrackSeconds reached: {playtimeSeconds:F1}s");
                        StopAndAdvance();
                        yield return wait;
                        continue;
                    }

                    lastTimelinePosMs = pos;
                }
                else if (s == PLAYBACK_STATE.STOPPED || s == PLAYBACK_STATE.STOPPING)
                {
                    Log($"[Monitor] state={s} — переключаемся");
                    TryAdvanceFromMonitor();
                }
            }

            yield return wait;
        }
    }

    private void StopAndAdvance()
    {
        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.stop(STOP_MODE.ALLOWFADEOUT);
            currentMusicInstance.release();
            currentMusicInstance = default;
        }
        TryAdvanceFromMonitor();
    }

    private void TryAdvanceFromMonitor()
    {
        float fadeIn = currentZone ? currentZone.fadeInDuration : defaultFadeIn;

        if (activePlaylist == null || activePlaylist.Length == 0)
            return;

        if (!activeLoop && !activeShuffle && activeIndex >= activePlaylist.Length - 1)
        {
            Log("[TryAdvance] Конец плейлиста, loop=false — дальше не идём.");
            return;
        }

        PlayNext(fadeIn);
    }

    private void OnDisable()  => Cleanup();
    private void OnDestroy()  => Cleanup();

    private void StopRunningCoroutines()
    {
        if (fadeCoroutine != null)    StopCoroutine(fadeCoroutine);
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
    }

    private void Cleanup()
    {
        StopRunningCoroutines();

        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.stop(STOP_MODE.IMMEDIATE);
            currentMusicInstance.release();
            currentMusicInstance = default;
        }

        Log("[Cleanup] Done");
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log($"[MusicZoneManager] {msg}");
    }
}