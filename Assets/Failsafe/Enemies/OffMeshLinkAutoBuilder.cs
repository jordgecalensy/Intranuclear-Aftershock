using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Failsafe.Obstacles;
using Unity.AI.Navigation;

/// <summary>
/// Генерирует NavMeshLink для подъема, используя существующие в сцене объекты Ledge.
/// Находит все компоненты Ledge, берет их активные края и создает идеально ровные линки.
/// </summary>
[ExecuteAlways]
public class OffMeshLinkAutoBuilder : MonoBehaviour
{
    [Header("Настройки Поиска")]
    [Tooltip("Слой, на котором находится геометрия пола (нижний уровень).")]
    [SerializeField]
    private LayerMask floorMask = 1;

    [Tooltip("Максимальное расстояние от края уступа вниз до пола.")]
    [SerializeField]
    private float maxClimbHeight = 5.0f;

    [Tooltip("Минимальное расстояние от края уступа вниз до пола.")]
    [SerializeField] private float minClimbHeight = 0.5f;

    [Tooltip("Расстояние между создаваемыми линками на одном уступе.")]
    [SerializeField] private float linkSpacing = 2.0f;

    [Tooltip("Насколько близко могут быть конечные точки разных линков.")]
    [SerializeField] private float linkProximity = 1.0f;
    
    [Tooltip("ЛОКАЛЬНОЕ смещение входа (снизу). Z: вперед/назад от стены. Y: вверх/вниз.")]
    [SerializeField] private Vector3 startPointOffset = new Vector3(0f, 0.05f, 0f);
    
    [Tooltip("ЛОКАЛЬНОЕ смещение выхода (сверху). Z: вперед/назад от стены. Y: вверх/вниз.")]
    [SerializeField] private Vector3 endPointOffset = new Vector3(0f, 0.05f, 0f);
    
    [Tooltip("Вертикальный сдвиг точки, откуда пускается raycast вниз.")]
    [SerializeField] private float topRaycastVerticalOffset = 0.25f;

    [Header("Параметры NavMeshLink")]
    [SerializeField]
    private float linkWidth = 0.8f;

    [Tooltip("Индекс зоны NavMesh, который будет назначен линку.")]
    [SerializeField] private int linkAreaIndex = 3; 
    
    [Tooltip("Если включено, скрипт автоматически возьмет ID первого (стандартного) агента в проекте.")]
    [SerializeField] private bool autoFindDefaultAgent = true;
    
    [Tooltip("Используется только если галочка автопоиска снята.")]
    [SerializeField] private int customAgentTypeID = 0;
    
    private int _finalAgentID;

    [SerializeField] private int area = 0; 
    [SerializeField] private float costModifier = 1.0f;
    
    [Tooltip("Базовый радиус поиска NavMesh вокруг точки.")]
    [SerializeField] private float navMeshSampleRadius = 1.0f;
    
    [Tooltip("Дополнительный радиус fallback-поиска NavMesh.")]
    [SerializeField] private float navMeshFallbackRadius = 2.0f;
    
    [Tooltip("Количество угловых проб вокруг точки для fallback-поиска.")]
    [SerializeField] private int navMeshFallbackRays = 8;

    [Tooltip("ЗАЩИТА ОТ ДАЛЕКИХ СТЕН: Максимально допустимое отклонение найденной точки NavMesh от оригинала.")]
    [SerializeField] private float maxSnapDrift = 1.5f;

    [Header("Отладка")]
    [Tooltip("Включить отображение гизмо для созданных линков.")]
    [SerializeField] private bool visualizeProcess = true;
    
    [Tooltip("Включить РАСШИРЕННУЮ отладку. Показывает лучи и точки проверки.")]
    [SerializeField] private bool enableDeepDebug = false;

