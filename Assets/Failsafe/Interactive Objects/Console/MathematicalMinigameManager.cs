using TMPro;
using UnityEngine;
using System;
using Tayx.Graphy.Utils.NumString;

public class MathematicalMinigameManager : MonoBehaviour
{
    private string[] _operations = { "+", "-" };

    private int _resultCalculation;
    private int _aCalculation;
    private int _bCalculation;
    private int _cellsCount = 0;

    [Header("Base Number Settings")]
    [SerializeField] private int _baseNumber;
    [SerializeField] private string _textWithBaseNumber;

    [Header("Numerical Range")]
    [SerializeField] private int _minNumber;
    [SerializeField] private int _maxNumber;

    [Header("Timer")]
    [SerializeField] private GameObject _timer;
    [SerializeField] private int _time;
    [SerializeField] private int _fimeTime;

    private float time;
    private bool _timerRunning = false;

    [Header("Text Settings")]
    [SerializeField] private TextMeshProUGUI _baseNumberText;
    [SerializeField] private TextMeshProUGUI _calculationText;
    [SerializeField] private TextMeshProUGUI[] _passwordCells = new TextMeshProUGUI[6];

    [Header("Window Settings")]
    [SerializeField] private GameObject _unlockingWindow;
    [SerializeField] private GameObject _minigameWindow;

    private void OnEnable()
    {
        time = _time;
        _timerRunning = true;
        _baseNumberText.text = _textWithBaseNumber + " " + _baseNumber;
        _cellsCount = 0;
        ClearcCells();
        GeneratingCalculation();
    }
    private void Update()
    {
        if (!_timerRunning) return;

        time -= Time.deltaTime;
        Debug.Log(time.ToInt());

        if (time <= 0f)
            TimeIsOut();
    }
    public void Сomparison(bool moreNumber)
    {
        if (!_timerRunning) return;
        if (moreNumber)
        {
            if(_baseNumber > _resultCalculation)
            {
                Debug.Log($"true {_baseNumber} > {_resultCalculation}");
                FillingCell();
            }
            else
            {
                Debug.Log($"false {_baseNumber} < {_resultCalculation}");
                time -= _fimeTime;
            }
        }
        else
        {
            if (_baseNumber < _resultCalculation)
            {
                Debug.Log($"true {_baseNumber} < {_resultCalculation}");
                FillingCell();
            }
            else
            {
                Debug.Log($"false {_baseNumber} > {_resultCalculation}");
                time -= _fimeTime;
            }
        }
        GeneratingCalculation();
    }
    private void GeneratingCalculation()
    {
        System.Random random = new System.Random();
        _resultCalculation = random.Next(_minNumber, _maxNumber);
        if (_resultCalculation == _baseNumber) 
            _resultCalculation++;
        _aCalculation = random.Next(_minNumber, _resultCalculation);

        string operation = _operations[random.Next(_operations.Length)];
        switch (operation)
        {
            case "+":
                _bCalculation = _resultCalculation - _aCalculation;
                break;
            case "-":
                _bCalculation = _resultCalculation + _aCalculation;
                break;
        }
        _calculationText.text = $"{_bCalculation} {operation} {_aCalculation}";
    }
    private void FillingCell()
    {
        if (_cellsCount == _passwordCells.Length) return;
        Debug.Log("Cell filled in " + _cellsCount);
        _passwordCells[_cellsCount].text = _resultCalculation.ToString();
        _cellsCount++;
        if (_cellsCount == _passwordCells.Length)
            UnlockConsole();
    }
    private void TimeIsOut()
    {
        _timerRunning = false;
        Debug.Log("Таймер завершен!");
        Debug.Log("Выполняется действие после таймера");
        Restart();
    }
    private void Restart()
    {
        time = _time;
        _timerRunning = true;
        _cellsCount = 0;
        ClearcCells();
        GeneratingCalculation();
        Debug.Log("Restart");
    }
    private void ClearcCells()
    {
        foreach(TextMeshProUGUI passwordCell in _passwordCells)
            passwordCell.text = "";
    }
    private void UnlockConsole()
    {
        Debug.Log("Unlock");
        _timerRunning = false;
        _unlockingWindow.SetActive(true);
        _minigameWindow.SetActive(false);
    }
}
