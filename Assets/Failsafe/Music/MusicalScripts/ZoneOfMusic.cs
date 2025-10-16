using UnityEngine;
using FMODUnity;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class MusicZone : MonoBehaviour
{
    [Header("Плейлист зоны (FMOD EventReference)")]
    public EventReference[] zonePlaylist;

    public float fadeInDuration = 2f;
    public float fadeOutDuration = 2f;
    public bool stopOnExit = false;

    [Header("Поведение плейлиста")]
    public bool shuffle = false;
    public bool loop = true;

    [Header("FMOD параметры (опционально)")]
    public string musicParameter = "MusicState";
    public float parameterValue = 1f;

    private MusicZoneManager zoneManager;
    private bool playerInZone = false;

    private void Reset()
    {
        // Для 3D-триггеров один из коллайдеров в паре должен быть с Rigidbody.
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Start()
    {
        zoneManager = FindObjectOfType<MusicZoneManager>();
        if (zoneManager == null)
        {
            Debug.LogError("[MusicZone] MusicZoneManager не найден на сцене!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerInZone) return;
        if (!other.CompareTag("Player")) return;

        playerInZone = true;
        zoneManager?.EnterMusicZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerInZone) return;
        if (!other.CompareTag("Player")) return;

        playerInZone = false;
        zoneManager?.ExitMusicZone(this);
    }

    // Для удобства, если нужно проверить валидность в редакторских утилитах
    public bool HasValidPlaylist() => zonePlaylist != null && zonePlaylist.Length > 0;
}