    private readonly List<NavMeshLink> _createdLinks = new List<NavMeshLink>();
#if UNITY_EDITOR
    private readonly List<System.Tuple<Vector3, Vector3>> _debug_createdLinkPoints = new List<System.Tuple<Vector3, Vector3>>();
    private readonly List<System.Tuple<Vector3, Vector3, Color>> _debug_rays = new List<System.Tuple<Vector3, Vector3, Color>>();
    private readonly List<System.Tuple<Vector3, Color>> _debug_samplePoints = new List<System.Tuple<Vector3, Color>>();
    private readonly List<LedgeEdge> _debug_processedEdges = new List<LedgeEdge>();
    private readonly List<System.Tuple<Vector3, string, Color>> _debug_failureLabels = new List<System.Tuple<Vector3, string, Color>>();
#endif

    [ContextMenu("Generate Links From Ledges")]
    public void GenerateLinks()
    {
        ClearLinks();

        _finalAgentID = customAgentTypeID;
        if (autoFindDefaultAgent && NavMesh.GetSettingsCount() > 0)
        {
            _finalAgentID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        }

        Ledge[] allLedges = FindObjectsOfType<Ledge>();
        if (allLedges == null || allLedges.Length == 0)
        {
            Debug.LogWarning("[LedgeLinkGenerator] В сцене не найдено ни одного объекта с компонентом 'Ledge'.", this);
            return;
        }

        foreach (Ledge ledge in allLedges)
        {
            ledge.Awake();

            var edges = new List<LedgeEdge>();
            if (ledge.FrontEdge != null) edges.Add(ledge.FrontEdge);
            if (ledge.BackEdge != null) edges.Add(ledge.BackEdge);
            if (ledge.LeftEdge != null) edges.Add(ledge.LeftEdge);
            if (ledge.RightEdge != null) edges.Add(ledge.RightEdge);

            foreach (LedgeEdge edge in edges)
            {
#if UNITY_EDITOR
                if (enableDeepDebug) _debug_processedEdges.Add(edge);
#endif
                ProcessEdge(edge);
            }
        }

        Debug.Log($"[LedgeLinkGenerator] Поиск завершен. Создано {_createdLinks.Count} линков. Agent ID: {_finalAgentID}", this);
    }

