using UnityEngine;
using System;

public class TMPDropdownOwnerId : MonoBehaviour
{
    [SerializeField] private string _id;
    public string Id => _id;

    public void SetId(string id)
    {
        _id = id;
    }

    private void Reset()
    {
        EnsureId();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureId();
    }
#endif

    private void EnsureId()
    {
        if (string.IsNullOrEmpty(_id))
            _id = Guid.NewGuid().ToString("N");
    }
}
