using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public class ReferenceRowBuilder : MonoBehaviour
{
    [Header("Входные данные")]
    public CodePatternSO pattern;
    public SymbolDictionary symbolDictionary;

    [Header("Куда инстансить (контейнер с Layout Group)")]
    public Transform container; // ДОЛЖЕН быть объект из сцены

    [Header("Префаб слота (на нём должен быть SymbolGrid с 9 GridPixel)")]
    public GameObject slotPrefab;

    [Header("Опции построения")]
    public bool clearBeforeBuild = true;
    public bool trim = true;
    public bool ignoreNewLines = true;

    [Header("Ограничения")]
    [Min(1)] public int maxSymbols = 6;            // <= лимит длины эталона
    public bool takeLastIfOverLimit = false;        // false = первые N, true = последние N

    [Header("Когда строить (рантайм)")]
    public bool buildOnAwake = true;   // до Start
    public bool buildOnStart = false;  // после Start

    private void Awake()
    {
        if (buildOnAwake) RebuildRuntime();
    }

    private void Start()
    {
        if (buildOnStart && !buildOnAwake) RebuildRuntime();
    }

    [ContextMenu("Rebuild (Runtime)")]
    public void RebuildRuntime()
    {
        if (!ValidateSetup(logErrors: true)) return;

        string code = pattern?.code ?? "";
        if (trim) code = code.Trim();
        if (ignoreNewLines) code = code.Replace("\n", "").Replace("\r", "");

        // Применяем лимит длины
        int targetLen = Mathf.Min(code.Length, maxSymbols);
        bool truncated = code.Length > targetLen;
        if (truncated)
        {
            code = takeLastIfOverLimit
                ? code.Substring(code.Length - targetLen, targetLen)
                : code.Substring(0, targetLen);
        }

        // Очистка — строго Destroy в рантайме
        if (clearBeforeBuild)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }

        for (int i = 0; i < code.Length; i++)
        {
            char ch = code[i];

            var go = Instantiate(slotPrefab, container, false);
            go.name = $"RefSlot_{i:00}_{(ch == ' ' ? "␣" : ch.ToString())}";

            var grid = go.GetComponent<SymbolGrid>();
            if (!grid)
            {
                Debug.LogError($"[ReferenceRowBuilder] На префабе нет SymbolGrid. Пропущен индекс {i}.");
                continue;
            }

            grid.expectedSymbol = ch;

            if (symbolDictionary != null && symbolDictionary.TryGetMask(ch, out var mask))
                grid.SetFromMask(mask);
            else
                Debug.LogWarning($"[ReferenceRowBuilder] Нет маски для '{(ch==' ' ? "␣" : ch.ToString())}' (index {i}).");

            grid.SetInteractive(false);
            grid.ClearErrors();
        }

        var row = container.GetComponent<SlotsRow>();
        if (row) row.isReferenceRow = true;

        Debug.Log($"[ReferenceRowBuilder] Построено «{pattern?.patternName ?? "Unnamed"}». Длина: {code.Length}" +
                  (truncated ? $" (урезано до {maxSymbols})" : ""));
    }

    private bool ValidateSetup(bool logErrors)
    {
        bool ok = true;

        if (!container) { if (logErrors) Debug.LogError("[ReferenceRowBuilder] Не задан container."); ok = false; }
        if (!slotPrefab) { if (logErrors) Debug.LogError("[ReferenceRowBuilder] Не задан slotPrefab."); ok = false; }
        if (!pattern) { if (logErrors) Debug.LogError("[ReferenceRowBuilder] Не задан pattern (CodePatternSO)."); ok = false; }
        else if (pattern.code == null) { if (logErrors) Debug.LogError("[ReferenceRowBuilder] pattern.code = null."); ok = false; }

        if (slotPrefab)
        {
            var grid = slotPrefab.GetComponent<SymbolGrid>();
            if (!grid) { if (logErrors) Debug.LogError("[ReferenceRowBuilder] slotPrefab без SymbolGrid."); ok = false; }
            else if (grid.pixels == null || grid.pixels.Length != 9)
                Debug.LogWarning("[ReferenceRowBuilder] На slotPrefab у SymbolGrid.pixels не 9 ссылок.");
        }

        if (!symbolDictionary)
            Debug.LogWarning("[ReferenceRowBuilder] symbolDictionary не задан. Маски не будут выставлены.");

        return ok;
    }
}