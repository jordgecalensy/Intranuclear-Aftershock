using TMPro;
using UnityEngine;

public class TMPDropdownCustomItem : Interactable
{
    [SerializeField] private TMP_Text _label;

    private TMPDropdownRoot _dropdown;
    private int _index;

    private void Reset()
    {
        if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(TMPDropdownRoot dropdown, int index)
    {
        _dropdown = dropdown;
        _index = index;
    }

    public void SetText(string text)
    {
        if (_label != null) _label.text = text;
    }

    public void EnsureCollider()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();

        var rt = transform as RectTransform;
        if (rt != null)
        {
            col.center = Vector3.zero;
            col.size = new Vector3(rt.rect.width, rt.rect.height, 1f);
        }
    }

    protected override void Interact()
    {
        if (_dropdown == null) return;
        _dropdown.Select(_index);
    }
}
