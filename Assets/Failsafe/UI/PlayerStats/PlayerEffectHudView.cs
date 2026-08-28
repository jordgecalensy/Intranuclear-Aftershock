using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;

namespace Failsafe.Player.UI
{
    public sealed class PlayerEffectHudView : MonoBehaviour
    {
        [Tooltip("Кубики в порядке снизу вверх. Все кубики должны иметь одного родителя.")]
        [SerializeField] private PlayerEffectHudEntryView[] _entriesBottomToTop;

        [Tooltip("Граница, за которую кубики уезжают вправо. Если не задана, используется их родитель.")]
        [SerializeField] private RectTransform _exitBoundary;

        [SerializeField, Min(0f)] private float _rearrangeSpeed = 600f;
        [SerializeField, Min(0f)] private float _exitSpeed = 900f;
        [SerializeField, Min(0f)] private float _exitPadding = 32f;

        private readonly List<PlayerEffectHudEntryView> _visibleEntries = new();
        private readonly Dictionary<EffectPresentation, PlayerEffectHudEntryView> _entriesByEffect = new();

        private Vector2[] _slotPositions;
        private bool _initialized;

        private void Awake()
        {
            InitializeEntries();
        }

        private void Update()
        {
            if (!_initialized)
                InitializeEntries();

            for (int i = _visibleEntries.Count - 1; i >= 0; i--)
            {
                PlayerEffectHudEntryView entry = _visibleEntries[i];

                if (!entry.Tick(Time.deltaTime, _rearrangeSpeed, _exitSpeed))
                    continue;

                EffectPresentation presentation = entry.Presentation;

                if (presentation != null)
                    _entriesByEffect.Remove(presentation);

                _visibleEntries.RemoveAt(i);
                entry.Release(GetRestingPosition(entry));
                ReflowEntries();
            }
        }

        public void Show(EffectPresentation presentation)
        {
            if (presentation == null)
                return;

            if (!_initialized)
                InitializeEntries();

            if (_entriesByEffect.TryGetValue(presentation, out PlayerEffectHudEntryView existingEntry))
            {
                existingEntry.Refresh(presentation);
                return;
            }

            PlayerEffectHudEntryView freeEntry = FindFreeEntry();

            if (freeEntry == null)
            {
                Debug.LogWarning(
                    $"[PlayerEffectHudView] Нет свободного кубика для эффекта {presentation.Definition.name}. " +
                    "Добавь ещё один элемент в массив Entries Bottom To Top.",
                    this);
                return;
            }

            int slotIndex = Mathf.Min(
                _visibleEntries.Count,
                _slotPositions.Length - 1);

            freeEntry.Bind(presentation, _slotPositions[slotIndex]);
            _visibleEntries.Add(freeEntry);
            _entriesByEffect.Add(presentation, freeEntry);
        }

        public void Refresh(EffectPresentation presentation)
        {
            if (presentation != null &&
                _entriesByEffect.TryGetValue(presentation, out PlayerEffectHudEntryView entry))
            {
                entry.Refresh(presentation);
            }
        }

        public void Hide(EffectPresentation presentation)
        {
            if (presentation == null ||
                !_entriesByEffect.TryGetValue(presentation, out PlayerEffectHudEntryView entry) ||
                entry.IsExiting)
            {
                return;
            }

            entry.BeginExit(CalculateExitX(entry));
        }

        public void ClearImmediate()
        {
            if (!_initialized)
                InitializeEntries();

            foreach (PlayerEffectHudEntryView entry in _visibleEntries)
                entry.Release(GetRestingPosition(entry));

            _visibleEntries.Clear();
            _entriesByEffect.Clear();
        }

        private void InitializeEntries()
        {
            if (_initialized)
                return;

            _initialized = true;

            int entryCount = _entriesBottomToTop?.Length ?? 0;
            _slotPositions = new Vector2[entryCount];

            Transform commonParent = null;

            for (int i = 0; i < entryCount; i++)
            {
                PlayerEffectHudEntryView entry = _entriesBottomToTop[i];

                if (entry == null)
                    continue;

                entry.Initialize();

                if (entry.RectTransform == null)
                {
                    Debug.LogError(
                        $"[PlayerEffectHudView] Элемент с индексом {i} не имеет RectTransform.",
                        this);
                    continue;
                }

                if (commonParent == null)
                    commonParent = entry.RectTransform.parent;
                else if (entry.RectTransform.parent != commonParent)
                    Debug.LogError(
                        "[PlayerEffectHudView] Все кубики должны иметь одного RectTransform-родителя.",
                        this);

                _slotPositions[i] = entry.RectTransform.anchoredPosition;
                entry.Release(_slotPositions[i]);
            }
        }

        private PlayerEffectHudEntryView FindFreeEntry()
        {
            if (_entriesBottomToTop == null)
                return null;

            foreach (PlayerEffectHudEntryView entry in _entriesBottomToTop)
            {
                if (entry != null && entry.Presentation == null)
                    return entry;
            }

            return null;
        }

        private void ReflowEntries()
        {
            int slotIndex = 0;

            foreach (PlayerEffectHudEntryView entry in _visibleEntries)
            {
                if (entry.IsExiting)
                    continue;

                if (slotIndex >= _slotPositions.Length)
                    break;

                entry.SetTargetPosition(_slotPositions[slotIndex]);
                slotIndex++;
            }
        }

        private Vector2 GetRestingPosition(PlayerEffectHudEntryView entry)
        {
            if (_entriesBottomToTop == null)
                return Vector2.zero;

            for (int i = 0; i < _entriesBottomToTop.Length; i++)
            {
                if (_entriesBottomToTop[i] == entry)
                    return _slotPositions[i];
            }

            return Vector2.zero;
        }

        private float CalculateExitX(PlayerEffectHudEntryView entry)
        {
            RectTransform entryRect = entry.RectTransform;
            RectTransform parent = entryRect.parent as RectTransform;

            if (parent == null)
                return entryRect.anchoredPosition.x + entryRect.rect.width + _exitPadding;

            RectTransform boundary = _exitBoundary != null
                ? _exitBoundary
                : parent;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                parent,
                boundary);

            float desiredLocalX =
                bounds.max.x +
                entryRect.rect.width * entryRect.pivot.x +
                _exitPadding;

            float localDistance = desiredLocalX - entryRect.localPosition.x;

            return entryRect.anchoredPosition.x + localDistance;
        }
    }
}
