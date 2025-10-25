using UnityEngine;
using VContainer;
using Failsafe.PlayerMovements.Controllers;

public class SpeedOverrideTest : MonoBehaviour
{
    [Inject] private PlayerMovementController _pmc;

    [SerializeField] private bool ApplySlow;
    [SerializeField, Range(0.05f,1f)] private float Multiplier = 0.3f;

    const int TestId = 123456; // стабильный ID

    void Update()
    {
        if (_pmc == null) return;

        if (ApplySlow)
            _pmc.SetSpeedModifier(TestId, Multiplier);
        else
            _pmc.RemoveSpeedModifier(TestId);

        if (Time.frameCount % 30 == 0)
            Debug.Log($"[SpeedOverrideTest] mul={_pmc.CurrentSpeedMultiplier:0.00}");
    }

    void OnDisable()
    {
        _pmc?.RemoveSpeedModifier(TestId);
    }
}