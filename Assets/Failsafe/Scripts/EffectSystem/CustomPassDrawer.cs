using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Failsafe.Scripts.EffectSystem
{
    [System.Serializable]
    public class CustomPassDrawer : CustomPass
    {
        private Material _effectMaterial;
        private static readonly int _customMaterialEnabled = Shader.PropertyToID("_customMaterialEnabled");

        public CustomPassDrawer(Material effectMaterial)
        {
            _effectMaterial = effectMaterial;
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (_effectMaterial == null)
                return;

            // Включаем параметр
            _effectMaterial.SetFloat(_customMaterialEnabled, 1f);

            // Рисуем fullscreen quad
            HDUtils.DrawFullScreen(ctx.cmd, _effectMaterial, ctx.cameraColorBuffer, null, 0);
        }
    }
}