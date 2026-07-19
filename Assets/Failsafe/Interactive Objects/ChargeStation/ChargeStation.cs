using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChargeStation : Interactable
{
    [Header("Charging")]
    [SerializeField] private float _chargingDuration = 2f;

    [Tooltip("Если true, станция заряжает предмет сразу до максимума.")]
    [SerializeField] private bool _fillToMax = true;

    [Tooltip("Если Fill To Max выключен, столько заряда добавляется за один цикл.")]
    [SerializeField] private int _chargeAmountPerCycle = 1;

    [Header("Animation")]
    [SerializeField] private Animation _lidAnimation;
    [SerializeField] private string _lidCloseAnimationName = "LidClose";
    [SerializeField] private string _lidOpenAnimationName = "LidOpen";

    [Header("UI")]
    [SerializeField] private Image _noItem;
    [SerializeField] private Image _itemPlaced;
    [SerializeField] private Image _chargeBar;
    [SerializeField] private Image[] _bars;

    private bool _chargingOngoing;

    protected override void Interact(PlayerInteractionContext context)
    {
        if (_chargingOngoing)
            return;

        if (context == null)
        {
            Debug.LogError("[ChargeStation] Interaction context is null.", this);
            return;
        }

        if (!context.TryGetItemInHand(out Item item))
        {
            Debug.Log("[ChargeStation] В руке нет предмета.", this);
            UpdateUI(null);
            return;
        }

        if (!item.HasEnergySystem())
        {
            Debug.Log($"[ChargeStation] У предмета {item.name} нет системы заряда.", item);
            UpdateUI(item);
            return;
        }

        if (item.IsEnergyFull())
        {
            Debug.Log($"[ChargeStation] Предмет {item.name} уже полностью заряжен: {item.EnergyAmountCurrent}/{item.EnergyAmountMax}", item);
            UpdateUI(item);
            return;
        }

        StartCoroutine(Charging(context, item));
    }

    private IEnumerator Charging(PlayerInteractionContext context, Item item)
    {
        _chargingOngoing = true;

        PlayAnimation(_lidCloseAnimationName);
        SetChargingUI(item);

        yield return new WaitForSeconds(Mathf.Max(0f, _chargingDuration));

        if (context != null &&
            context.TryGetItemInHand(out Item currentItem) &&
            currentItem == item &&
            item.HasEnergySystem())
        {
            if (_fillToMax)
                item.FillEnergy();
            else
                item.ReloadEnergy(_chargeAmountPerCycle);

            Debug.Log($"[ChargeStation] Заряжен предмет {item.name}: {item.EnergyAmountCurrent}/{item.EnergyAmountMax}", item);
        }
        else
        {
            Debug.Log("[ChargeStation] Зарядка отменена: предмет уже не в руке.", this);
        }

        PlayAnimation(_lidOpenAnimationName);

        _chargingOngoing = false;

        UpdateUI(item);
    }

    private void PlayAnimation(string animationName)
    {
        if (_lidAnimation == null)
            return;

        if (string.IsNullOrWhiteSpace(animationName))
            return;

        _lidAnimation.Play(animationName);
    }

    private void UpdateUI(Item item)
    {
        bool hasChargeableItem =
            item != null &&
            item.HasEnergySystem();

        if (_noItem != null)
            _noItem.enabled = !hasChargeableItem;

        if (_itemPlaced != null)
            _itemPlaced.enabled = hasChargeableItem;

        if (_chargeBar != null)
            _chargeBar.enabled = false;

        SetBarsVisible(false);
        UpdateBars(item);
    }

    private void SetChargingUI(Item item)
    {
        if (_noItem != null)
            _noItem.enabled = false;

        if (_itemPlaced != null)
            _itemPlaced.enabled = false;

        if (_chargeBar != null)
            _chargeBar.enabled = true;

        SetBarsVisible(true);
        UpdateBars(item);
    }

    private void SetBarsVisible(bool visible)
    {
        if (_bars == null)
            return;

        foreach (Image bar in _bars)
        {
            if (bar == null)
                continue;

            bar.enabled = visible;
        }
    }

    private void UpdateBars(Item item)
    {
        if (_bars == null || _bars.Length == 0)
            return;

        if (item == null || !item.HasEnergySystem())
        {
            foreach (Image bar in _bars)
            {
                if (bar != null)
                    bar.fillAmount = 0f;
            }

            return;
        }

        int maxEnergy = Mathf.Max(1, item.EnergyAmountMax);
        int currentEnergy = Mathf.Clamp(item.EnergyAmountCurrent, 0, maxEnergy);

        float normalized = currentEnergy / (float)maxEnergy;

        for (int i = 0; i < _bars.Length; i++)
        {
            if (_bars[i] == null)
                continue;

            float threshold = (i + 1f) / _bars.Length;
            _bars[i].fillAmount = normalized >= threshold ? 1f : 0f;
        }
    }
    public void OnButtonPress(PlayerInteractionContext context)
    {
        if (_chargingOngoing)
            return;

        if (context == null)
        {
            Debug.LogError("[ChargeStation] Interaction context is null.", this);
            return;
        }

        if (!context.TryGetItemInHand(out Item item))
        {
            Debug.Log("[ChargeStation] В руке нет предмета.", this);
            UpdateUI(null);
            return;
        }

        if (!item.HasEnergySystem())
        {
            Debug.Log($"[ChargeStation] У предмета {item.name} нет системы заряда.", item);
            UpdateUI(item);
            return;
        }

        if (item.IsEnergyFull())
        {
            Debug.Log($"[ChargeStation] Предмет {item.name} уже полностью заряжен: {item.EnergyAmountCurrent}/{item.EnergyAmountMax}", item);
            UpdateUI(item);
            return;
        }

        StartCoroutine(Charging(context, item));
    }
}