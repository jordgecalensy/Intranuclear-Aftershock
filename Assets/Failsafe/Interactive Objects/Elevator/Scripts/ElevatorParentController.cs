using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class ElevatorParentController : MonoBehaviour
{
    private sealed class PlayerOccupancy
    {
        public Transform OriginalParent;
        public readonly HashSet<Collider> Colliders = new HashSet<Collider>();
    }

    private readonly Dictionary<Transform, PlayerOccupancy> _players = new Dictionary<Transform, PlayerOccupancy>();

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform player = FindPlayerTransform(other.transform);
        if (player == null) return;

        if (!_players.TryGetValue(player, out PlayerOccupancy occupancy))
        {
            occupancy = new PlayerOccupancy
            {
                OriginalParent = player.parent
            };
            _players.Add(player, occupancy);
            player.SetParent(transform, true);
        }

        occupancy.Colliders.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        Transform player = FindPlayerTransform(other.transform);
        if (player == null || !_players.TryGetValue(player, out PlayerOccupancy occupancy)) return;

        occupancy.Colliders.Remove(other);
        if (occupancy.Colliders.Count > 0) return;

        RestoreParent(player, occupancy.OriginalParent);
        _players.Remove(player);
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<Transform, PlayerOccupancy> entry in _players)
        {
            if (entry.Key != null)
            {
                RestoreParent(entry.Key, entry.Value.OriginalParent);
            }
        }

        _players.Clear();
    }

    private static Transform FindPlayerTransform(Transform current)
    {
        while (current != null)
        {
            if (current.CompareTag("Player")) return current;
            current = current.parent;
        }

        return null;
    }

    private void RestoreParent(Transform player, Transform originalParent)
    {
        if (player.parent == transform)
        {
            player.SetParent(originalParent, true);
        }
    }
}
