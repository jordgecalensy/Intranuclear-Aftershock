using UnityEngine;

public interface IInsertable
{
    // вызывать при вставке в держатель
    void OnInserted();
    // вызывать при извлечении из держателя
    void OnEjected();
}

public interface IEnterable
{
    bool IsRightType(Component candidate);
    // вызывать при входе в держатель
    void OnEntered();
    // вызывать при выходе из держателя
    void OnExited();
}