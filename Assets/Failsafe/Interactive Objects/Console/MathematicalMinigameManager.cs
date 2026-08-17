using TMPro;
using UnityEngine;
using System;
using Failsafe.Scripts.SaveSystem;
using Tayx.Graphy.Utils.NumString;
using UnityEngine.UI;
using System.Collections;

public enum MathematicalVariations {variant_1, variant_2, variant_3}

public class MathematicalMinigameManager :
    MonoBehaviour,
    IRunPersistentStateProvider
{
    private const string PersistentStateTypeId = "mathematical-minigame";
    private const int PersistentStateVersion = 1;

    private string[] _operations = { "+", "-" };

    private int _resultCalculation;
    private int _aCalculation;
    private int _bCalculation;
    private int _cellsCount = 0;

    [Header("Variation of the mini-game")]
    [SerializeField] private MathematicalVariations _mathematicalVariation;

    [Header("�ompared Number Settings")]
    [SerializeField] private string _textWithСomparedNumber;
    [SerializeField] private int _comparedNumber;

    private int comparedNumber;

    [Header("Numerical Range")]
    [SerializeField] private int _minNumber;
    [SerializeField] private int _maxNumber;

    [Header("Timer")]
    [SerializeField] private Image _timerImage;
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private int _time;
    [SerializeField] private int _fimeTime;

    private float time;
    private bool _timerRunning = false;
    private bool _isSolved;

    [Header("Text Settings")]
    [SerializeField] private TextMeshProUGUI _comparedNumberText;
    [SerializeField] private TextMeshProUGUI _calculationText;

    [Header("Password Cells Settings")]
    [SerializeField] private Image[] _passwordCells = new Image[6];
    [SerializeField] private Image _lockOn;
    [SerializeField] private Image _lockOff;

    [Header("Window Settings")]
    [SerializeField] private float _timeBeforeCloseWindow = 1f;
    [SerializeField] private GameObject _openWindow;
    [SerializeField] private GameObject _mainWindow;
    [SerializeField] private GameObject _minigameWindow;
    [SerializeField] private GameObject _failedWindow;

    public string StateTypeId => PersistentStateTypeId;
    public int StateVersion => PersistentStateVersion;

    private void OnEnable()
    {
        if (_isSolved)
        {
            ApplySolvedState();
            return;
        }

        CreateNewGame();
    }
    private void Update()
    {
        if (!_timerRunning) return;

        time -= Time.deltaTime;
        float fill = Mathf.Clamp01(time / _time);
        _timerImage.fillAmount = fill;
        _timerText.text = Mathf.CeilToInt(time).ToString();

        if (time <= 0f)
        {
            time = 0f;
            TimeIsOut();
        }
    }
    public void Сomparison(bool moreNumber)
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
        _comparedNumberText.text = _textWithСomparedNumber + " " + comparedNumber;
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
                _calculationText.text = $"{_bCalculation}{operation}{_aCalculation}";
                break;
            }
        }
    }

    private void Lock(bool isLockOn)
    {
        _lockOn.enabled = !isLockOn;
        _lockOff.enabled = isLockOn;
    }
    private void FillingCell()
    {
        if (_cellsCount == _passwordCells.Length) return;
        Debug.Log("Cell filled in " + _cellsCount);
        // _passwordCells[_cellsCount].text = _resultCalculation.ToString();
        _passwordCells[_cellsCount].enabled = true;
        _cellsCount++;
        if (_cellsCount == _passwordCells.Length)
            UnlockConsole();
    }
    private void TimeIsOut()
    {
        _timerRunning = false;
        _timerText.text = "0";
        Debug.Log("Time is out!");
        Debug.Log("Game over");
        StartCoroutine(GameOver(false));
        // CreateNewGame();
    }
    private void CreateNewGame()
    {
        comparedNumber = _comparedNumber;
        _cellsCount = 0;
        ClearCells();
        ComparedNumber();
        GeneratingCalculation();
        _timerImage.fillAmount = 1f;
        time = _time;
        _timerText.text = Mathf.CeilToInt(time).ToString();
        Lock(true);
        _timerRunning = true;
    }
    private void ClearCells()
    {
        foreach(Image passwordCell in _passwordCells)
            passwordCell.enabled = false;
    }
    private void UnlockConsole()
    {
        Debug.Log("Unlock");
        _isSolved = true;
        _timerRunning = false;
        Lock(false);
        StartCoroutine(GameOver(true));
    }

    public string CapturePersistentState()
    {
        MathematicalMinigamePersistentState state =
            new MathematicalMinigamePersistentState
            {
                isSolved = _isSolved
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
                $"Mathematical minigame state version {stateVersion} is not supported. " +
                $"Expected {PersistentStateVersion}.");
        }

        if (string.IsNullOrWhiteSpace(serializedState))
        {
            throw new InvalidOperationException(
                "Saved mathematical minigame state is empty.");
        }

        MathematicalMinigamePersistentState state =
            JsonUtility.FromJson<MathematicalMinigamePersistentState>(
                serializedState);

        if (state == null)
        {
            throw new InvalidOperationException(
                "Saved mathematical minigame state is invalid.");
        }

        StopAllCoroutines();
        _isSolved = state.isSolved;

        if (_isSolved)
        {
            ApplySolvedState();
            return;
        }

        ApplyUnsolvedState();
    }

    private void ApplySolvedState()
    {
        _timerRunning = false;
        Lock(false);

        if (_failedWindow != null)
            _failedWindow.SetActive(false);

        if (_mainWindow != null)
            _mainWindow.SetActive(true);

        if (_minigameWindow != null)
            _minigameWindow.SetActive(false);
    }

    private void ApplyUnsolvedState()
    {
        if (_failedWindow != null)
            _failedWindow.SetActive(false);

        CreateNewGame();
    }

    IEnumerator GameOver(bool win)
    {
        yield return new WaitForSeconds(_timeBeforeCloseWindow);
        if (win)
        {
            _mainWindow.SetActive(true);
            _minigameWindow.SetActive(false);
        }
        else
        {
            _failedWindow.SetActive(true);
            _minigameWindow.SetActive(false);
        }
    }

    [Serializable]
    private sealed class MathematicalMinigamePersistentState
    {
        public bool isSolved;
    }
}
