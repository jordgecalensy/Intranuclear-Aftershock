using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class DebugManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool CursorOn;
    [SerializeField] private bool showNavMeshPath = true;
    [SerializeField] private Color pathColor = Color.cyan;

    [Header("Data")]
    [SerializeField] private GameObject[] Enemies;

    private Rect windowRect = new Rect(100, 100, 350, 500);
    private bool showWindow = true;
    private Vector2 scrollPos;

    private void Start()
    {
        RefreshEnemies();
        windowRect.x = Screen.width - windowRect.width - 10;
        windowRect.y = 10;
    }

    private void LateUpdate()
    {
        // Управление курсором
        Cursor.visible = CursorOn;
        Cursor.lockState = CursorOn ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // Метод для поиска только уникальных родительских объектов с компонентом Enemy
    public void RefreshEnemies()
    {
        GameObject[] allTagged = GameObject.FindGameObjectsWithTag("Enemy");
        HashSet<GameObject> uniqueParents = new HashSet<GameObject>();

        foreach (var obj in allTagged)
        {
            // Ищем компонент Enemy в объекте или его родителях
            Enemy e = obj.GetComponentInParent<Enemy>();
            if (e != null)
            {
                uniqueParents.Add(e.gameObject);
            }
        }

        Enemies = new GameObject[uniqueParents.Count];
        uniqueParents.CopyTo(Enemies);
    }

    private void OnGUI()
    {
        if (showWindow)
        {
            windowRect = GUI.Window(0, windowRect, DrawDebugWindow, "Enemy Debugger");
        }
        else
        {
            if (GUI.Button(new Rect(Screen.width - 110, 10, 100, 30), "Show Debug"))
            {
                showWindow = true;
            }
        }
    }

    private void DrawDebugWindow(int windowID)
    {
        // Кнопка закрытия
        if (GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "×"))
        {
            showWindow = false;
        }

        GUILayout.BeginVertical();
        
        // Общие настройки дебага
        showNavMeshPath = GUILayout.Toggle(showNavMeshPath, " Отображать пути NavMesh");
        
        if (GUILayout.Button("Обновить список врагов"))
        {
            RefreshEnemies();
        }

        GUILayout.Space(5);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1)); // Разделитель
        GUILayout.Space(5);

        if (Enemies == null || Enemies.Length == 0)
        {
            GUILayout.Label("Враги с тегом 'Enemy' не найдены.");
        }
        else
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < Enemies.Length; i++)
            {
                GameObject enemyGO = Enemies[i];
                if (enemyGO == null) continue;

                Enemy enemy = enemyGO.GetComponent<Enemy>();
                NavMeshAgent nav = enemyGO.GetComponent<NavMeshAgent>();

                if (enemy == null) continue;

                // Секция врага
                GUI.color = Color.yellow;
                GUILayout.Label($"[{i}] {enemyGO.name}");
                GUI.color = Color.white;

                GUILayout.Label($"Состояние: {enemy.currentState?.ToString() ?? "N/A"}");
                
                if (enemy.Health != null)
                    GUILayout.Label($"Здоровье: {enemy.Health.CurrentHealth}");

                if (enemy._awarenessMeter != null)
                    GUILayout.Label($"Настороженность: {enemy._awarenessMeter.AlertnessValue:F2}");

                GUILayout.Label($"Видит игрока: {(enemy.seePlayer ? "ДА" : "нет")}");
                GUILayout.Label($"Слышит игрока: {(enemy.hearPlayer ? "ДА" : "нет")}");

                if (nav != null)
                    GUILayout.Label($"Скорость: {nav.velocity.magnitude:F2} м/с");

                // Вызов вашего метода дебага внутри класса Enemy
                enemy.DebugEnemy();

                GUILayout.Space(10);
            }

            GUILayout.EndScrollView();
        }

        GUILayout.EndVertical();

        // Позволяет перетаскивать окно за заголовок
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 25));
    }

    private void OnDrawGizmos()
    {
        if (!showNavMeshPath || Enemies == null) return;

        Gizmos.color = pathColor;
        foreach (var enemyGO in Enemies)
        {
            if (enemyGO == null) continue;

            NavMeshAgent agent = enemyGO.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.hasPath) continue;

            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
                Gizmos.DrawSphere(corners[i], 0.1f);
            }
        }
    }
}