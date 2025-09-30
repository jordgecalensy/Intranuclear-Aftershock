using System.Collections;
using UnityEngine;

public class MusicZone : MonoBehaviour
{
    [Header("Настройки музыкальной зоны")]
    public AudioClip[] zonePlaylist;
    public float fadeInDuration = 2f;
    public float fadeOutDuration = 2f;
    public bool stopOnExit = false;
    
    [Header("Настройки воспроизведения")]
    public bool shuffle = false;
    public bool loop = true;
    
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

    public AudioClip[] GetPlaylist()
    {
        return zonePlaylist;
    }
}
