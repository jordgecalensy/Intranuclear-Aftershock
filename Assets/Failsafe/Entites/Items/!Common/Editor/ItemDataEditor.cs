using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData))]
[CanEditMultipleObjects]
public sealed class ItemDataEditor : Editor
{
    private SerializedProperty _script;

    private SerializedProperty _description;
    private SerializedProperty _type;

    private SerializedProperty _inventoryDefinitionId;
    private SerializedProperty _displayName;
    private SerializedProperty _inventoryIcon;
    private SerializedProperty _worldItemPrefab;
    private SerializedProperty _inventoryModelPrefab;
    private SerializedProperty _inventoryBaseEulerAngles;
    private SerializedProperty _inventoryModelOffsetInCells;
    private SerializedProperty _inventoryModelScaleMultiplier;
    private SerializedProperty _inventoryModelFitPadding;
    private SerializedProperty _inventoryModelMaxDepthInCells;
    private SerializedProperty _inventoryWidth;
    private SerializedProperty _inventoryHeight;
    private SerializedProperty _inventoryMaxStack;
    private SerializedProperty _canRotateInInventory;
    private SerializedProperty _canAssignQuickSlot;

    private SerializedProperty _startUseDelay;
    private SerializedProperty _useDelay;

    private SerializedProperty _usesEnergy;
    private SerializedProperty _energyAmountMax;
    private SerializedProperty _energyCostPerUse;

    private SerializedProperty _useRange;
    private SerializedProperty _useMask;

    private SerializedProperty _defaultModeEffects;
    private SerializedProperty _alternativeModeEffects;

    private SerializedProperty _useSfx;
    private SerializedProperty _emptyUseSfx;
    private SerializedProperty _modeSwitchSfx;

    private bool _showBase = true;
    private bool _showInventory = true;
    private bool _showUseTimings;
    private bool _showEnergy;
    private bool _showRaycast;
    private bool _showEffects;
    private bool _showSfx;

    private InventoryModelPosePreview _inventoryModelPosePreview;

    private void OnEnable()
    {
        _inventoryModelPosePreview = new InventoryModelPosePreview();
        _script = serializedObject.FindProperty("m_Script");

        _description = serializedObject.FindProperty(nameof(ItemData.Description));
        _type = serializedObject.FindProperty(nameof(ItemData.Type));

        _inventoryDefinitionId = serializedObject.FindProperty(nameof(ItemData.InventoryDefinitionId));
        _displayName = serializedObject.FindProperty(nameof(ItemData.DisplayName));
        _inventoryIcon = serializedObject.FindProperty(nameof(ItemData.InventoryIcon));
        _worldItemPrefab = serializedObject.FindProperty(nameof(ItemData.WorldItemPrefab));
        _inventoryModelPrefab = serializedObject.FindProperty(nameof(ItemData.InventoryModelPrefab));
        _inventoryBaseEulerAngles = serializedObject.FindProperty(nameof(ItemData.InventoryBaseEulerAngles));
        _inventoryModelOffsetInCells = serializedObject.FindProperty(nameof(ItemData.InventoryModelOffsetInCells));
        _inventoryModelScaleMultiplier = serializedObject.FindProperty(nameof(ItemData.InventoryModelScaleMultiplier));
        _inventoryModelFitPadding = serializedObject.FindProperty(nameof(ItemData.InventoryModelFitPadding));
        _inventoryModelMaxDepthInCells = serializedObject.FindProperty(nameof(ItemData.InventoryModelMaxDepthInCells));
        _inventoryWidth = serializedObject.FindProperty(nameof(ItemData.InventoryWidth));
        _inventoryHeight = serializedObject.FindProperty(nameof(ItemData.InventoryHeight));
        _inventoryMaxStack = serializedObject.FindProperty(nameof(ItemData.InventoryMaxStack));
        _canRotateInInventory = serializedObject.FindProperty(nameof(ItemData.CanRotateInInventory));
        _canAssignQuickSlot = serializedObject.FindProperty(nameof(ItemData.CanAssignQuickSlot));

        _startUseDelay = serializedObject.FindProperty(nameof(ItemData.StartUseDelay));
        _useDelay = serializedObject.FindProperty(nameof(ItemData.UseDelay));

        _usesEnergy = serializedObject.FindProperty(nameof(ItemData.UsesEnergy));
        _energyAmountMax = serializedObject.FindProperty(nameof(ItemData.EnergyAmountMax));
        _energyCostPerUse = serializedObject.FindProperty(nameof(ItemData.EnergyCostPerUse));

        _useRange = serializedObject.FindProperty(nameof(ItemData.UseRange));
        _useMask = serializedObject.FindProperty(nameof(ItemData.UseMask));

        _defaultModeEffects = serializedObject.FindProperty(nameof(ItemData.DefaultModeEffects));
        _alternativeModeEffects = serializedObject.FindProperty(nameof(ItemData.AlternativeModeEffects));

        _useSfx = serializedObject.FindProperty(nameof(ItemData.UseSFX));
        _emptyUseSfx = serializedObject.FindProperty(nameof(ItemData.EmptyUseSFX));
        _modeSwitchSfx = serializedObject.FindProperty(nameof(ItemData.ModeSwitchSFX));
    }

