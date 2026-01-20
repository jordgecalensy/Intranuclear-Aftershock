using UnityEngine;

public interface IInsertable
{
    // вызывать при вставке в держатель
    void OnInserted(Transform holderTransform, float speed);
    // вызывать при извлечении из держателя
    void OnEjected();
    // статус захвата игроком
    bool IsGrabbed { get; }
}
