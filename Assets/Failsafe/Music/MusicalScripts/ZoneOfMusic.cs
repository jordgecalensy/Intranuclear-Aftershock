using System.Collections;
using UnityEngine;

public class MusicZone : MonoBehaviour
{
    [Header("Настройки музыкальной зоны")]
    [FMODUnity.EventRef]
    public string[] zonePlaylist; // Массив событий FMOD
    public float fadeInDuration = 2f;
    public float fadeOutDuration = 2f;
    public bool stopOnExit = false;
    
    [Header("Настройки воспроизведения")]
    public bool shuffle = false;
    public bool loop = true;
    
    [Header("Параметры FMOD")]
    public string musicParameter = "MusicState";
    public float parameterValue = 1f;
    
    private MusicZoneManager zoneManager;
    private bool playerInZone = false;

    void Start()
    {
        zoneManager = FindObjectOfType<MusicZoneManager>();
        if (zoneManager == null)
        {
            Debug.LogError("MusicZoneManager не найден в сцене!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playerInZone)
        {
            playerInZone = true;
            zoneManager.EnterMusicZone(this);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && playerInZone && stopOnExit)
        {
            playerInZone = false;
            zoneManager.ExitMusicZone(this);
        }
    }

    public string[] GetPlaylist()
    {
        return zonePlaylist;
    }

    // Метод для получения случайного трека из плейлиста
    public string GetRandomTrack()
    {
        if (zonePlaylist == null || zonePlaylist.Length == 0)
            return null;

        return zonePlaylist[Random.Range(0, zonePlaylist.Length)];
    }

    // Метод для получения трека по индексу
    public string GetTrackByIndex(int index)
    {
        if (zonePlaylist == null || zonePlaylist.Length == 0 || index < 0 || index >= zonePlaylist.Length)
            return null;

        return zonePlaylist[index];
    }

    // Проверка валидности плейлиста
    public bool HasValidPlaylist()
    {
        return zonePlaylist != null && zonePlaylist.Length > 0;
    }
}
