using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

public class MusicZoneManager : MonoBehaviour
{
    [Header("FMOD Settings")]
    [SerializeField] private List<EventReference> defaultMusicEvent = new List<EventReference>();
    
    private FMOD.Studio.EventInstance currentMusicInstance;
    private MusicZone currentMusicZone;
    private Coroutine fadeCoroutine;

    void Start()
    {
        // Запускаем музыку по умолчанию, если она задана
        if (!string.IsNullOrEmpty(defaultMusicEvent))
        {
            PlayMusic(defaultMusicEvent, 0f);
        }
    }

    public void EnterMusicZone(MusicZone zone)
    {
        if (currentMusicZone == zone) return;

        // Останавливаем предыдущую музыку с фейдом
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(SwitchMusicCoroutine(zone));
    }

    public void ExitMusicZone(MusicZone zone)
    {
        if (currentMusicZone != zone) return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOutMusicCoroutine(zone.fadeOutDuration));
        currentMusicZone = null;
    }

    private IEnumerator SwitchMusicCoroutine(MusicZone zone)
    {
        // Фейд-аут текущей музыки
        if (currentMusicZone != null)
        {
            yield return StartCoroutine(FadeOutMusicCoroutine(currentMusicZone.fadeOutDuration));
        }

        currentMusicZone = zone;

        // Запускаем музыку новой зоны
        if (zone.HasValidPlaylist())
        {
            string musicEvent = zone.shuffle ? zone.GetRandomTrack() : zone.GetTrackByIndex(0);
            PlayMusic(musicEvent, zone.fadeInDuration);
        }
    }

    private IEnumerator FadeOutMusicCoroutine(float fadeDuration)
    {
        if (!currentMusicInstance.isValid()) yield break;

        float currentVolume = 1f;
        float timer = 0f;

        // Получаем текущую громкость
        currentMusicInstance.getVolume(out currentVolume);

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float newVolume = Mathf.Lerp(currentVolume, 0f, timer / fadeDuration);
            currentMusicInstance.setVolume(newVolume);
            yield return null;
        }

        currentMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        currentMusicInstance.release();
    }

    private void PlayMusic(string eventName, float fadeInDuration)
    {
        if (string.IsNullOrEmpty(eventName)) return;

        // Останавливаем предыдущую музыку
        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            currentMusicInstance.release();
        }

        // Создаем новое событие
        currentMusicInstance = RuntimeManager.CreateInstance(eventName);
        
        // Запускаем фейд-ин
        if (fadeInDuration > 0f)
        {
            StartCoroutine(FadeInMusicCoroutine(currentMusicInstance, fadeInDuration));
        }
        else
        {
            currentMusicInstance.setVolume(1f);
        }

        currentMusicInstance.start();
    }

    private IEnumerator FadeInMusicCoroutine(FMOD.Studio.EventInstance instance, float fadeDuration)
    {
        instance.setVolume(0f);

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float volume = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            instance.setVolume(volume);
            yield return null;
        }

        instance.setVolume(1f);
    }

    // Установка параметра FMOD
    public void SetMusicParameter(string parameterName, float value)
    {
        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.setParameterByName(parameterName, value);
        }
    }

    void OnDestroy()
    {
        // Очистка FMOD инстансов
        if (currentMusicInstance.isValid())
        {
            currentMusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            currentMusicInstance.release();
        }
    }
}