using FMODUnity;
using Failsafe.Scripts.EffectSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PostEffect
{
    public float Duration;
    public GameObject Effect;
}
[Serializable]
public struct ExplosiveVFX
{
    public float Duration;
    public GameObject Effect;
}
//    protected Dictionary<Direction, PowerNode> Neighbors;
//    [SerializeField] protected List<DirectionNodePair> NeighborsSerialized = new List<DirectionNodePair>();
[CreateAssetMenu(fileName = "ExplosiveObjectData", menuName = "ScriptableObjects/ExplosiveObject")]
public class ExplosiveObjectData : ScriptableObject
{
    [Header("Ignore Collision")]
    [SerializeField] private bool _ignoreCollision;
    [Header("Base explosin setting")]
    [SerializeField] private int _explosionDamage;
    [SerializeField] private float _explosionRadius;
    [SerializeField] private float _explosionForce;
    [Header("Effect System")]
    [SerializeField] private EffectBundle _explosionEffects;
    [Header("Explosion post effect")]
    [SerializeField] private List<PostEffect> _postEffect;
    [Header("On Enemy Effect")]
    [SerializeField] private float _durationOnEnemyEffect;
    [SerializeField] private GameObject _onEnemyEffect;
    [Header("Explosion VFX effect")]
    [SerializeField] private List<ExplosiveVFX> _explosiveVFX;
    [Header("Explosion SFX effect")]
    [SerializeField] private EventReference _explsiveSFXEvent;

    public int ExplosionDamage
    {
        get
        {
            return _explosionDamage;
        }
    }
    public float ExplosionRadius
    {
        get
        {
            return _explosionRadius;
        }
    }
    public float ExplosionForce
    {
        get
        {
            return _explosionForce;
        }
    }
    public EffectBundle ExplosionEffects
    {
        get
        {
            return _explosionEffects;
        }
    }
    public List<PostEffect> PostEffects
    {
        get
        {
            return _postEffect;
        }
    }
    public float DurationOnEnemyEffect
    {
        get
        {
            return _durationOnEnemyEffect;
        }
    }
    public GameObject OnEnemyEffect
    {
        get
        {
            return _onEnemyEffect;
        }
    }
    public List<ExplosiveVFX> ExplosiveVfx
    {
        get
        {
            return _explosiveVFX;
        }
    }
    public EventReference ExplsiveSFXEvent
    {
        get
        {
            return _explsiveSFXEvent;
        }
    }
    public bool IgnoreCollision
    {
        get
        {
            return _ignoreCollision;
        }
    }
}
