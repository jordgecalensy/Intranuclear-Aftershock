using UnityEngine;

using System;
using UnityEngine;

public class CodeOrderJudge : MonoBehaviour
{
    [Header("Ссылки")]
    public SlotsRow referenceRow;
    public SlotsRow playerRow;
    public SymbolDictionary symbolDictionary;

    [Header("Опции")]
    public bool requireSameLength = true;
    public bool allowNearestRecognition = false;

    // 👉 Получатель, заданный при открытии UI
    private ICodeSuccessReceiver _externalReceiver;

    private void Start()
    {
        CloseGame();
    }

    // вызвать при открытии мини-игры для конкретной консоли
    public void SetExternalReceiver(ICodeSuccessReceiver receiver)
    {
        _externalReceiver = receiver;
    }

    // (опц.) очистить при закрытии окна
    public void ClearExternalReceiver()
    {
        _externalReceiver = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseGame();
        }
    }

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
            foreach (var g in usrs) g?.HighlightDiff(symbolDictionary, g.expectedSymbol);
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
            if (expected == '\0' && !symbolDictionary.TryRecognize(refGrid.GetMask(), out expected, false))
            {
                allOk = false;
                continue;
            }

            ushort userMask = usrGrid.GetMask();
            if (!symbolDictionary.TryRecognize(userMask, out var got, allowNearestRecognition) || got != expected)
            {
                allOk = false;
                usrGrid.HighlightDiff(symbolDictionary, expected);
            }
        }

        if (!requireSameLength && usrs.Count < refs.Count) allOk = false;

        if (allOk)
        {
            // 👉 Триггерим только текущего адресата (эту конкретную консоль)
            _externalReceiver?.OnCodeAccepted();
        }
    }

    public void CheckButton() => Check();

    private void ToggleGame(bool isOpen)
    {
        this.gameObject.SetActive(isOpen);
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    
        if (isOpen && playerRow != null)
            playerRow.ClearAll();
    }

    public void OpenGame()  => ToggleGame(true);
    public void CloseGame() => ToggleGame(false);
}