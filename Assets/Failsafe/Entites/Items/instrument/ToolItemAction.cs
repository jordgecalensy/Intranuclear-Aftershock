using Failsafe.Items;
using UnityEngine;

public class ToolItemAction : IActionWithItem
{
    private readonly Transform _cameraTransform;
    public ToolItemAction(Transform cameraTransform)
    {
        _cameraTransform = cameraTransform;
    }
    public ItemUseResult Execute(PlayerHandsContainer playerHandsContainer)
    {
        return new ItemUseResult { UsageType = UsageType.HoldToUse };
    }
}
