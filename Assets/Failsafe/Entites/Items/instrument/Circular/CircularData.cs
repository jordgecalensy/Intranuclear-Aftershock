using Failsafe.Scripts.EffectSystem;
using System.Collections.Generic;
using FMODUnity;
using System;
using UnityEngine;

[Serializable]
public struct CircularStages
{
    public float StageDuration;
    public EffectBundle EffectBundle;
}

[CreateAssetMenu(fileName = "CircularData", menuName = "ScriptableObjects/Entities/Items/CircularData")]
public class CircularData : ScriptableObject
{
    [Header("Стадии работы циркулярки")]
    [SerializeField] private List<CircularStages> _circularStages;

    [Header("Модификаторы времени заводки и остановки циркулярки")]
    [SerializeField] private float _timeChargeModifier = 1;
    [SerializeField] private float _timeDischargeModifier = 1;

    public List<CircularStages> CircularStages
    {
        get { return _circularStages; }
    }
    public float TimeChargeModifier
    {
        get { return _timeChargeModifier; }
    }
    public float TimeDischargeModifier
    {
        get { return _timeDischargeModifier; }
    }
}
