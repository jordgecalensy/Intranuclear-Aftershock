using UnityEngine;

public sealed class PlayerInteractionContext
{
    public PlayerInteraction PlayerInteraction { get; }
    public PlayerHandsContainer HandsContainer { get; }
    public Camera PlayerCamera { get; }
    public RaycastHit Hit { get; }

    public PlayerInteractionContext(
        PlayerInteraction playerInteraction,
        PlayerHandsContainer handsContainer,
        Camera playerCamera,
        RaycastHit hit)
    {
        PlayerInteraction = playerInteraction;
        HandsContainer = handsContainer;
        PlayerCamera = playerCamera;
        Hit = hit;
    }

    public bool TryGetItemInHand(out Item item)
    {
        item = null;

        if (HandsContainer == null)
            return false;

        if (HandsContainer.State == PlayerHandsContainer.HandState.EmptyHands)
            return false;

        item = HandsContainer.ItemInHand?.ItemObject;

        return item != null;
    }
}