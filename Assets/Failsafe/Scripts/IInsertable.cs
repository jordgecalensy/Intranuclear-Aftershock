using UnityEngine;

public interface IInsertable
{
    // вызывать при вставке в держатель
    void OnInserted(Transform holderTransform, IEnterable charger);
    // вызывать при извлечении из держателя
    void OnEjected();
}

public interface IEnterable
{
    // вызывать при входе в держатель
    void OnEntered();
    // вызывать при выходе из держателя
    void OnExited();
}