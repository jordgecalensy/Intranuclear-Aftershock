using FMODUnity;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.PhrasesScript
{
    [CreateAssetMenu(fileName = "PhrasesData", menuName = "ScriptableObjects/PhrasesData")]
    public class PhrasesData : ScriptableObject
    {
        [SerializeField] private List<Phrase> _phrases = new List<Phrase>();
        public List<Phrase> Phrases => _phrases;
    }

    [System.Serializable]
    public class Phrase
    {
        public string Text;
        public EventReference FMODEvent;
        public int Weight = 1;
        public bool Once = false;
    }
}