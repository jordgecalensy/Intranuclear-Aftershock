using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AdvancedMusicPlaylist : MonoBehaviour
{
    [Header("Треки плейлиста")]
    public AudioClip[] musicTracks;
    
    [Header("Настройки аудио")]
    [SerializeField] private AudioSource primaryAudioSource;
    [SerializeField] private AudioSource secondaryAudioSource;
    
    [Header("Настройки перехода")]
    public float crossfadeDuration = 2f;
    public bool shufflePlaylist = false;
    public bool loopPlaylist = true;
    public float volume = 1f;
    
    private int currentTrackIndex = 0;
    private bool isCrossfading = false;
    private int[] shuffledIndices;
    private Coroutine playlistCoroutine;

    void Awake()
    {
        InitializeAudioSources();
    }

    void Start()
    {
        if (shufflePlaylist)
            ShufflePlaylist();
        
        StartPlaylist();
    }

    void InitializeAudioSources()
    {
        // Создаем или находим первичный AudioSource
        if (primaryAudioSource == null)
            primaryAudioSource = GetComponent<AudioSource>();
        
        if (primaryAudioSource == null)
            primaryAudioSource = gameObject.AddComponent<AudioSource>();
        
        // Создаем вторичный AudioSource для кроссфейда
        if (secondaryAudioSource == null)
        {
            secondaryAudioSource = gameObject.AddComponent<AudioSource>();
        }

        // Настраиваем оба AudioSource
        ConfigureAudioSource(primaryAudioSource);
        ConfigureAudioSource(secondaryAudioSource);
        
        // Устанавливаем громкость
        primaryAudioSource.volume = volume;
        secondaryAudioSource.volume = 0f; // Начинаем с нулевой громкости
    }

    void ConfigureAudioSource(AudioSource audioSource)
    {
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D звук
        audioSource.volume = volume;
    }

    void ShufflePlaylist()
    {
        if (musicTracks == null || musicTracks.Length == 0) return;

        shuffledIndices = new int[musicTracks.Length];
        for (int i = 0; i < musicTracks.Length; i++)
        {
            shuffledIndices[i] = i;
        }
        
        // Алгоритм Фишера-Йейтса для перемешивания
        for (int i = shuffledIndices.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = shuffledIndices[i];
            shuffledIndices[i] = shuffledIndices[j];
            shuffledIndices[j] = temp;
        }
    }

    public void StartPlaylist()
    {
        if (musicTracks == null || musicTracks.Length == 0)
        {
            Debug.LogWarning("Music playlist is empty!");
            return;
        }

        if (playlistCoroutine != null)
            StopCoroutine(playlistCoroutine);
            
        playlistCoroutine = StartCoroutine(PlaylistRoutine());
    }

    IEnumerator PlaylistRoutine()
    {
        int trackIndex = 0;
        
        while (true)
        {
            // Получаем индекс трека в зависимости от режима перемешивания
            int actualIndex = shufflePlaylist ? shuffledIndices[trackIndex] : trackIndex;
            
            // Воспроизводим текущий трек
            yield return StartCoroutine(PlayTrack(actualIndex));
            
            // Переходим к следующему треку
            trackIndex++;
            
            // Проверяем достижение конца плейлиста
            if (trackIndex >= musicTracks.Length)
            {
                if (loopPlaylist)
                {
                    if (shufflePlaylist)
                        ShufflePlaylist();
                    trackIndex = 0;
                }
                else
                {
                    // Завершаем плейлист
                    yield break;
                }
            }
        }
    }

    IEnumerator PlayTrack(int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= musicTracks.Length || musicTracks[trackIndex] == null)
            yield break;

        AudioClip clip = musicTracks[trackIndex];
        currentTrackIndex = trackIndex;

        // Если это первый трек, просто воспроизводим его
        if (!primaryAudioSource.isPlaying && !secondaryAudioSource.isPlaying)
        {
            primaryAudioSource.clip = clip;
            primaryAudioSource.Play();
            
            // Ждем окончания трека (минус время кроссфейда для плавного перехода)
            float waitTime = clip.length - crossfadeDuration;
            if (waitTime > 0)
                yield return new WaitForSeconds(waitTime);
        }
        else
        {
            // Запускаем кроссфейд с новым треком
            yield return StartCoroutine(CrossfadeToNextTrack(clip));
        }
    }

    IEnumerator CrossfadeToNextTrack(AudioClip nextClip)
    {
        isCrossfading = true;
        
        // Определяем активный и неактивный источники
        AudioSource fadingOutSource = primaryAudioSource.isPlaying ? primaryAudioSource : secondaryAudioSource;
        AudioSource fadingInSource = primaryAudioSource.isPlaying ? secondaryAudioSource : primaryAudioSource;
        
        // Настраиваем источник для нового трека
        fadingInSource.clip = nextClip;
        fadingInSource.volume = 0f;
        fadingInSource.Play();
        
        float timer = 0f;
        float startVolumeOut = fadingOutSource.volume;
        
        while (timer < crossfadeDuration)
        {
            timer += Time.deltaTime;
            float ratio = timer / crossfadeDuration;
            
            // Плавно уменьшаем громкость уходящего трека
            fadingOutSource.volume = Mathf.Lerp(startVolumeOut, 0f, ratio);
            // Плавно увеличиваем громкость входящего трека
            fadingInSource.volume = Mathf.Lerp(0f, volume, ratio);
            
            yield return null;
        }
        
        // Завершаем уходящий трек
        fadingOutSource.Stop();
        fadingOutSource.volume = 0f;
        
        isCrossfading = false;
        
        // Ждем оставшееся время трека до начала следующего кроссфейда
        float remainingTime = fadingInSource.clip.length - crossfadeDuration;
        if (remainingTime > 0)
            yield return new WaitForSeconds(remainingTime);
    }

    public void PlayNextTrack()
    {
        if (playlistCoroutine != null)
            StopCoroutine(playlistCoroutine);
        
        int nextIndex = currentTrackIndex + 1;
        if (nextIndex >= musicTracks.Length)
        {
            if (loopPlaylist)
            {
                nextIndex = 0;
                if (shufflePlaylist)
                    ShufflePlaylist();
            }
            else
            {
                return;
            }
        }
        
        playlistCoroutine = StartCoroutine(PlayTrack(nextIndex));
    }

    public void PlayPreviousTrack()
    {
        if (playlistCoroutine != null)
            StopCoroutine(playlistCoroutine);
        
        int prevIndex = currentTrackIndex - 1;
        if (prevIndex < 0)
            prevIndex = musicTracks.Length - 1;
        
        playlistCoroutine = StartCoroutine(PlayTrack(prevIndex));
    }

    public void StopPlaylist()
    {
        if (playlistCoroutine != null)
        {
            StopCoroutine(playlistCoroutine);
            playlistCoroutine = null;
        }
        
        primaryAudioSource.Stop();
        secondaryAudioSource.Stop();
    }

    public void PausePlaylist()
    {
        primaryAudioSource.Pause();
        secondaryAudioSource.Pause();
    }

    public void ResumePlaylist()
    {
        primaryAudioSource.UnPause();
        secondaryAudioSource.UnPause();
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        primaryAudioSource.volume = volume;
        secondaryAudioSource.volume = volume;
    }

    // Метод для принудительного переключения на конкретный трек
    public void PlayTrackImmediate(int trackIndex)
    {
        if (trackIndex >= 0 && trackIndex < musicTracks.Length)
        {
            if (playlistCoroutine != null)
                StopCoroutine(playlistCoroutine);
            
            playlistCoroutine = StartCoroutine(PlayTrack(trackIndex));
        }
    }

    // Автоматическое создание плейлиста в редакторе
    #if UNITY_EDITOR
    [ContextMenu("Setup Audio Sources")]
    void SetupAudioSourcesInEditor()
    {
        InitializeAudioSources();
        UnityEditor.EditorUtility.SetDirty(this);
    }
    #endif
}