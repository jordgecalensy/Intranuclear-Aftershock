using System;
using System.Collections.Generic;
using UnityEngine;

public class ConsoleJsonInstaller : MonoBehaviour
{
    [Header("Where to spawn dropdowns")]
    [SerializeField] private Transform _parent; // например Content твоего ScrollView

    [Header("Prefab")]
    [SerializeField] private TMPDropdownRoot _dropdownPrefab;

    [Header("Json files (TextAssets)")]
    [SerializeField] private List<TextAsset> _jsonFiles = new();

    [Header("Options")]
    [SerializeField] private bool _clearParentBeforeSpawn = false;

    private void Start()
    {
        if (_parent == null) _parent = transform;

        if (_dropdownPrefab == null)
        {
            Debug.LogError("ConsoleJsonInstaller: Dropdown prefab is not set!");
            return;
        }

        if (_clearParentBeforeSpawn)
            ClearParent();

        SpawnAll();
    }

    private void ClearParent()
    {
        for (int i = _parent.childCount - 1; i >= 0; i--)
            Destroy(_parent.GetChild(i).gameObject);
    }

    private void SpawnAll()
    {
        foreach (var file in _jsonFiles)
        {
            if (file == null) continue;

            ConsoleEntryModel model;
            try
            {
                model = JsonUtility.FromJson<ConsoleEntryModel>(file.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"ConsoleJsonInstaller: Failed to parse json '{file.name}'. {e}");
                continue;
            }

            if (model == null)
            {
                Debug.LogError($"ConsoleJsonInstaller: Json '{file.name}' parsed as null.");
                continue;
            }

            var dropdown = Instantiate(_dropdownPrefab, _parent);
            dropdown.name = $"Dropdown_{(string.IsNullOrEmpty(model.id) ? file.name : model.id)}";

            // Заполняем UI
            dropdown.SetHeader(model.data, model.summary);
            dropdown.SetBodyText(model.body);

            // Важно: после установки текста форсим пересчёт высот
            dropdown.RebuildNow();
        }
    }
}
