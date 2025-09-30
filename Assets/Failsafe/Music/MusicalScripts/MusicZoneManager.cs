using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicZoneManager : MonoBehaviour
{
    [Header("Настройки менеджера")]
    public float globalVolume = 1f;
    public AudioClip[] defaultPlaylist;
    
    private AdvancedMusicPlaylist musicPlayer;
    private MusicZone currentZone;
    private Coroutine fadeCoroutine;
    
    // Приоритеты зон (чем выше, тем приоритетнее)
    private Dictionary<MusicZone, int> zonePriorities = new Dictionary<MusicZone, int>();
    private List<MusicZone> activeZones = new List<MusicZone>();

    void Awake()
    {
        musicPlayer = GetComponent<AdvancedMusicPlaylist>();
        if (musicPlayer == null)
        {
            musicPlayer = gameObject.AddComponent<AdvancedMusicPlaylist>();
        }
        
        // Запускаем музыку по умолчанию
        if (defaultPlaylist != null && defaultPlaylist.Length > 0)
        {
            musicPlayer.musicTracks = defaultPlaylist;
            musicPlayer.SetVolume(globalVolume);
        }
    }

    public void EnterMusicZone(MusicZone zone)
    {
        // Добавляем зону в список активных
        if (!activeZones.Contains(zone))
        {
            activeZones.Add(zone);
        }
        
        // Определяем зону с наивысшим приоритетом
        MusicZone highestPriorityZone = GetHighestPriorityZone();
        
        // Если это новая зона с более высоким приоритетом
        if (highestPriorityZone != currentZone)
        {
            currentZone = highestPriorityZone;
            StartZoneMusic(currentZone);
        }
    }

    public void ExitMusicZone(MusicZone zone)
    {
        // Убираем зону из активных
        if (activeZones.Contains(zone))
        {
            activeZones.Remove(zone);
        }
        
        // Если вышли из текущей активной зоны
        if (currentZone == zone)
        {
            MusicZone nextZone = GetHighestPriorityZone();
            
            if (nextZone != null)
            {
                currentZone = nextZone;
                StartZoneMusic(currentZone);
            }
            else
            {
                // Возвращаемся к музыке по умолчанию
                currentZone = null;
                StartDefaultMusic();
            }
        }
    }

    MusicZone GetHighestPriorityZone()
    {
        if (activeZones.Count == 0) return null;
        
        // Здесь можно добавить логику приоритетов
        // Пока просто берем первую зону (можно расширить систему приоритетов)
        return activeZones[0];
    }

    void StartZoneMusic(MusicZone zone)
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
            
        fadeCoroutine = StartCoroutine(SwitchToZoneMusic(zone));
    }

    IEnumerator SwitchToZoneMusic(MusicZone zone)
    {
        // Плавно выключаем текущую музыку
        yield return StartCoroutine(FadeOutMusic());
        
        // Устанавливаем новый плейлист
        musicPlayer.StopPlaylist();
        musicPlayer.musicTracks = zone.GetPlaylist();
        musicPlayer.shufflePlaylist = zone.shuffle;
        musicPlayer.loopPlaylist = zone.loop;
        
        // Плавно включаем новую музыку
        yield return StartCoroutine(FadeInMusic());
        
        // Запускаем плейлист
        musicPlayer.StartPlaylist();
    }

    void StartDefaultMusic()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
            
        fadeCoroutine = StartCoroutine(SwitchToDefaultMusic());
    }

    IEnumerator SwitchToDefaultMusic()
    {
        yield return StartCoroutine(FadeOutMusic());
        
        musicPlayer.StopPlaylist();
        musicPlayer.musicTracks = defaultPlaylist;
        
        yield return StartCoroutine(FadeInMusic());
        
        musicPlayer.StartPlaylist();
    }

    IEnumerator FadeOutMusic()
    {
        float currentVolume = globalVolume;
        float timer = 0f;
        
        while (timer < 1f)
        {
            timer += Time.deltaTime / 1f; // 1 секунда на фейд-аут
            musicPlayer.SetVolume(Mathf.Lerp(currentVolume, 0f, timer));
            yield return null;
        }
    }

    IEnumerator FadeInMusic()
    {
        float timer = 0f;
        
        while (timer < 1f)
        {
            timer += Time.deltaTime / 1f; // 1 секунда на фейд-ин
            musicPlayer.SetVolume(Mathf.Lerp(0f, globalVolume, timer));
            yield return null;
        }
    }

    public void SetZonePriority(MusicZone zone, int priority)
    {
        zonePriorities[zone] = priority;
    }
}