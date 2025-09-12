using UnityEngine;

[ExecuteAlways]
public class ReferenceRowBuilder : MonoBehaviour
{
    [Header("Входные данные")]
    public CodePatternSO pattern;
    public SymbolDictionary symbolDictionary;

    [Header("Куда инстансить (контейнер с Layout Group)")]
    public Transform container;

    [Header("Префаб слота (на нём должен быть SymbolGrid с 9 GridPixel)")]
    public GameObject slotPrefab;

    [Header("Опции")]
    public bool clearBeforeBuild = true;
    public bool trim = true;            // продублировали здесь на всякий
    public bool ignoreNewLines = true;

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        if (!ValidateSetup()) return;

        string code = pattern?.code ?? "";
        if (trim) code = code.Trim();
        if (ignoreNewLines) code = code.Replace("\n", "").Replace("\r", "");

        if (clearBeforeBuild)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                DestroyImmediate(container.GetChild(i).gameObject);
        }

        for (int i = 0; i < code.Length; i++)
        {
            char ch = code[i];

            var go = Instantiate(slotPrefab, container);
            go.name = $"RefSlot_{i:00}_{(ch==' ' ? "␣" : ch.ToString())}";

            var grid = go.GetComponent<SymbolGrid>();
            if (!grid)
            {
                Debug.LogError($"[ReferenceRowBuilder] На префабе нет SymbolGrid. Пропущен индекс {i}.");
                continue;
            }

            // задаём ожидаемый символ всегда
            grid.expectedSymbol = ch;

            // пытаемся проставить пиксели из словаря
            if (symbolDictionary != null && symbolDictionary.TryGetMask(ch, out var mask))
            {
                grid.SetFromMask(mask);
            }
            else
            {
                // Нет записи в словаре — не смертельно: оставим пустым, но сообщим
                Debug.LogWarning($"[ReferenceRowBuilder] В словаре нет маски для символа '{(ch==' ' ? "␣" : ch.ToString())}' (index {i}). " +
                                 $"Добавь запись в SymbolDictionary.");
            }

            // эталон не должен редактироваться
            grid.SetInteractive(false);
            grid.ClearErrors();
        }

        var row = container.GetComponent<SlotsRow>();
        if (row) row.isReferenceRow = true;

        Debug.Log($"[ReferenceRowBuilder] Построено «{pattern.patternName}». Длина: {code.Length}");
    }

    private bool ValidateSetup()
    {
        bool ok = true;

        if (!container) { Debug.LogError("[ReferenceRowBuilder] Не задан container."); ok = false; }
        if (!slotPrefab) { Debug.LogError("[ReferenceRowBuilder] Не задан slotPrefab."); ok = false; }
        if (!pattern)
        {
            Debug.LogError("[ReferenceRowBuilder] Не задан pattern (CodePatternSO).");
            ok = false;
        }
        else if (pattern.code == null)
        {
            Debug.LogError("[ReferenceRowBuilder] pattern.code = null.");
            ok = false;
        }

        // Предупреждаем (но не блокируем) про пустой словарь
        if (!symbolDictionary)
            Debug.LogWarning("[ReferenceRowBuilder] symbolDictionary не задан. Маски не будут выставлены.");

        // Предпроверка префаба
        if (slotPrefab)
        {
            var grid = slotPrefab.GetComponent<SymbolGrid>();
            if (!grid)
            {
                Debug.LogError("[ReferenceRowBuilder] slotPrefab без SymbolGrid.");
                ok = false;
            }
            else
            {
                if (grid.pixels == null || grid.pixels.Length != 9)
                    Debug.LogWarning("[ReferenceRowBuilder] На slotPrefab у SymbolGrid.pixels не 9 ссылок. " +
                                     "Убедись, что в инстансах они заполнены по порядку 0..8.");
            }
        }

        return ok;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Позволяет пересобирать превью прямо в редакторе при изменении данных
        if (!Application.isPlaying)
        {
            // маленькая защита от спама пересборкой при каждом чихе
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this) Rebuild();
            };
        }
    }
#endif
}