    private void OnDisable()
    {
        _inventoryModelPosePreview?.Dispose();
        _inventoryModelPosePreview = null;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(_script);

        DrawGroup(ref _showBase, "Base", _description, _type);
        DrawInventoryGroup();
        DrawGroup(ref _showUseTimings, "Use Timings", _startUseDelay, _useDelay);
        DrawEnergyGroup();
        DrawGroup(ref _showRaycast, "Raycast Use", _useRange, _useMask);
        DrawGroup(
            ref _showEffects,
            "Effects",
            _defaultModeEffects,
            _alternativeModeEffects);
        DrawGroup(ref _showSfx, "SFX", _useSfx, _emptyUseSfx, _modeSwitchSfx);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawInventoryGroup()
    {
        _showInventory = EditorGUILayout.BeginFoldoutHeaderGroup(
            _showInventory,
            "Inventory");

        if (_showInventory)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_inventoryDefinitionId);

            if (!_inventoryDefinitionId.hasMultipleDifferentValues &&
                string.IsNullOrWhiteSpace(_inventoryDefinitionId.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Generate a stable ID before this ItemData is used by the inventory.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Generate missing definition ID"))
                GenerateMissingDefinitionIds();

            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_inventoryIcon);
            EditorGUILayout.PropertyField(_worldItemPrefab);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("3D Inventory Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_inventoryModelPrefab);
            EditorGUILayout.PropertyField(_inventoryBaseEulerAngles);
            DrawInventoryPoseButtons();
            EditorGUILayout.PropertyField(_inventoryModelOffsetInCells);
            EditorGUILayout.PropertyField(_inventoryModelScaleMultiplier);
            EditorGUILayout.PropertyField(_inventoryModelFitPadding);
            EditorGUILayout.PropertyField(_inventoryModelMaxDepthInCells);

            DrawInventoryModelValidation();
            DrawInventoryModelPreview();

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Grid Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_inventoryWidth);
            EditorGUILayout.PropertyField(_inventoryHeight);
            EditorGUILayout.PropertyField(_inventoryMaxStack);
            EditorGUILayout.PropertyField(_canRotateInInventory);
            EditorGUILayout.PropertyField(_canAssignQuickSlot);

            if (!_worldItemPrefab.hasMultipleDifferentValues &&
                _worldItemPrefab.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "World Item Prefab can stay empty during core setup, but dropping and loading the item will require it.",
                    MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void DrawInventoryPoseButtons()
    {
        using (new EditorGUI.DisabledScope(
                   _inventoryBaseEulerAngles.hasMultipleDifferentValues))
        {
            EditorGUILayout.LabelField(
                "Canonical Pose Quarter-Turns",
                EditorStyles.miniBoldLabel);

            DrawAxisRotationButtons("X", 0);
            DrawAxisRotationButtons("Y", 1);
            DrawAxisRotationButtons("Z", 2);

            if (GUILayout.Button("Reset Inventory Pose"))
                _inventoryBaseEulerAngles.vector3Value = Vector3.zero;
        }
    }

    private void DrawAxisRotationButtons(string axisName, int axisIndex)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(axisName, GUILayout.Width(18f));

            if (GUILayout.Button("-90°"))
                RotateInventoryPoseAxis(axisIndex, -90f);

            if (GUILayout.Button("+90°"))
                RotateInventoryPoseAxis(axisIndex, 90f);
        }
    }

    private void RotateInventoryPoseAxis(int axisIndex, float delta)
    {
        Vector3 eulerAngles = _inventoryBaseEulerAngles.vector3Value;
        eulerAngles[axisIndex] = NormalizeAngle(
            eulerAngles[axisIndex] + delta);

        _inventoryBaseEulerAngles.vector3Value = eulerAngles;
    }

    private void DrawInventoryModelPreview()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "Inventory Pose Preview",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Preview matches the runtime inventory pose. Screen horizontal is inventory X, " +
            "screen vertical is inventory Z, and the camera looks along inventory -Y.",
            MessageType.Info);

