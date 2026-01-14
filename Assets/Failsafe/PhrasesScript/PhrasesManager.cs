using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Failsafe.PhrasesScript
{
    public class PhrasesManager : MonoBehaviour
    {
        [SerializeField] private PhrasesData _phrasesData;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private bool _allowInterrupt = false;
        [SerializeField] private bool _oneUsing = false;

        private List<Phrase> _phrases = new List<Phrase>();
        private Dictionary<Phrase, bool> _usedPhrases = new Dictionary<Phrase, bool>();
        private bool _used = false;

        private void Awake()
        {
            ValidateComponents();
            LoadPhrases();
        }

        private void ValidateComponents()
        {
            if (_audioSource == null)
            {
                Debug.LogWarning("AudioSource not assigned. Adding one automatically.");
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;

            if (_phrasesData == null)
            {
                Debug.LogError("PhrasesData is not assigned!");
            }
        }

        private void LoadPhrases()
        {
            if (_phrasesData == null)
            {
                return;
            }

            _phrases.Clear();
            _usedPhrases.Clear();

            foreach (var phraseData in _phrasesData.Phrases)
            {
                if (phraseData.Audio == null)
                {
                    Debug.LogWarning($"Phrase '{phraseData.Text}' has no AudioClip assigned!");
                    continue;
                }

                _phrases.Add(phraseData);
                _usedPhrases[phraseData] = false;
            }

            if (_phrases.Count == 0)
            {
                Debug.LogWarning("No valid phrases loaded!");
            }
        }

        private Phrase GetPhrase()
        {
            if (_phrases == null || _phrases.Count == 0)
            {
                Debug.LogWarning("No phrases available!");
                return null;
            }

            var availableOnce = _phrases
                .Where(p => p.Once && !_usedPhrases[p])
                .OrderByDescending(p => p.Weight)
                .ToList();

            if (availableOnce.Count > 0)
            {
                return availableOnce[0];
            }

            var availableRegular = _phrases
                .Where(p => !p.Once && !_usedPhrases[p])
                .ToList();

            if (availableRegular.Count == 0)
            {
                Debug.LogWarning("All phrases have been used!");
                return null;
            }

            return availableRegular[0];
        }

        public void PlayPhrase()
        {
            if (_oneUsing && _used)
            {
                Debug.Log("Phrase already used in one-using mode. Skipping.");
                return;
            }

            if (_audioSource.isPlaying && !_allowInterrupt)
            {
                Debug.Log("AudioSource is already playing. Skipping.");
                return;
            }

            var phrase = GetPhrase();
            if (phrase == null)
            {
                Debug.LogWarning("No phrase available to play!");
                return;
            }

            if (phrase.Audio == null)
            {
                Debug.LogError($"Phrase '{phrase.Text}' has no AudioClip!");
                return;
            }

            if (_audioSource.isPlaying && _allowInterrupt)
            {
                _audioSource.Stop();
            }

            _audioSource.clip = phrase.Audio;
            _audioSource.Play();

            if (_oneUsing)
            {
                _used = true;
            }

            _usedPhrases[phrase] = true;

            Debug.Log($"Playing phrase: {phrase.Text}");
        }

        public void StopPlayingPhrase()
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        public void ResetUsedPhrases()
        {
            foreach (var phrase in _phrases)
            {
                _usedPhrases[phrase] = false;
            }
            Debug.Log("All phrases reset.");
        }

        public void ResetOneUsing()
        {
            _used = false;
            Debug.Log("One-using reset.");
        }

        public bool HasAvailablePhrases()
        {
            return _phrases.Any(p => !_usedPhrases[p]);
        }

        public int GetAvailablePhrasesCount()
        {
            return _phrases.Count(p => !_usedPhrases[p]);
        }
    }
}