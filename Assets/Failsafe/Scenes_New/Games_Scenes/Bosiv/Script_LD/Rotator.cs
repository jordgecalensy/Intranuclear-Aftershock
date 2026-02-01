using UnityEngine;

public class Rotator : MonoBehaviour  
{
    [Header("Настройки вращения")]
    [Tooltip("Скорость вращения объекта")]
    public float rotationSpeed = 10f;

    [Header("Ось вращения")]
    [Tooltip("Ось вращения (X, Y, Z)")]
    public Vector3 rotationAxis = new Vector3(0, 1, 0); // По умолчанию вращение вокруг Y

    void Update()
    {
        // Вычисляем вращение  
        float rotationAmount = rotationSpeed * Time.deltaTime;
        
        // Вращаем объект вокруг указанной оси  
        transform.Rotate(rotationAxis * rotationAmount);
    }
}