using System;
using UnityEngine;

public class InteractHackCode : Interactable, ICodeSuccessReceiver
{
    [Header("Мини-игра")]
    [SerializeField] private CodePatternSO pattern;          // что показывать в UI
    [SerializeField] private CodeOrderJudge judge;           // судья мини-игры (в UI)
    [SerializeField] private ReferenceRowBuilder target;     // куда построить эталон

    [Header("Визуал")]
    [SerializeField] private Color onColor = new Color(1f, 0.4f, 0f, 1f);
    [SerializeField] private Color baseColor = new Color(1f, 0.2f, 0.2f, 0.6f);

    private Renderer _renderer;
    private bool isSolved = false;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _renderer.material.color = baseColor;
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

        ChangeColor();
        judge.CloseGame();      // закрыть UI после успеха (если требуется)
        // очищаем адресата:
         judge.SetExternalReceiver(null);
    }

    private void ChangeColor()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();

        if (_renderer != null)
            _renderer.material.color = onColor;

        isSolved = true;
    }
}