using System.Collections.Generic;
using UnityEngine;

public class SlotsRow : MonoBehaviour
{
    public bool isReferenceRow = false;

    public IReadOnlyList<SymbolGrid> GetGrids()
    {
        var list = new List<SymbolGrid>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++)
        {
            var g = transform.GetChild(i).GetComponent<SymbolGrid>();
            if (g) list.Add(g);
        }
        return list;
    }

    public void ClearAll()
    {
        foreach (var g in GetGrids())
            g.Clear(); // у SymbolGrid должен быть метод очистки
    }
}