using UnityEngine;

public interface IInsertable
{
    // вызывать при вставке в держатель
    void OnInserted(Transform holderTransform, float speed, float delayTime);
    // вызывать при извлечении из держателя
    void OnEjected();
    // статус захвата игроком
    bool IsGrabbed { get; }
}
