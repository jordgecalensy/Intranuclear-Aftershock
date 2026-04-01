using System;
using System.Collections;
using UnityEngine;

public class ChargeStation : MonoBehaviour, IEnterable
{
    [SerializeField] Transform _posForPistolGO;
    [SerializeField] int _containedEnergyAmount = 2000;
    private bool _chargingOngoing;
    private bool _isEmpty = true;
    [SerializeField] private Animation _lidAnimation;
    public event Action<bool> UpdateIsEmpty;

    public void OnButtonPress()
    {
        /*EnergyContainerOLD connectedEnergyContainer = other.GetComponent<EnergyContainerOLD>();
        //other.transform.position = _posForPistolGO.position;
        //other.GetComponent<Rigidbody>().isKinematic = true;
        if (connectedEnergyContainer != null)
        {
            int energyAmountOfConnectedObj = connectedEnergyContainer.GetAmountForMax();
            if (_containedEnergyAmount >= energyAmountOfConnectedObj && !connectedEnergyContainer.IsFull())
            {
                connectedEnergyContainer.Reload(energyAmountOfConnectedObj);
                _containedEnergyAmount -= energyAmountOfConnectedObj;
                if (_containedEnergyAmount == 0)
                    Destroy(this.gameObject);
            }

        }*/

        if(!_chargingOngoing)
            StartCoroutine(Charging());
    }

    private IEnumerator Charging()
    {
        _chargingOngoing = true;
        _lidAnimation.Play("LidClose");
        Debug.Log("Foo");

        yield return new WaitForSeconds(2);

        _lidAnimation.Play("LidOpen");
        _chargingOngoing = false;
        Debug.Log("FooEnd");
    }
    public void OnEntered()
    {
        Debug.LogWarning("NOTEMPTY");
        _isEmpty = false;
    }

    public void OnExited()
    {
        _isEmpty = true;
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<Item>().ItemData.Type == ItemType.Gun;
    }

    public bool IsEmpty()
    {
        return _isEmpty;
    }
}
