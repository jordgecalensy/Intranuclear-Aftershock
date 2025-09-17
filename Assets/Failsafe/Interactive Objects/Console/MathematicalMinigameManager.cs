using TMPro;
using UnityEngine;
using System;
using Tayx.Graphy.Utils.NumString;
using static UnityEngine.Rendering.HDROutputUtils;

public enum MathematicalVariations {variant_1, variant_2, variant_3}

public class MathematicalMinigameManager : MonoBehaviour
{
    private string[] _operations = { "+", "-" };

    private int _resultCalculation;
    private int _aCalculation;
    private int _bCalculation;
    private int _cellsCount = 0;

    [Header("Variation of the mini-game")]
    [SerializeField] private MathematicalVariations _mathematicalVariation;

    [Header("Ñompared Number Settings")]
    [SerializeField] private string _textWithÑomparedNumber;
    [SerializeField] private int _comparedNumber;

    private int comparedNumber;

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
    [SerializeField] private TextMeshProUGUI _comparedNumberText;
    [SerializeField] private TextMeshProUGUI _calculationText;
    [SerializeField] private TextMeshProUGUI[] _passwordCells = new TextMeshProUGUI[6];

    [Header("Window Settings")]
    [SerializeField] private GameObject _unlockingWindow;
    [SerializeField] private GameObject _minigameWindow;

    private void OnEnable()
    {
        CreateNewGame();
    }
    private void Update()
    {
        if (!_timerRunning) return;

        time -= Time.deltaTime;
        Debug.Log(time.ToInt());

        if (time <= 0f)
            TimeIsOut();
    }
    public void Ñomparison(bool moreNumber)
    {
        if (!_timerRunning) return;
        if (moreNumber)
        {
            if(comparedNumber > _resultCalculation)
            {
                Debug.Log($"true {comparedNumber} > {_resultCalculation}");
                FillingCell();
                ComparedNumber();
            }
            else
            {
                Debug.Log($"false {comparedNumber} < {_resultCalculation}");
                time -= _fimeTime;
            }
        }
        else
        {
            if (comparedNumber < _resultCalculation)
            {
                Debug.Log($"true {comparedNumber} < {_resultCalculation}");
                FillingCell();
                ComparedNumber();
            }
            else
            {
                Debug.Log($"false {comparedNumber} > {_resultCalculation}");
                time -= _fimeTime;
            }
        }
        GeneratingCalculation();
    }
    private void ComparedNumber()
    {
        if (_mathematicalVariation == MathematicalVariations.variant_3)
            if(_cellsCount != 0)
                comparedNumber = _resultCalculation;
        _comparedNumberText.text = _textWithÑomparedNumber + " " + comparedNumber;
    }
    private void GeneratingCalculation()
    {
        System.Random random = new System.Random();
        _resultCalculation = random.Next(_minNumber, _maxNumber);
        if (_resultCalculation == comparedNumber) 
            _resultCalculation++;
        switch (_mathematicalVariation)
        {
            case MathematicalVariations.variant_1:
            {
                _calculationText.text = $"{_resultCalculation}";
                break;
            }
            case MathematicalVariations.variant_2:
            case MathematicalVariations.variant_3:
            {
                string operation = _operations[random.Next(_operations.Length)];
                switch (operation)
                {
                    case "+":
                        _aCalculation = random.Next(_minNumber, _resultCalculation);
                        _bCalculation = _resultCalculation - _aCalculation;
                        break;
                    case "-":
                        _aCalculation = random.Next(_minNumber, _maxNumber - _resultCalculation);
                        _bCalculation = _resultCalculation + _aCalculation;
                        break;
                }
                _calculationText.text = $"{_bCalculation} {operation} {_aCalculation}";
                break;
            }
        }
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
        Debug.Log("Òàéìåð çàâåðøåí!");
        Debug.Log("Âûïîëíÿåòñÿ äåéñòâèå ïîñëå òàéìåðà");
        CreateNewGame();
    }
    private void CreateNewGame()
    {
        comparedNumber = _comparedNumber;
        _cellsCount = 0;
        ClearCells();
        ComparedNumber();
        GeneratingCalculation();
        time = _time;
        _timerRunning = true;
    }
    private void ClearCells()
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
