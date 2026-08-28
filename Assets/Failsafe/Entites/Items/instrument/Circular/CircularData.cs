using Failsafe.Scripts.EffectSystem;
using System.Collections.Generic;
using FMODUnity;
using System;
using UnityEngine;

[Serializable]
public struct CircularStages
{
    public float Duration;
    public EffectBundle EffectBundle;
}

[CreateAssetMenu(fileName = "CircularData", menuName = "ScriptableObjects/Entities/Items/CircularData")]
public class CircularData : ScriptableObject
{
    public float MaxDistance;
    public List<CircularStages> CircularStages;
}
