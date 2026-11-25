using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class BorzoiFlashEffect : MonoBehaviour
{
    private const float DuckedVolume = 0.18f;
    private const float RingFrequency = 2500f;
    private const float RingVolume = 0.035f;

    private Camera _camera;
    private Canvas _canvas;
    private Image _overlay;
    private bool _playing;
    private float _startTime;
    private float _endTime;
    private float _listenerVolume;
    private Bus _masterBus;
    private float _masterVolume;
    private bool _duckingFmod;
    private DSP _ringDsp;
    private Channel _ring;

    private void Awake() => _camera = GetComponent<Camera>();

    public void Play(float duration)
    {
        if (_overlay == null)
            CreateOverlay();

        _startTime = Time.unscaledTime;
        _endTime = Mathf.Max(_endTime, _startTime + Mathf.Max(0.1f, duration));
        _overlay.color = Color.white;
        _canvas.enabled = true;
        if (_playing)
            return;

        _playing = true;
        StartAudio();
    }

    private void Update()
    {
        if (!_playing)
            return;

        float now = Time.unscaledTime;
        if (now >= _endTime || !_camera.isActiveAndEnabled || _overlay == null)
        {
            Stop();
            return;
        }

        float progress = Mathf.InverseLerp(_startTime, _endTime, now);
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1f, progress));
        _overlay.color = new Color(1f, 1f, 1f, 1f - fade);

        if (_ring.hasHandle())
        {
            float attack = Mathf.Clamp01((now - _startTime) / 0.04f);
            _ring.setVolume(_masterVolume * RingVolume * attack * (1f - fade));
        }
    }

    private void CreateOverlay()
    {
        var root = new GameObject("Flash Grenade Overlay", typeof(RectTransform), typeof(Canvas));
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = -1;
        _canvas.targetDisplay = _camera.targetDisplay;

        var image = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(root.transform, false);
        _overlay = image.GetComponent<Image>();
        _overlay.raycastTarget = false;

        var rect = _overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void StartAudio()
    {
        _listenerVolume = AudioListener.volume;
        AudioListener.volume = _listenerVolume * DuckedVolume;

        if (!RuntimeManager.IsInitialized ||
            RuntimeManager.StudioSystem.getBus("bus:/", out _masterBus) != RESULT.OK ||
            _masterBus.getVolume(out _masterVolume) != RESULT.OK)
            return;

        _duckingFmod = _masterBus.setVolume(_masterVolume * DuckedVolume) == RESULT.OK;
        _masterBus.getMute(out bool muted);
        if (muted || _masterVolume <= 0f)
            return;

        // This tone plays through FMOD Core, outside the attenuated Studio bus.
        var core = RuntimeManager.CoreSystem;
        if (core.createDSPByType(DSP_TYPE.OSCILLATOR, out _ringDsp) != RESULT.OK)
            return;

        _ringDsp.setParameterFloat((int)DSP_OSCILLATOR.RATE, RingFrequency);
        if (core.playDSP(_ringDsp, default, true, out _ring) != RESULT.OK)
            return;

        _ring.setVolume(0f);
        _ring.setPaused(false);
    }

    private void Stop()
    {
        if (!_playing)
            return;

        _playing = false;
        _endTime = 0f;
        if (_canvas != null)
            _canvas.enabled = false;

        if (Mathf.Approximately(AudioListener.volume, _listenerVolume * DuckedVolume))
            AudioListener.volume = _listenerVolume;

        if (RuntimeManager.IsInitialized)
        {
            if (_ring.hasHandle())
                _ring.stop();
            if (_ringDsp.hasHandle())
                _ringDsp.release();
            if (_duckingFmod && _masterBus.getVolume(out float volume) == RESULT.OK &&
                Mathf.Approximately(volume, _masterVolume * DuckedVolume))
                _masterBus.setVolume(_masterVolume);
        }

        _duckingFmod = false;
        _ring = default;
        _ringDsp = default;
    }

    private void OnDisable() => Stop();

    private void OnDestroy()
    {
        if (_canvas != null)
            Destroy(_canvas.gameObject);
    }
}
