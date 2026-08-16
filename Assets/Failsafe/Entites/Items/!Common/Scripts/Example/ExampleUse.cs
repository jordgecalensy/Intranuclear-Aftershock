using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class ExampleUse : MonoBehaviour
{
    [SerializeField] private InputActionReference _action;

    [ContextMenu(nameof(Invoke))]
    public void Invoke()
    {
        var items = GetComponentsInChildren<Item>();

        foreach (var item in items)
        {
            foreach (var actionsGroup in item.ActionsGroups.Where(x => x.Actions.Contains(_action)))
            {
                actionsGroup.Invoke();
            }
        }
    }
}
