using UnityEngine;
using UnityEngine.AI;

public class DebugManager : MonoBehaviour
{
    [SerializeField] private bool CursorOn;
    [SerializeField] private GameObject[] Enemies;

    private Rect windowRect = new Rect(100, 100, 300, 400);
    private bool showWindow = true;
    private Vector2 scrollPos;
    [SerializeField] private bool showNavMeshPath = true;
    [SerializeField] private Color pathColor = Color.cyan;

    private void Start()
    {
        Enemies = GameObject.FindGameObjectsWithTag("Enemy");
        windowRect.x = Screen.width - windowRect.width - 10; // начальная позиция справа
        windowRect.y = 10;
    }

    private void LateUpdate()
    {
        Cursor.visible = CursorOn;
        Cursor.lockState = CursorOn ? CursorLockMode.None : CursorLockMode.Locked;
        
        
    }

    private void OnGUI()
    {
        if (Enemies == null || Enemies.Length == 0)
            return;

        if (showWindow)
        {
            windowRect = GUI.Window(0, windowRect, DrawDebugWindow, "Enemy Debug");
        }
        else
        {
            // Кнопка открытия
            if (GUI.Button(new Rect(Screen.width - 110, 10, 100, 30), "Show Debug"))
            {
                showWindow = true;
            }
        }
    }
    private void OnDrawGizmos()
    {
        if (!showNavMeshPath || Enemies == null) return;

        Gizmos.color = pathColor;

        foreach (var enemy in Enemies)
        {
            if (enemy == null) continue;

            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null || agent.path == null || agent.path.corners.Length < 2)
                continue;

            var corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }
    }
    private void DrawDebugWindow(int windowID)
    {
        // Кнопка скрытия окна
        if (GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "−"))
        {
            showWindow = false;
            return;
        }

        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(windowRect.height - 40));

        for (int i = 0; i < Enemies.Length; i++)
        {
            var enemyGO = Enemies[i];
            if (enemyGO == null) continue;

            Vector3 pos = enemyGO.transform.position;
            var enemy = enemyGO.GetComponent<Enemy>();
            var NavMesh = enemyGO.GetComponent<NavMeshAgent>();
            var awarness = enemy._awarenessMeter.AlertnessValue;
            string stateName = enemy?.currentState?.ToString() ?? "Unknown";
            enemy.DebugEnemy();
            
            GUILayout.Label($"[{i}] {enemyGO.name}");
            GUILayout.Label($"Pos: {pos}");
            showNavMeshPath = GUILayout.Toggle(showNavMeshPath, "Показать путь NavMeshAgent");
            GUILayout.Label($"Speed: {NavMesh.velocity}");
            GUILayout.Label($"State: {stateName}");
            GUILayout.Label($"Степень настороженности: {awarness} ");
            GUILayout.Label($"Противник видит игрока: {enemy.seePlayer}");
            GUILayout.Label($"Противник слышит игрока:{enemy.hearPlayer}");
            GUILayout.Space(10);
            
        }

        GUILayout.EndScrollView();

        // Перетаскивание окна
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 25));
    }
}
