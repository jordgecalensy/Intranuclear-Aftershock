#pragma warning disable IDE1006
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

[Serializable] public class MessageDatabaseJson { public List<MessageSectionJson> sections = new(); }
[Serializable] public class MessageSectionJson { public string id; public string title; public List<MessageEntryJson> entries = new(); }
[Serializable] public class MessageEntryJson { public string id; public string date; public string subject; public string body; }
#pragma warning restore IDE1006

public class AlertsListSpawner : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Transform contentRoot;      // Content
    [SerializeField] private GameObject itemPrefab;      // MessageItem prefab

    [Header("Config")]
    [SerializeField] private string sectionId = "alerts"; // какую секцию показываем

    private void Start()
    {
        Spawn();
    }

    private void Spawn()
    {
        // 1) прочитать JSON
        var path = Path.Combine(Application.streamingAssetsPath, "messages.json");
        var json = File.ReadAllText(path);
        var db = JsonUtility.FromJson<MessageDatabaseJson>(json);

        var section = db.sections.Find(s => s.id == sectionId);
        if (section == null)
        {
            Debug.LogError($"Section '{sectionId}' not found in messages.json");
            return;
        }

        // 2) очистить старые элементы
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        // 3) создать элементы
        foreach (var entry in section.entries)
        {
            var go = Instantiate(itemPrefab, contentRoot);

            // ищем TMP_Text внутри префаба (если он на корне — тоже найдётся)
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = $"{entry.date}  —  {entry.subject}";
        }
    }
}
