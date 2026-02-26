using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq; // Нужно для удобной работы со списками

public class DebugManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool CursorOn;
    [SerializeField] private bool showNavMeshPath = true;
    [SerializeField] private Color pathColor = Color.cyan;

    // Храним сразу компоненты Enemy, чтобы не искать их каждый кадр
    [Header("Data")]
    [SerializeField] private List<Enemy> Enemies = new List<Enemy>();

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
        // Управление курсором (показываем если окно открыто или если включена галочка)
        bool showCursor = CursorOn || showWindow;
        if (Cursor.visible != showCursor)
        {
            Cursor.visible = showCursor;
            Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    public void RefreshEnemies()
    {
        Enemies.Clear();
        
        // Находим все объекты с тегом Enemy
        GameObject[] allTagged = GameObject.FindGameObjectsWithTag("Enemy");
        
        // Используем HashSet чтобы избежать дубликатов (если тег висит и на родителе, и на детях)
        HashSet<Enemy> uniqueEnemies = new HashSet<Enemy>();

        foreach (var obj in allTagged)
        {
            Enemy e = obj.GetComponentInParent<Enemy>();
            if (e != null)
            {
                uniqueEnemies.Add(e);
            }
        }

        Enemies = uniqueEnemies.ToList();
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
        if (GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "×"))
        {
            showWindow = false;
        }

        GUILayout.BeginVertical();
        
        showNavMeshPath = GUILayout.Toggle(showNavMeshPath, " Отображать пути NavMesh");
        CursorOn = GUILayout.Toggle(CursorOn, " Force Cursor Visible");

        if (GUILayout.Button("Обновить список врагов"))
        {
            RefreshEnemies();
        }

        GUILayout.Space(5);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        GUILayout.Space(5);

        if (Enemies == null || Enemies.Count == 0)
        {
            GUILayout.Label("Враги не найдены (проверьте тег 'Enemy').");
        }
        else
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            // Обратный цикл безопаснее при удалении, но здесь просто foreach
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy enemy = Enemies[i];

                // Если враг был уничтожен в игре, удаляем из списка
                if (enemy == null) 
                {
                    Enemies.RemoveAt(i);
                    i--;
                    continue;
                }

                NavMeshAgent nav = enemy.GetComponent<NavMeshAgent>();

                // --- ОТРИСОВКА ИНФО ---
                GUI.color = Color.yellow;
                GUILayout.Label($"[{i}] {enemy.name}");
                GUI.color = Color.white;

                // Используем null-conditional оператор (?.) для безопасности
                GUILayout.Label($"State: {enemy.currentState?.GetType().Name ?? "None"}");
                
                if (enemy.Health != null)
                    GUILayout.Label($"HP: {enemy.Health.CurrentHealth}/{enemy.Health.MaxHealth}");

                // ИСПОЛЬЗУЕМ НОВЫЕ СВОЙСТВА ИЗ ENEMY.CS
                GUILayout.Label($"Alertness: {enemy.DebugAlertness:F1}%");
                GUILayout.Label($"See: {(enemy.DebugCanSeePlayer ? "YES" : "no")}");
                GUILayout.Label($"Hear: {(enemy.DebugCanHearPlayer ? "YES" : "no")}");

                if (nav != null)
                    GUILayout.Label($"Speed: {nav.velocity.magnitude:F2} / {nav.speed:F1}");

                GUILayout.Space(10);
            }

            GUILayout.EndScrollView();
        }

        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 25));
    }

    private void OnDrawGizmos()
    {
        if (!showNavMeshPath || Enemies == null) return;

        Gizmos.color = pathColor;
        foreach (var enemy in Enemies)
        {
            if (enemy == null) continue;

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.hasPath) continue;

            Vector3[] corners = agent.path.corners;
            if (corners.Length < 2) continue;

            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
                Gizmos.DrawSphere(corners[i + 1], 0.2f); // Рисуем точки поворота
            }
            
            // Рисуем линию к цели назначения
            Gizmos.DrawWireSphere(agent.destination, 0.5f);
        }
    }
}