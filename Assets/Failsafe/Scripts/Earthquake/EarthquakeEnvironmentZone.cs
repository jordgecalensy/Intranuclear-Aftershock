using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

public class EarthquakeEnvironmentZone : MonoBehaviour
{
    [SerializeField] private string _carryObjectsLayerName = "CarryObjects";
    [SerializeField] private string _destructibleLayerName = "DestructableByEarthquake";

    private readonly List<Rigidbody> _carryObjects = new();
    private readonly List<GameObject> _destructibleObjects = new();
    private readonly Collider[] _overlapResults = new Collider[256];

    private Collider[] _zoneColliders;
    private StudioEventEmitter _earthquakeEmitter;

    private int _carryLayer;
    private int _destructibleLayer;
    private int _scanMask;
    private int _activeEarthquakeCount;

    public IReadOnlyList<Rigidbody> CarryObjects => _carryObjects;
    public IReadOnlyList<GameObject> DestructibleObjects => _destructibleObjects;

    private void Awake()
    {
        _carryLayer = LayerMask.NameToLayer(_carryObjectsLayerName);
        _destructibleLayer = LayerMask.NameToLayer(_destructibleLayerName);
        _scanMask = (1 << _carryLayer) | (1 << _destructibleLayer);
        _zoneColliders = GetComponents<Collider>();
        _earthquakeEmitter = GetComponent<StudioEventEmitter>();
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterObject(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterObject(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (MatchesLayer(other, _carryLayer))
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null)
                _carryObjects.Remove(rb);
        }

        if (MatchesLayer(other, _destructibleLayer))
        {
            _destructibleObjects.Remove(GetDestructibleObject(other));
        }
    }

    private void RegisterObject(Collider other)
    {
        if (MatchesLayer(other, _carryLayer))
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null && !_carryObjects.Contains(rb))
            {
                _carryObjects.Add(rb);
                //Debug.Log($"[EarthquakeEnvironmentZone] Registered carry object: {rb.gameObject.name}");
            }
        }

        if (MatchesLayer(other, _destructibleLayer))
        {
            GameObject go = GetDestructibleObject(other);
            if (!_destructibleObjects.Contains(go))
            {
                _destructibleObjects.Add(go);
                //Debug.Log(
                    //$"[EarthquakeEnvironmentZone] Registered destructible object: {go.name}. " +
                    //$"Collider={other.gameObject.name}, Layer={LayerMask.LayerToName(go.layer)}");
            }
        }
    }

    private static bool MatchesLayer(Collider collider, int targetLayer)
    {
        if (collider.gameObject.layer == targetLayer)
            return true;

        if (collider.attachedRigidbody != null && collider.attachedRigidbody.gameObject.layer == targetLayer)
            return true;

        return collider.transform.gameObject.layer == targetLayer;
    }

    private static GameObject GetDestructibleObject(Collider collider)
    {
        if (collider.attachedRigidbody != null)
            return collider.attachedRigidbody.gameObject;

        return collider.transform.gameObject;
    }

    public void CleanupNulls()
    {
        for (int i = _carryObjects.Count - 1; i >= 0; i--)
        {
            if (_carryObjects[i] == null)
                _carryObjects.RemoveAt(i);
        }

        for (int i = _destructibleObjects.Count - 1; i >= 0; i--)
        {
            if (_destructibleObjects[i] == null)
                _destructibleObjects.RemoveAt(i);
        }
    }

    public void RefreshObjects()
    {
        CleanupNulls();

        _carryObjects.Clear();
        _destructibleObjects.Clear();

        if (_zoneColliders == null || _zoneColliders.Length == 0)
            return;

        for (int i = 0; i < _zoneColliders.Length; i++)
        {
            Collider zoneCollider = _zoneColliders[i];
            if (zoneCollider == null || !zoneCollider.enabled)
                continue;

            Bounds bounds = zoneCollider.bounds;
            int hitCount = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                _overlapResults,
                Quaternion.identity,
                _scanMask,
                QueryTriggerInteraction.Collide);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = _overlapResults[hitIndex];
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                RegisterObject(hit);
                _overlapResults[hitIndex] = null;
            }
        }
    }

    public void BeginEarthquakeAudio()
    {
        _activeEarthquakeCount++;

        if (_activeEarthquakeCount > 1)
            return;

        if (_earthquakeEmitter == null)
        {
            //Debug.LogWarning($"[EarthquakeEnvironmentZone] StudioEventEmitter not found on zone '{name}'.");
            return;
        }

        _earthquakeEmitter.Play();
        //Debug.Log($"[EarthquakeEnvironmentZone] Earthquake audio started for zone '{name}'.");
    }

    public void EndEarthquakeAudio()
    {
        _activeEarthquakeCount = Mathf.Max(0, _activeEarthquakeCount - 1);

        if (_activeEarthquakeCount > 0)
            return;

        if (_earthquakeEmitter == null)
            return;

        _earthquakeEmitter.Stop();
        //Debug.Log($"[EarthquakeEnvironmentZone] Earthquake audio stopped for zone '{name}'.");
    }
}
