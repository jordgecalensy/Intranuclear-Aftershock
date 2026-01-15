using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FMODUnity;

namespace Failsafe.PhrasesScript
{
    public class PhrasesManager : MonoBehaviour
    {
        [SerializeField] private PhrasesData _phrasesData;
        [SerializeField] private bool _allowInterrupt = false;
        [SerializeField] private bool _oneUsing = false;
        [SerializeField] private bool _nextPhrase = false;

        private Dictionary<int, int> _phrasesUsedCount = new Dictionary<int, int>();
        private Phrase _phrase;
        private bool _used = false;
        private FMOD.Studio.EventInstance _currentEventInstance;
        private bool _playing = false;

        private void Awake()
        {
            LoadPhrase();
        }

        private void LoadPhrase()
        {
            if (_phrasesData == null || _phrasesData.Phrases.Count == 0)
            {
                Debug.LogError("PhrasesData is null or contains no phrases!");
                return;
            }

            var sortedPhrases = _phrasesData.Phrases
                .OrderByDescending(p => p.Once)
                // Вес - количество использований * 5
                .ThenByDescending(p => p.Weight - _phrasesUsedCount.GetValueOrDefault(_phrasesData.Phrases.IndexOf(p), 0) * 5)
                // С единичным использованием, только те, которые не использовались
                .Where(p => !(p.Once && _phrasesUsedCount.ContainsKey(_phrasesData.Phrases.IndexOf(p))))
                .ToList();

            foreach (var phrase in _phrasesUsedCount)
            {
                Debug.Log($"Phrase index: {phrase.Key}, Used count: {phrase.Value}");
            }

            _phrase = sortedPhrases[0];
            _currentEventInstance = RuntimeManager.CreateInstance(_phrase.FMODEvent);
            Debug.Log($"Loaded phrase: {_phrase.Text}");
        }

        public void PlayPhrase()
        {
            if (_playing && !IsEventPlaying() && _nextPhrase)
            {
                LoadPhrase();
                Debug.Log("Loading next phrase.");
            }

            if (_oneUsing && _used)
            {
                Debug.Log("Phrase already used. Skipping.");
                return;
            }

            if (IsEventPlaying() && !_allowInterrupt)
            {
                Debug.Log("AudioSource is already playing. Skipping.");
                return;
            }

            if (_phrase == null)
            {
                Debug.LogWarning("No phrase assigned!");
                return;
            }

            if (_phrase.FMODEvent.IsNull)
            {
                Debug.LogError($"Phrase '{_phrase.Text}' has no FMOD Event assigned!");
                return;
            }

            if (IsEventPlaying() && _allowInterrupt)
            {
                StopPlayingPhrase();
            }
            _currentEventInstance.start();
            _phrasesUsedCount[_phrasesData.Phrases.IndexOf(_phrase)] = _phrasesUsedCount.GetValueOrDefault(_phrasesData.Phrases.IndexOf(_phrase), 0) + 1;
            _playing = true;

            if (_oneUsing)
            {
                _used = true;
            }

            Debug.Log($"Playing phrase: {_phrase.Text}");
        }

        public Phrase GetPhrase()
        {
            return _phrase;
        }

        private bool IsEventPlaying()
        {
            if (!_currentEventInstance.isValid())
                return false;

            _currentEventInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state);
            return state == FMOD.Studio.PLAYBACK_STATE.PLAYING;
        }

        public void StopPlayingPhrase()
        {
            if (_currentEventInstance.isValid())
            {
                _currentEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _currentEventInstance.release();
            }
        }

        public void ResetUsed()
        {
            _used = false;
            Debug.Log("Phrase usage reset.");
        }

        public bool IsUsed()
        {
            return _used;
        }
    }
}