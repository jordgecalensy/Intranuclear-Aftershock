using UnityEngine;

public class CodeOrderJudge : MonoBehaviour
{
    [Header("Ссылки")]
    public SlotsRow referenceRow;
    public SlotsRow playerRow;
    public SymbolDictionary symbolDictionary;

    [Header("Опции")]
    public bool requireSameLength = true;
    public bool allowNearestRecognition = false; // можно включить "ближайший по Хэмминга"
    
    private void Awake()
    {
        foreach (var g in playerRow.GetGrids())
            g.OnChanged += Check;
    }
    public void Check()
    {
        if (!referenceRow || !playerRow || !symbolDictionary)
        {
            Debug.LogError("[CodeOrderJudge] Задай referenceRow/playerRow/dict");
            return;
        }

        var refs = referenceRow.GetGrids();
        var usrs = playerRow.GetGrids();


        if (requireSameLength && refs.Count != usrs.Count)
        {
            foreach (var g in usrs) g?.HighlightDiff(symbolDictionary, g.expectedSymbol); // подсветим как неверные
            Debug.Log("[CodeOrderJudge] FAIL: разные длины ряда.");
            return;
        }

        int n = Mathf.Min(refs.Count, usrs.Count);
        bool allOk = true;

        for (int i = 0; i < n; i++)
        {
            var refGrid = refs[i];
            var usrGrid = usrs[i];
            if (!refGrid || !usrGrid) { allOk = false; continue; }

            char expected = refGrid.expectedSymbol;
            if (expected == '\0')
            {
                // если expected не задан, определим его по маске эталона
                if (!symbolDictionary.TryRecognize(refGrid.GetMask(), out expected, false))
                {
                    allOk = false;
                    // Убрана подсветка ошибок для эталонных ячеек
                    continue;
                }
            }

            // распознаём у игрока
            ushort userMask = usrGrid.GetMask();
            if (!symbolDictionary.TryRecognize(userMask, out var got, allowNearestRecognition))
            {
                allOk = false;
                usrGrid.HighlightDiff(symbolDictionary, expected);
                continue;
            }

            if (got != expected)
            {
                allOk = false;
                usrGrid.HighlightDiff(symbolDictionary, expected); // подсветим, где не сходится с эталоном
            }
        }

        if (!requireSameLength && usrs.Count < refs.Count) allOk = false;

        Debug.Log(allOk ? "[CodeOrderJudge] OK — порядок и символы совпали."
                        : "[CodeOrderJudge] FAIL — есть несовпадения.");
    }

    public void CheckButton() => Check();
}