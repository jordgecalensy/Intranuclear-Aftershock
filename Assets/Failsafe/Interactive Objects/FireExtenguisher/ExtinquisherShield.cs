using UnityEngine;

public class ExtinquisherShield : MonoBehaviour, IEnterable
{
    public void OnEntered()
    {
    }

    public void OnExited()
    {
    }

    public bool IsRightType(Component candidate)
    {
        return candidate.GetComponent<ExtinguisherCarryable>() != null;
    }
}
