using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Failsafe/Symbol Dictionary", fileName = "SymbolDictionary")]
public class SymbolDictionary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Символ(ы) — можно указать несколько, например 'AА' для латиницы и кириллицы")]
        public string key = "A";

        [Tooltip("3×3: слева-направо, сверху-вниз")]
        public bool[] cells = new bool[9];

        [TextArea] public string description;

        [HideInInspector] public ushort mask9;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    // Новый формат: символ -> список масок
    private Dictionary<char, List<ushort>> _map;
    private Dictionary<char, string> _desc;

    private void OnEnable()
    {
        RebuildDictionaries();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildDictionaries();
    }
#endif

    private void RebuildDictionaries()
    {
        if (entries == null) return;

        _map  = new Dictionary<char, List<ushort>>(entries.Count);
        _desc = new Dictionary<char, string>(entries.Count);

        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.key)) continue;

            if (e.cells == null || e.cells.Length != 9)
                e.cells = ResizeTo9(e.cells);

            e.mask9 = BuildMaskFromCells(e.cells);

            foreach (var c in e.key)
            {
                if (!_map.ContainsKey(c)) _map[c] = new List<ushort>();
                if (!_map[c].Contains(e.mask9)) _map[c].Add(e.mask9);
                if (!_desc.ContainsKey(c)) _desc.Add(c, e.description ?? "");
            }
        }
    }

    // Возвращает первый вариант маски для символа (для обратной совместимости)
    public bool TryGetMask(char c, out ushort mask)
    {
        mask = 0;
        return _map != null && _map.TryGetValue(c, out var list) && list.Count > 0 && (mask = list[0]) >= 0;
    }

    public string GetDescription(char c)
    {
        return _desc != null && _desc.TryGetValue(c, out var d) ? d : "";
    }

    /// Узнаёт символ по маске (точно; при allowNearest — выбирает ближайший по расстоянию Хэмминга)
    public bool TryRecognize(ushort mask9, out char symbol, bool allowNearest = false)
    {
        symbol = '\0';
        if (_map == null || _map.Count == 0) return false;

        // точное совпадение среди всех вариантов
        foreach (var kv in _map)
        {
            foreach (var m in kv.Value)
                if (m == mask9) { symbol = kv.Key; return true; }
        }

        if (!allowNearest) return false;

        // ближайший по Хэммингу среди всех вариантов
        int best = 10; char bestC = '\0';
        foreach (var kv in _map)
        {
            foreach (var m in kv.Value)
            {
                int d = Hamming(mask9, m);
                if (d < best) { best = d; bestC = kv.Key; }
            }
        }
        if (bestC != '\0') { symbol = bestC; return true; }
        return false;
    }

    // ===== helpers =====
    public static ushort BuildMaskFromCells(bool[] cells9)
    {
        ushort m = 0;
        if (cells9 == null) return m;
        for (int i = 0; i < Mathf.Min(9, cells9.Length); i++)
            if (cells9[i]) m |= (ushort)(1 << i);
        return m;
    }

    public static string Mask9ToString(ushort m)
    {
        char[] a = new char[9];
        for (int i = 0; i < 9; i++) a[i] = ((m & (1 << i)) != 0) ? '1' : '0';
        return new string(a);
    }

    public static int Hamming(ushort a, ushort b)
    {
        int x = a ^ b, cnt = 0;
        while (x != 0) { x &= (x - 1); cnt++; }
        return cnt;
    }

    private static bool[] ResizeTo9(bool[] src)
    {
        var a = new bool[9];
        if (src == null) return a;
        for (int i = 0; i < Mathf.Min(9, src.Length); i++) a[i] = src[i];
        return a;
    }
}