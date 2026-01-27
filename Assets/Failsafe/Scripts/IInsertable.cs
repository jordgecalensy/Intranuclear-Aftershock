using UnityEngine;

public interface IInsertable
{
    // вызывать при вставке в держатель
    void OnInserted(Transform holderTransform);
    // вызывать при извлечении из держателя
    void OnEjected();
}
