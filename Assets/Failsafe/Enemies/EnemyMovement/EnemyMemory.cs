using UnityEngine;

public class EnemyMemory
{  
    private Vector3 _lastKnownPlayerPosition;
    private Vector3 _lastKnownPlayerDirection;
    
    // Флаг: помним ли мы что-то?
    private bool _hasLastKnownPosition;

    // Свойства для чтения
    public Vector3 LastKnownPlayerPosition => _lastKnownPlayerPosition;
    public Vector3 LastKnownPlayerDirection => _lastKnownPlayerDirection;
    public bool HasLastKnownPosition => _hasLastKnownPosition; // Это свойство нужно для SearchingState

    public void SetLastKnownPlayerPosition(Vector3 position, Vector3 direction)
    {
        _lastKnownPlayerPosition = position;
        _lastKnownPlayerDirection = direction.normalized;
        _hasLastKnownPosition = true; // Теперь мы знаем, где игрок
    }
    
    // Метод для очистки памяти (например, если долго не видели игрока)
    public void ClearMemory()
    {
        _hasLastKnownPosition = false;
    }
}