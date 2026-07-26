using System;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;

public class InteractHackCode :
    Interactable,
    ICodeSuccessReceiver,
    IRunPersistentStateProvider
{
    private const string PersistentStateTypeId = "code-hack-minigame";
    private const int PersistentStateVersion = 1;

    [Header("Мини-игра")]
    [SerializeField] private CodePatternSO pattern;          // что показывать в UI
    [SerializeField] private CodeOrderJudge judge;           // судья мини-игры (в UI)
    [SerializeField] private ReferenceRowBuilder target;     // куда построить эталон

    [Header("Визуал")]
    [SerializeField] private Color onColor = new Color(1f, 0.4f, 0f, 1f);
    [SerializeField] private Color baseColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    private Renderer _renderer;
    private bool isSolved = false;

    public string StateTypeId => PersistentStateTypeId;
    public int StateVersion => PersistentStateVersion;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        ApplyPersistentPresentation();
    }

    private void Start()
    {
        // Если target/judge не заданы в инспекторе — попробуем найти рядом (не обязательно)
        if (target == null)
            target = GetComponentInChildren<ReferenceRowBuilder>(true);
    }

    protected override void Interact()
    {
        if (isSolved) return;

        if (judge == null)
        {
            Debug.LogError($"[{name}] Judge (CodeOrderJudge) не назначен.");
            return;
        }
        if (target == null)
        {
            Debug.LogError($"[{name}] ReferenceRowBuilder (target) не назначен.");
            return;
        }

        // 1) Пробрасываем нужный паттерн в референс-строку и пересобираем
        if (pattern != null)
        {
            target.pattern = pattern;
            target.RebuildRuntime();
        }

        // 2) Назначаем ЭТУ консоль адресатом результата и открываем UI
        judge.SetExternalReceiver(this);  // ← важно: адресуем именно текущую консоль
        judge.OpenGame();
    }

    /// <summary>
    /// Колбэк из мини-игры при корректном вводе.
    /// </summary>
    public void OnCodeAccepted()
    {
        if (isSolved) return;

        SetSolved();
        judge.CloseGame();      // закрыть UI после успеха (если требуется)
        // очищаем адресата:
         judge.SetExternalReceiver(null);
    }

    public string CapturePersistentState()
    {
        HackMinigamePersistentState state =
            new HackMinigamePersistentState
            {
                isSolved = isSolved
            };

        return JsonUtility.ToJson(state);
    }

    public void RestorePersistentState(
        string serializedState,
        int stateVersion)
    {
        if (stateVersion != PersistentStateVersion)
        {
            throw new InvalidOperationException(
                $"Code hack minigame state version {stateVersion} is not supported. " +
                $"Expected {PersistentStateVersion}.");
        }

        if (string.IsNullOrWhiteSpace(serializedState))
        {
            throw new InvalidOperationException(
                "Saved code hack minigame state is empty.");
        }

        HackMinigamePersistentState state =
            JsonUtility.FromJson<HackMinigamePersistentState>(
                serializedState);

        if (state == null)
        {
            throw new InvalidOperationException(
                "Saved code hack minigame state is invalid.");
        }

        isSolved = state.isSolved;
        ApplyPersistentPresentation();
    }

    private void SetSolved()
    {
        isSolved = true;
        ApplyPersistentPresentation();
    }

    private void ApplyPersistentPresentation()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();

        if (_renderer != null)
            _renderer.material.color = isSolved ? onColor : baseColor;
    }

    [Serializable]
    private sealed class HackMinigamePersistentState
    {
        public bool isSolved;
    }
}
