using UnityEngine;
using Assets.Failsafe.Scripts.interaction_System;

public class DoorScript : MonoBehaviour
{
    private Animator _animator;
    private string _enemyTag = "Enemy";

    private bool _enemyBlockDoor = false;
    private bool _doorWasOpen = false;
    private bool _statusVisualsInitialized = false;
    private bool _lastIsPowered;
    private bool _lastNeedsCard;

    [SerializeField] private bool _isPowered;
    [SerializeField] private bool _isOpen;
    [SerializeField] private bool _hasCard;
    [SerializeField] private CarryObjectPlaceArea _cardPlaceArea;
    [SerializeField] private Material _poweredReadyMaterial;
    [SerializeField] private Material _needsCardMaterial;
    [SerializeField] private Material _noPowerMaterial;
    [SerializeField] private Renderer[] _emissiveRenderers;
    [SerializeField] private GameObject[] _poweredReadyGifObjects;
    [SerializeField] private GameObject[] _needsCardGifObjects;
    [SerializeField] private GameObject[] _noPowerGifObjects;

    public bool IsPowered => _isPowered;

    private void Start()
    {
        _animator = GetComponent<Animator>();
        _animator.SetBool("isOpen", _isOpen);
        UpdateCardSlotCollider();
        UpdateStatusVisuals();
    }

    private void Update()
    {
        UpdateStatusVisuals();
    }
    public void OnPowered()
    {
        Debug.Log("Door power on");
        _isPowered = true;
        UpdateStatusVisuals();
        if (_enemyBlockDoor)
        {
            _isOpen = true;
            _animator.SetBool("isOpen", true);
            Debug.Log("Active Door");
        }
    }
    public void OffPowered()
    {
        Debug.Log("Door power off");
        _isPowered = false;
        UpdateStatusVisuals();
    }
    private void OpenCloseDoor(bool open)
    {
        if (!_isPowered) return;
        if (NeedsCard()) return;
        if (_enemyBlockDoor) return;
        _isOpen = open;
        _animator.SetBool("isOpen", open);
        Debug.Log("Active Door");
    }
    public void InteractDoor()
    {
        OpenCloseDoor(!_isOpen);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag(_enemyTag)) return;


        _doorWasOpen = _isOpen;
        OpenCloseDoor(true);
        _enemyBlockDoor = true;
    }
    private void OnTriggerExit(Collider other)
    {
        if (!other.gameObject.CompareTag(_enemyTag)) return;

        _enemyBlockDoor = false;
        UpdateStatusVisuals(true);

        if (_doorWasOpen) { return; }

        OpenCloseDoor(false);
    }

    private void UpdateStatusVisuals(bool force = false)
    {
        bool needsCard = NeedsCard();

        if (!force && _statusVisualsInitialized && _lastIsPowered == _isPowered && _lastNeedsCard == needsCard)
        {
            return;
        }

        ChangeEmissiveMaterial(GetStatusMaterial(needsCard));
        ChangeGifObjects(needsCard);
        UpdateCardSlotCollider();

        _lastIsPowered = _isPowered;
        _lastNeedsCard = needsCard;
        _statusVisualsInitialized = true;
    }

    private Material GetStatusMaterial(bool needsCard)
    {
        if (!_isPowered)
        {
            return _noPowerMaterial;
        }

        if (needsCard)
        {
            return _needsCardMaterial;
        }

        return _poweredReadyMaterial;
    }

    private bool NeedsCard()
    {
        return !_hasCard && _cardPlaceArea != null && _cardPlaceArea.IsEmpty;
    }

    private void UpdateCardSlotCollider()
    {
        if (_cardPlaceArea == null)
        {
            return;
        }

        _cardPlaceArea.SetSlotColliderEnabled(NeedsCard());
    }

    private void ChangeEmissiveMaterial(Material material)
    {
        if (_emissiveRenderers == null || material == null)
        {
            return;
        }

        foreach (Renderer emissiveRenderer in _emissiveRenderers)
        {
            if (emissiveRenderer == null)
            {
                continue;
            }

            emissiveRenderer.sharedMaterial = material;
        }
    }

    private void ChangeGifObjects(bool needsCard)
    {
        SetObjectsActive(_noPowerGifObjects, !_isPowered);
        SetObjectsActive(_needsCardGifObjects, _isPowered && needsCard);
        SetObjectsActive(_poweredReadyGifObjects, _isPowered && !needsCard);
    }

    private void SetObjectsActive(GameObject[] objects, bool isActive)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject targetObject in objects)
        {
            if (targetObject == null)
            {
                continue;
            }

            targetObject.SetActive(isActive);
        }
    }
}