    private void ProcessEdge(LedgeEdge edge)
    {
        float edgeLength = Vector3.Distance(edge.Point1, edge.Point2);
        int linkCount = Mathf.Max(1, Mathf.RoundToInt(edgeLength / linkSpacing));

        for (int i = 0; i < linkCount; i++)
        {
            float t = (linkCount <= 1) ? 0.5f : (float)i / (linkCount - 1);
            Vector3 topPoint = Vector3.Lerp(edge.Point1, edge.Point2, t);
            Vector3 rayOrigin = topPoint + Vector3.up * topRaycastVerticalOffset;

            // 1. ВЫЧИСЛЯЕМ ЛОКАЛЬНЫЙ ПОВОРОТ СРАЗУ
            Vector3 edgeDirection = (edge.Point2 - edge.Point1).normalized;
            Quaternion linkRotation = Quaternion.identity;
            if (edgeDirection != Vector3.zero)
            {
                Vector3 forwardDirection = Vector3.Cross(edgeDirection, Vector3.up);
                linkRotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
            }

            // 2. ПРЕВРАЩАЕМ ЛОКАЛЬНЫЕ ОФФСЕТЫ В ГЛОБАЛЬНЫЕ
            Vector3 worldStartOffset = linkRotation * startPointOffset;
            Vector3 worldEndOffset = linkRotation * endPointOffset;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit floorHit, maxClimbHeight + topRaycastVerticalOffset, floorMask))
            {
                float climbHeight = Vector3.Distance(topPoint, floorHit.point);
                if (climbHeight < minClimbHeight)
                {
#if UNITY_EDITOR
                    if (enableDeepDebug)
                    {
                        _debug_rays.Add(new System.Tuple<Vector3, Vector3, Color>(rayOrigin, floorHit.point, Color.yellow));
                        _debug_failureLabels.Add(new System.Tuple<Vector3, string, Color>(topPoint, $"Слишком низко ({climbHeight:F1}m)", Color.yellow));
                    }
#endif
                    continue; 
                }

#if UNITY_EDITOR
                if (enableDeepDebug) _debug_rays.Add(new System.Tuple<Vector3, Vector3, Color>(rayOrigin, floorHit.point, Color.green));
#endif

                Vector3 startCandidate = floorHit.point + worldStartOffset;
                Vector3 endCandidate = topPoint + worldEndOffset;
                
                bool topOk = TrySampleNavMeshWithFallback(endCandidate, out NavMeshHit topNavHit);
                bool bottomOk = TrySampleNavMeshWithFallback(startCandidate, out NavMeshHit bottomNavHit);

#if UNITY_EDITOR
                if (enableDeepDebug)
                {
                    _debug_samplePoints.Add(new System.Tuple<Vector3, Color>(endCandidate, topOk ? Color.green : Color.red));
                    _debug_samplePoints.Add(new System.Tuple<Vector3, Color>(startCandidate, bottomOk ? Color.green : Color.red));
                    
                    if (!topOk || !bottomOk)
                    {
                        string reason = !topOk ? "Нет верхнего NavMesh" : "Нет нижнего NavMesh";
                        _debug_failureLabels.Add(new System.Tuple<Vector3, string, Color>(topPoint, reason, new Color(1f, 0.5f, 0f)));
                    }
                }
#endif

                if (topOk && bottomOk)
                {
                    // --- НОВАЯ ЗАЩИТА ОТ ЛЕЖАЧИХ ЛИНКОВ ---
                    // Проверяем, не схлопнулись ли верхняя и нижняя точки на один этаж
                    float verticalDiff = Mathf.Abs(topNavHit.position.y - bottomNavHit.position.y);
                    if (verticalDiff < (minClimbHeight * 0.8f)) 
                    {
#if UNITY_EDITOR
                        if (enableDeepDebug)
                        {
                            _debug_failureLabels.Add(new System.Tuple<Vector3, string, Color>(topPoint, "Схлопнулись на один этаж", Color.red));
                        }
#endif
                        continue; // Прерываем постройку линка
                    }
                    // ----------------------------------------

                    float topDrift = Vector3.Distance(endCandidate, topNavHit.position);
                    float bottomDrift = Vector3.Distance(startCandidate, bottomNavHit.position);

                    if (topDrift > maxSnapDrift || bottomDrift > maxSnapDrift)
                    {
#if UNITY_EDITOR
                        if (enableDeepDebug)
                        {
                            string reason = topDrift > maxSnapDrift ? $"Сдвиг верха ({topDrift:F1}m)" : $"Сдвиг низа ({bottomDrift:F1}m)";
                            _debug_failureLabels.Add(new System.Tuple<Vector3, string, Color>(topPoint, reason, Color.magenta));
                        }
#endif
                        continue; 
                    }

                    CreateLink(bottomNavHit.position, topNavHit.position, topPoint, linkRotation);
                }
            }
            else
            {
#if UNITY_EDITOR
                if (enableDeepDebug)
                {
                    _debug_rays.Add(new System.Tuple<Vector3, Vector3, Color>(rayOrigin, rayOrigin + Vector3.down * (maxClimbHeight + topRaycastVerticalOffset), Color.red));
                    _debug_failureLabels.Add(new System.Tuple<Vector3, string, Color>(topPoint, "Нет пола", Color.red));
                }
#endif
            }
        }
    }

    private void CreateLink(Vector3 start, Vector3 end, Vector3 topPoint, Quaternion linkRotation)
    {
        // Переводим точки NavMesh в локальное пространство
        Vector3 localStart = Quaternion.Inverse(linkRotation) * (start - topPoint);
        Vector3 localEnd = Quaternion.Inverse(linkRotation) * (end - topPoint);

        // Обнуляем смещение по оси X
        localStart.x = 0f;
        localEnd.x = 0f;

        // Возвращаем исправленные точки в мировые координаты
        Vector3 correctedStart = topPoint + linkRotation * localStart;
        Vector3 correctedEnd = topPoint + linkRotation * localEnd;

        // Проверка на дистанцию
        foreach (var link in _createdLinks)
        {
            if (Vector3.Distance(link.transform.TransformPoint(link.endPoint), correctedEnd) < linkProximity)
            {
#if UNITY_EDITOR
                if (enableDeepDebug) _debug_failureLabels.Add(new System.Tuple<Vector3, string, Color>(topPoint, "Слишком близко", Color.gray));
#endif
                return;
            }
        }

        var go = new GameObject("LedgeClimbUpLink");
        go.transform.position = correctedStart; 
        go.transform.rotation = linkRotation;
        go.transform.SetParent(transform, true);

        var navLink = go.AddComponent<NavMeshLink>();
        navLink.startPoint = Vector3.zero; 
        navLink.endPoint = go.transform.InverseTransformPoint(correctedEnd); 
        
        navLink.width = linkWidth;
        navLink.costModifier = costModifier;
        navLink.area = linkAreaIndex;
        navLink.agentTypeID = _finalAgentID;
        navLink.bidirectional = true; 
        
        _createdLinks.Add(navLink);
#if UNITY_EDITOR
        if (visualizeProcess) _debug_createdLinkPoints.Add(new System.Tuple<Vector3, Vector3>(correctedStart, correctedEnd));
#endif
    }

    private bool TrySampleNavMeshWithFallback(Vector3 point, out NavMeshHit hit)
    {
        if (NavMesh.SamplePosition(point, out hit, navMeshSampleRadius, NavMesh.AllAreas))
            return true;

        int rays = Mathf.Max(4, navMeshFallbackRays);
        float step = 360f / rays;
        for (int i = 0; i < rays; i++)
        {
            float angle = step * i * Mathf.Deg2Rad;
            Vector3 ringOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * navMeshFallbackRadius;
            Vector3 probe = point + ringOffset;
            if (NavMesh.SamplePosition(probe, out hit, navMeshFallbackRadius, NavMesh.AllAreas))
                return true;
        }

        return false;
    }

    [ContextMenu("Clear Generated Links")]
    public void ClearLinks()
    {
        for (int i = _createdLinks.Count - 1; i >= 0; i--)
        {
            if (_createdLinks[i] != null)
            {
                if (Application.isPlaying) Destroy(_createdLinks[i].gameObject);
                else DestroyImmediate(_createdLinks[i].gameObject);
            }
        }

        _createdLinks.Clear();
#if UNITY_EDITOR
        _debug_createdLinkPoints.Clear();
        _debug_rays.Clear();
        _debug_samplePoints.Clear();
        _debug_processedEdges.Clear();
        _debug_failureLabels.Clear();
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!visualizeProcess) return;

        foreach (var points in _debug_createdLinkPoints)
        {
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawAAPolyLine(5f, points.Item1, points.Item2);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(points.Item1, 0.15f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(points.Item2, 0.15f);
        }

        if (enableDeepDebug)
        {
            foreach (var edge in _debug_processedEdges)
            {
                UnityEditor.Handles.color = Color.magenta;
                UnityEditor.Handles.DrawAAPolyLine(3f, edge.Point1, edge.Point2);
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(edge.Point1, 0.1f);
                Gizmos.DrawSphere(edge.Point2, 0.1f);
            }

            foreach (var ray in _debug_rays)
            {
                UnityEditor.Handles.color = ray.Item3;
                UnityEditor.Handles.DrawAAPolyLine(3f, ray.Item1, ray.Item2);
                Gizmos.color = ray.Item3;
                Gizmos.DrawWireSphere(ray.Item2, 0.15f);
            }

            foreach (var point in _debug_samplePoints)
            {
                Gizmos.color = point.Item2;
                Gizmos.DrawSphere(point.Item1, 0.1f);
            }

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerCenter,
                fontSize = 12
            };

            foreach (var label in _debug_failureLabels)
            {
                labelStyle.normal.textColor = label.Item3;
                UnityEditor.Handles.Label(label.Item1 + Vector3.up * 0.4f, label.Item2, labelStyle);
            }
        }
    }
#endif
}