using UnityEngine;

public class SymbolGrid : MonoBehaviour
{
    [Header("3×3 пиксели (порядок слева-направо, сверху-вниз)")]
    public GridPixel[] pixels = new GridPixel[9];

    [Header("Лок-маска (1 = заблокирован)")]
    [Tooltip("Если нужно сделать часть пикселей неактивной для рисования.")]
    public string lockMask9 = ""; // "001001000" или пусто

    [Header("Ожидаемый символ (опционально для эталона)")]
    public char expectedSymbol = '\0';
    public System.Action OnChanged;

    private void Awake()
    {
        ApplyLockMask(lockMask9);
    }

    public void ApplyLockMask(string mask9)
    {
        if (string.IsNullOrEmpty(mask9)) return;
        for (int i = 0; i < pixels.Length && i < mask9.Length; i++)
        {
            if (pixels[i]) pixels[i].SetLocked(mask9[i] == '1');
        }
    }

    public void SetFromMask(ushort mask)
    {
        for (int i = 0; i < 9 && i < pixels.Length; i++)
        {
            bool on = (mask & (1 << i)) != 0;
            if (pixels[i]) pixels[i].SetOn(on);
        }
    }

    public ushort GetMask()
    {
        ushort m = 0;
        for (int i = 0; i < 9 && i < pixels.Length; i++)
        {
            if (pixels[i] && pixels[i].IsOn) m |= (ushort)(1 << i);
        }
        return m;
    }

    public void ClearErrors()
    {
        foreach (var p in pixels) if (p) p.ClearError();
    }

    public void HighlightDiff(SymbolDictionary dict, char target, bool highlight = true)
    {
        if (!highlight) return; // если не нужно подсвечивать — выходим

        if (!dict.TryGetMask(target, out var refMask))
        {
            // если символ не в словаре — подсветим всё
            foreach (var p in pixels) if (p) p.SetError(true);
            return;
        }
        ushort cur = GetMask();
        for (int i = 0; i < 9 && i < pixels.Length; i++)
        {
            bool need = (refMask & (1 << i)) != 0;
            bool has  = (cur     & (1 << i)) != 0;
            bool bad = need != has;
            if (pixels[i]) pixels[i].SetError(bad);
        }
    }

    public void SetInteractive(bool enable)
    {
        // делаем/снимаем локи у ВСЕХ пикселей: эталон — выключить, игрок — включить
        for (int i = 0; i < pixels.Length; i++)
        {
            if (!pixels[i]) continue;
            pixels[i].SetLocked(!enable && true); // для эталона фиксируем
        }
    }
    
    public void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
    
    public void Clear()
    {
        foreach (var p in pixels)
            if (p) p.SetOn(false);
    }
}