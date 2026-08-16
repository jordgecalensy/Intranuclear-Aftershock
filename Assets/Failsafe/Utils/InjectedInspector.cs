#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VContainer;

public class InjectedInspector : MonoBehaviour
{
    private IObjectResolver _resolver;

    [SerializeField]
    private List<string> _interfaceTypeNames = new List<string>();

    [NonSerialized]
    public List<Type> InterfaceTypes = new List<Type>();

    [NonSerialized]
    public List<object> InjectedInstances = new List<object>();

    private void OnEnable()
    {
        RefreshInterfaceTypes();
    }

    private void OnValidate()
    {
        RefreshInterfaceTypes();
    }

    internal static Type[] GetInterfaceTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type => type != null && type.IsInterface)
            .OrderBy(type => type.FullName)
            .ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    internal void RefreshInterfaceTypes()
    {
        InterfaceTypes.Clear();

        foreach (string typeName in _interfaceTypeNames)
        {
            Type type = Type.GetType(typeName);

            if (type != null && type.IsInterface)
            {
                InterfaceTypes.Add(type);
            }
        }
    }

    [Inject]
    private void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
        InjectAll();
    }

    [ContextMenu(nameof(InjectAll))]
    public void InjectAll()
    {
        InjectedInstances.Clear();

        foreach (var type in InterfaceTypes)
        {
            if (type == null)
            {
                Debug.LogWarning($"Interface is null");
                continue;
            }

            try
            {
                var instance = _resolver.Resolve(type);
                InjectedInstances.Add(instance);
                Debug.Log($"Injected {type.Name}: {instance != null}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to inject {type.Name}: {exception.Message}");
                InjectedInstances.Add(null);
            }
        }
    }

    [ContextMenu(nameof(ClearAll))]
    public void ClearAll()
    {
        _interfaceTypeNames.Clear();
        InterfaceTypes.Clear();
        InjectedInstances.Clear();
    }
}

[CustomEditor(typeof(InjectedInspector))]
public class InjectedInspectorEditor : Editor
{
    private SerializedProperty _interfaceTypeNames;
    private string[] _typeNames;
    private string[] _typeDisplayNames;

    private void OnEnable()
    {
        _interfaceTypeNames = serializedObject.FindProperty("_interfaceTypeNames");
        Type[] interfaceTypes = InjectedInspector.GetInterfaceTypes();
        _typeNames = new[] { string.Empty }
            .Concat(interfaceTypes.Select(type => type.AssemblyQualifiedName))
            .ToArray();
        _typeDisplayNames = new[] { "<None>" }
            .Concat(interfaceTypes.Select(type => type.FullName))
            .ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Interface Types", EditorStyles.boldLabel);

        for (int index = 0; index < _interfaceTypeNames.arraySize; index++)
        {
            SerializedProperty typeName = _interfaceTypeNames.GetArrayElementAtIndex(index);
            int selectedIndex = Mathf.Max(0, Array.IndexOf(_typeNames, typeName.stringValue));

            EditorGUILayout.BeginHorizontal();
            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, _typeDisplayNames);

            if (newSelectedIndex != selectedIndex)
            {
                typeName.stringValue = _typeNames[newSelectedIndex];
            }

            if (GUILayout.Button("-", GUILayout.Width(24f)))
            {
                _interfaceTypeNames.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add Interface"))
        {
            _interfaceTypeNames.InsertArrayElementAtIndex(_interfaceTypeNames.arraySize);
            _interfaceTypeNames.GetArrayElementAtIndex(_interfaceTypeNames.arraySize - 1).stringValue = string.Empty;
        }

        bool changed = serializedObject.ApplyModifiedProperties();
        InjectedInspector inspector = (InjectedInspector)target;

        if (changed)
        {
            inspector.RefreshInterfaceTypes();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Injection is automatic in play mode. Runtime controls appear after entering play mode.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Injected Instances", EditorStyles.boldLabel);

        foreach (object instance in inspector.InjectedInstances)
        {
            EditorGUILayout.LabelField(instance?.ToString() ?? "null");
        }

        if (GUILayout.Button("Inject All"))
        {
            inspector.InjectAll();
        }

        if (GUILayout.Button("Clear All"))
        {
            Undo.RecordObject(inspector, "Clear Injected Inspector");
            inspector.ClearAll();
            EditorUtility.SetDirty(inspector);
        }
    }
}
#endif
