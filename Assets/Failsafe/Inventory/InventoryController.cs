using System;
// InventoryController.cs
using System;
using UnityEngine;

namespace Failsafe.Inventory
{
    public class InventoryController : MonoBehaviour
    {
        [Header("Scene/Player")]
        public Transform player;
        public Camera playerCamera;

        [Header("Player Grid")]
        public string playerGridId = "case";
        public int gridWidth = 10;
        public int gridHeight = 6;

        [Header("Noise FX on Open")]
        public float noiseRadius = 6f;
        public GameObject noiseSpherePrefab;
        public float noiseFxTTL = 0.75f;

        [Header("Quickbar")]
        public int quickbarSize = 5;

        [Header("Open/Close keys")]
        public KeyCode toggleKey1 = KeyCode.I;
        public KeyCode toggleKey2 = KeyCode.Tab;
        public KeyCode closeKey  = KeyCode.Escape;

        [Header("Case (3D board)")]
        public CaseProxy caseProxyPrefab;
        public float caseDistance = 0.9f;
        public float caseHeightOffset = 0.75f;

        public static InventoryController Instance { get; private set; }

        public InventoryModel Model { get; private set; }
        public PlacementService Placement { get; private set; }
        public PlacementService.InventoryService Service { get; private set; }

        [Header("Inventory UI/Control")]
        [SerializeField] private bool forceCursorWhileOpen = true;
        [SerializeField] private bool disableControlsWhileOpen = true;
        [SerializeField] private MonoBehaviour[] controlsToDisable; // сюда FPController, MouseLook и т.п.

        public bool IsOpen => _open;
        public event Action<bool> OnOpenChanged;

        private bool _open;
        private CursorLockMode _savedLock;
        private bool _savedVisible;
        private bool _cursorStateSaved;        private CaseProxy _spawnedCase;

        private void Awake()
        {
            if (Instance != null){ Destroy(gameObject); return; }
            Instance = this;

            Model = new InventoryModel(quickbarSize);
            Placement = new PlacementService();
            Service   = new PlacementService.InventoryService(Model, Placement);

            Model.Grids[playerGridId] = new InventoryGrid(playerGridId, gridWidth, gridHeight);

            SetOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey1) || Input.GetKeyDown(toggleKey2)) SetOpen(!_open);
            else if (_open && Input.GetKeyDown(closeKey)) SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            _open = open;

            if (open)
            {
                // сохранить текущее состояние курсора
                if (!_cursorStateSaved)
                {
                    _savedLock = Cursor.lockState;
                    _savedVisible = Cursor.visible;
                    _cursorStateSaved = true;
                }

                // показать курсор и разлочить
#if UNITY_STANDALONE_OSX
            Cursor.lockState = CursorLockMode.None;      // на macOS Confined бывает глючным
#else
                Cursor.lockState = CursorLockMode.Confined;
#endif
                Cursor.visible = true;

                if (disableControlsWhileOpen) ToggleControls(false);

                SpawnCaseProxy();
                EmitNoise();
            }
            else
            {
                // вернуть состояние курсора
                if (_cursorStateSaved)
                {
                    Cursor.lockState = _savedLock;
                    Cursor.visible   = _savedVisible;
                    _cursorStateSaved = false;
                }

                if (disableControlsWhileOpen) ToggleControls(true);

                DespawnCaseProxy();
            }

            OnOpenChanged?.Invoke(open);
        }
        
        private void LateUpdate()
        {
            // если какой-то контроллер снова залочил курсор — переустановим
            if (_open && forceCursorWhileOpen)
            {
#if UNITY_STANDALONE_OSX
            if (Cursor.lockState != CursorLockMode.None)     Cursor.lockState = CursorLockMode.None;
#else
                if (Cursor.lockState != CursorLockMode.Confined) Cursor.lockState = CursorLockMode.Confined;
#endif
                if (!Cursor.visible) Cursor.visible = true;
            }
        }

        private void ToggleControls(bool enable)
        {
            if (controlsToDisable == null) return;
            for (int i = 0; i < controlsToDisable.Length; i++)
                if (controlsToDisable[i]) controlsToDisable[i].enabled = enable;
        }

        private void SpawnCaseProxy()
        {
            if (_spawnedCase != null) return;
            if (caseProxyPrefab == null) { Debug.LogWarning("CaseProxy prefab is not set"); return; }
            _spawnedCase = Instantiate(caseProxyPrefab);
            _spawnedCase.PlaceInFront(player, caseDistance, caseHeightOffset);
            _spawnedCase.Initialize(this, playerGridId, gridWidth, gridHeight);
        }

        private void DespawnCaseProxy()
        {
            if (_spawnedCase != null) Destroy(_spawnedCase.gameObject);
            _spawnedCase = null;
        }

        private void EmitNoise()
        {
            if (player == null) return;
            // TODO: AI слух события: AINoise.Emit(player.position, noiseRadius);
            if (noiseSpherePrefab)
            {
                var go = Instantiate(noiseSpherePrefab, player.position, Quaternion.identity);
                go.transform.localScale = Vector3.one * noiseRadius * 2f;
                Destroy(go, noiseFxTTL);
            }
        }

        // Дроп предмета из инвентаря в мир
        public void DropToWorld(ItemInstance inst, float forward = 1.1f)
        {
            if (inst?.Def?.WorldPrefab == null || player == null) return;
            var pos = player.position + player.forward * forward + Vector3.up * 0.5f;
            var rot = Quaternion.LookRotation(player.forward, Vector3.up);
            var go  = Instantiate(inst.Def.WorldPrefab, pos, rot);
            var wi  = go.GetComponent<WorldItem>(); if (wi == null) wi = go.AddComponent<WorldItem>();
            wi.BindFromInstance(inst);
            wi.ToWorldState();
        }
        
        
    }
}