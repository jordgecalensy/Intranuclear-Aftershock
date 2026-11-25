using Failsafe.Player.View;
using UnityEngine;

public sealed class BorzoiFlashGrenade : BaseGrеnadeObject
{
    [SerializeField, Min(0.1f)] private float _radius = 10f;
    [SerializeField, Min(0.1f)] private float _duration = 2.5f;

    private bool _detonated;

    public override float ExplosionRadius => _radius;

    public override void Explosion()
    {
        if (_detonated)
            return;

        _detonated = true;
        var player = FindFirstObjectByType<PlayerView>();
        if (player != null && player.PlayerCamera != null &&
            player.PlayerCamera.TryGetComponent(out Camera camera) &&
            camera.isActiveAndEnabled && !PlayerScreenScript.IsCameraFullScreen && IsInView(camera))
        {
            if (!camera.TryGetComponent(out BorzoiFlashEffect effect))
                effect = camera.gameObject.AddComponent<BorzoiFlashEffect>();

            effect.Play(_duration);
        }

        Destroy(gameObject);
    }

    private bool IsInView(Camera camera)
    {
        Vector3 position = transform.position;
        if ((position - camera.transform.position).sqrMagnitude > _radius * _radius)
            return false;

        Vector3 viewportPoint = camera.WorldToViewportPoint(position);
        return viewportPoint.z > 0f &&
            viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
            viewportPoint.y >= 0f && viewportPoint.y <= 1f;
    }
}
