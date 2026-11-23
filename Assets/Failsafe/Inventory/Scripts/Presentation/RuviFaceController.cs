using UnityEngine;
using UnityEngine.Video;

namespace Failsafe.Inventory.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RuviFaceController : MonoBehaviour
    {
        private const float PreparationTimeout = 5f;
        private static readonly int FaceTextureProperty = Shader.PropertyToID("_UnlitColorMap");

        [SerializeField] private InventoryRobotPresentationController _presentation;
        [SerializeField] private Renderer _screenRenderer;
        [SerializeField] private RuviEmotionLibrary _library;

        private VideoPlayer _player;
        private RenderTexture _videoTexture;
        private MaterialPropertyBlock _properties;
        private VideoClip _requestedClip;
        private float _deadline;

        private void Awake()
        {
            if (_presentation == null)
                _presentation = GetComponent<InventoryRobotPresentationController>();

            // Keep playback on the presentation root, outside the animated visual hierarchy.
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.isLooping = false;
            _player.waitForFirstFrame = true;
            _player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            _player.prepareCompleted += HandlePrepared;
            _player.frameReady += HandleFirstFrame;
            _player.loopPointReached += HandleFinished;
            _player.errorReceived += HandleError;
        }

        private void OnEnable()
        {
            if (_presentation != null)
                _presentation.StateChanged += HandleStateChanged;
            ShowNeutral();
            if (_presentation != null && _presentation.State == InventoryRobotPresentationState.Opening)
                PlayRandomEmotion();
        }

        private void Update()
        {
            if (_requestedClip != null && Time.realtimeSinceStartup >= _deadline)
            {
                Debug.LogWarning("Ruvi face video did not finish in time; showing the neutral face.", this);
                ShowNeutral();
            }
        }

        private void HandleStateChanged(InventoryRobotPresentationState state)
        {
            if (state == InventoryRobotPresentationState.Opening)
                PlayRandomEmotion();
            else if (state == InventoryRobotPresentationState.Closing || state == InventoryRobotPresentationState.Hidden)
                ShowNeutral();
        }

        public bool PlayRandomEmotion()
        {
            if (_library == null || _library.TotalWeight <= 0)
                return false;
            return _library.TrySelect(Random.Range(0, _library.TotalWeight), out RuviEmotionLibrary.Emotion emotion)
                && Play(emotion.Clip);
        }

        public bool PlayEmotion(int index)
        {
            if (_library == null || index < 0 || index >= _library.Emotions.Count)
                return false;
            RuviEmotionLibrary.Emotion emotion = _library.Emotions[index];
            return emotion != null && Play(emotion.Clip);
        }

        private bool Play(VideoClip clip)
        {
            if (!isActiveAndEnabled || _player == null || clip == null || _screenRenderer == null ||
                _screenRenderer.sharedMaterial == null || !_screenRenderer.sharedMaterial.HasProperty(FaceTextureProperty))
                return false;
            if (_presentation != null && _presentation.State != InventoryRobotPresentationState.Opening &&
                _presentation.State != InventoryRobotPresentationState.Open)
                return false;

            ShowNeutral();
            EnsureVideoTexture((int)clip.width, (int)clip.height);
            _requestedClip = clip;
            _deadline = Time.realtimeSinceStartup + PreparationTimeout;
            _player.clip = clip;
            _player.targetTexture = _videoTexture;
            _player.sendFrameReadyEvents = true;
            _player.Prepare();
            return true;
        }

        private bool IsCurrentRequest(VideoPlayer source) =>
            isActiveAndEnabled && source == _player && _requestedClip != null && source.clip == _requestedClip;

        private void HandlePrepared(VideoPlayer source)
        {
            if (!IsCurrentRequest(source))
                return;
            _deadline = Time.realtimeSinceStartup + (float)_requestedClip.length + PreparationTimeout;
            source.Play();
        }

        private void HandleFirstFrame(VideoPlayer source, long frame)
        {
            if (!IsCurrentRequest(source))
                return;
            // Leave the neutral image visible until a decoded frame replaces the old RT contents.
            SetFaceTexture(_videoTexture);
            source.sendFrameReadyEvents = false;
        }

        private void HandleFinished(VideoPlayer source)
        {
            if (IsCurrentRequest(source))
                ShowNeutral();
        }

        private void HandleError(VideoPlayer source, string message)
        {
            if (!IsCurrentRequest(source))
                return;
            Debug.LogWarning($"Ruvi face video could not be played: {message}", this);
            ShowNeutral();
        }

        public void ShowNeutral()
        {
            // Invalidate callbacks before stopping; closing never waits for video preparation.
            _requestedClip = null;
            if (_player != null)
            {
                _player.sendFrameReadyEvents = false;
                _player.Stop();
            }
            if (_library != null && _library.NeutralFace != null)
                SetFaceTexture(_library.NeutralFace);
        }

        private void SetFaceTexture(Texture texture)
        {
            if (_screenRenderer == null)
                return;
            if (_properties == null)
                _properties = new MaterialPropertyBlock();
            _screenRenderer.GetPropertyBlock(_properties);
            _properties.SetTexture(FaceTextureProperty, texture);
            _screenRenderer.SetPropertyBlock(_properties);
        }

        private void EnsureVideoTexture(int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (_videoTexture != null && _videoTexture.width == width && _videoTexture.height == height)
                return;
            ReleaseVideoTexture();
            _videoTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Ruvi Face Video",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            _videoTexture.Create();
        }

        private void ReleaseVideoTexture()
        {
            if (_player != null)
                _player.targetTexture = null;
            if (_videoTexture == null)
                return;
            _videoTexture.Release();
            DestroyOwnedObject(_videoTexture);
            _videoTexture = null;
        }

        private void OnDisable()
        {
            if (_presentation != null)
                _presentation.StateChanged -= HandleStateChanged;
            ShowNeutral();
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.prepareCompleted -= HandlePrepared;
                _player.frameReady -= HandleFirstFrame;
                _player.loopPointReached -= HandleFinished;
                _player.errorReceived -= HandleError;
                DestroyOwnedObject(_player);
            }
            ReleaseVideoTexture();
        }

        private static void DestroyOwnedObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
