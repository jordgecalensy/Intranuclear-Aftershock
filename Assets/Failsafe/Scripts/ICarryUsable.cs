using UnityEngine;

public interface ICarryUsable
{
    // вызывать один раз при захвате; можно передать камеру для прицеливания
    void OnGrabbed(Transform playerCamera);

    // нажал/отпустил кнопку использования
    void OnUseStart();
    void UseTick(float dt);     // удержание
    void OnUseStop();

    // вызывать при дропе/броске
    void OnDropped();
}
