using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class NavMeshAgentIDFinder
{
    // Эта строчка создает новую кнопку в верхнем меню Unity
    [MenuItem("Tools/Print NavMesh Agent IDs")]
    public static void PrintIDs()
    {
        int count = NavMesh.GetSettingsCount();
        
        if (count == 0)
        {
            Debug.LogWarning("В проекте нет настроенных NavMesh агентов!");
            return;
        }

        Debug.Log($"--- Найдено NavMesh агентов: {count} ---");

        // Перебираем всех существующих агентов и выводим их данные
        for (int i = 0; i < count; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            string agentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
            
            Debug.Log($"Имя агента: <b>{agentName}</b> | Его ID: <b>{settings.agentTypeID}</b>");
        }
    }
}