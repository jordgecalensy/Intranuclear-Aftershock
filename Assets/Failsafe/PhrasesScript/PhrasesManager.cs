using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Failsafe.PhrasesScript
{
    public class PhrasesManager : MonoBehaviour
    {
        [SerializeField] private PhrasesData _phrasesData;
        [SerializeField] private int _phraseIndex = 0;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private bool _allowInterrupt = false;
        [SerializeField] private bool _oneUsing = false;
        [SerializeField] private bool _nextPhrase = false;

        private Phrase _phrase;
        private bool _used = false;

        private void Awake()
        {
            ValidateComponents();
            LoadPhrase();
        }

        private void ValidateComponents()
        {
            if (_audioSource == null)
            {
                Debug.LogWarning("AudioSource not assigned. Adding one automatically.");
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
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
                .ThenByDescending(p => p.Weight)
                .ToList();

            if (_phraseIndex < 0 || _phraseIndex >= sortedPhrases.Count)
            {
                Debug.LogError($"Phrase index {_phraseIndex} is out of bounds!");
                return;
            }

            _phrase = sortedPhrases[_phraseIndex];
            Debug.Log($"Loaded phrase: {_phrase.Text}");
        }

        public void PlayPhrase()
        {
            if (_oneUsing && _used)
            {
                Debug.Log("Phrase already used. Skipping.");
                return;
            }

            if (_audioSource.isPlaying && !_allowInterrupt)
            {
                Debug.Log("AudioSource is already playing. Skipping.");
                return;
            }

            if (_phrase == null)
            {
                Debug.LogWarning("No phrase assigned!");
                return;
            }

            if (_phrase.Audio == null)
            {
                Debug.LogError($"Phrase '{_phrase.Text}' has no AudioClip!");
                return;
            }

            if (_audioSource.isPlaying && _allowInterrupt)
            {
                _audioSource.Stop();
            }

            _audioSource.clip = _phrase.Audio;
            _audioSource.Play();

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

        public void LoadNextPhrase()
        {
            _phraseIndex++;
            LoadPhrase();
        }

        public void StopPlayingPhrase()
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        public void ResetUsed()
        {
            _used = false;
            Debug.Log("Phrase usage reset.");
        }

        public bool IsPlaying()
        {
            return _audioSource != null && _audioSource.isPlaying;
        }

        public bool IsUsed()
        {
            return _used;
        }
    }
}