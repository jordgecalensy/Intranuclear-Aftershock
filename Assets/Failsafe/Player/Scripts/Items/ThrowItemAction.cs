using Failsafe.Items;
using UnityEngine;

public class ThrowItemAction : IActionWithItem
{
    private float _throwForce;
    // Начальная точка справа от головы, пока так потому что нет анимации броска
    private Vector3 _startPosition = new Vector3(0.5f, 0, 0);
    private readonly Transform _cameraTransform;

    public ThrowItemAction(Transform cameraTransform, float _throwPower)
    {
        _cameraTransform = cameraTransform;
        _throwForce = _throwPower;
    }

    public ItemUseResult Execute(PlayerHandsContainer playerHandsContainer)
    {
        var direction = _cameraTransform.forward;
        var useResult = playerHandsContainer.ItemInHand.ItemUsable?.Use() ?? ItemUseResult.Consumed;
        var item = playerHandsContainer.DropItemFromHand();
        item.Use();
        item.gameObject.transform.position = _cameraTransform.position + _cameraTransform.rotation * _startPosition;
        var itemRb = item.GetComponent<Rigidbody>();
        itemRb.AddForce(direction * _throwForce, ForceMode.Impulse);

        return useResult;
    }
}