        Rect previewRect = GUILayoutUtility.GetRect(
            100f,
            240f,
            GUILayout.ExpandWidth(true));

        if (_inventoryModelPosePreview == null ||
            _inventoryModelPrefab.hasMultipleDifferentValues ||
            _inventoryBaseEulerAngles.hasMultipleDifferentValues)
        {
            EditorGUI.HelpBox(
                previewRect,
                "Select a single shared model and pose to display the preview.",
                MessageType.Info);

            return;
        }

        InventoryModelPosePreviewSettings settings =
            new InventoryModelPosePreviewSettings(
                _inventoryModelPrefab.objectReferenceValue as GameObject,
                _inventoryBaseEulerAngles.vector3Value,
                _inventoryModelOffsetInCells.vector3Value,
                _inventoryModelScaleMultiplier.floatValue,
                _inventoryModelFitPadding.floatValue,
                _inventoryModelMaxDepthInCells.floatValue,
                _inventoryWidth.intValue,
                _inventoryHeight.intValue);

        _inventoryModelPosePreview.Draw(previewRect, settings);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle > 180f)
            angle -= 360f;
        else if (angle <= -180f)
            angle += 360f;

        return angle;
    }

    private void DrawInventoryModelValidation()
    {
        if (_inventoryModelPrefab.hasMultipleDifferentValues)
            return;

        if (_inventoryModelPrefab.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a render-only 3D model before this item is shown in the inventory.",
                MessageType.Warning);
        }

        if (!_inventoryModelScaleMultiplier.hasMultipleDifferentValues &&
            _inventoryModelScaleMultiplier.floatValue <= 0f)
        {
            EditorGUILayout.HelpBox(
                "Inventory model scale multiplier must be greater than zero.",
                MessageType.Error);
        }

        if (!_inventoryModelMaxDepthInCells.hasMultipleDifferentValues &&
            _inventoryModelMaxDepthInCells.floatValue <= 0f)
        {
            EditorGUILayout.HelpBox(
                "Inventory model depth must be greater than zero.",
                MessageType.Error);
        }
    }

    private void DrawEnergyGroup()
    {
        _showEnergy = EditorGUILayout.BeginFoldoutHeaderGroup(_showEnergy, "Energy / Charges");

        if (_showEnergy)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_usesEnergy);

            if (_usesEnergy.hasMultipleDifferentValues || _usesEnergy.boolValue)
            {
                EditorGUILayout.PropertyField(_energyAmountMax);
                EditorGUILayout.PropertyField(_energyCostPerUse);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private static void DrawGroup(
        ref bool isExpanded,
        string title,
        params SerializedProperty[] properties)
    {
        isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(isExpanded, title);

        if (isExpanded)
        {
            EditorGUI.indentLevel++;

            foreach (SerializedProperty property in properties)
                EditorGUILayout.PropertyField(property, true);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(2f);
    }

    private void GenerateMissingDefinitionIds()
    {
        serializedObject.ApplyModifiedProperties();
        Undo.RecordObjects(targets, "Generate inventory definition IDs");

        foreach (var currentTarget in targets)
        {
            SerializedObject currentObject = new SerializedObject(currentTarget);
            SerializedProperty currentId = currentObject.FindProperty(
                nameof(ItemData.InventoryDefinitionId));

            if (!string.IsNullOrWhiteSpace(currentId.stringValue))
                continue;

            currentId.stringValue = Guid.NewGuid().ToString("N");
            currentObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentTarget);
        }

        serializedObject.Update();
    }
}
