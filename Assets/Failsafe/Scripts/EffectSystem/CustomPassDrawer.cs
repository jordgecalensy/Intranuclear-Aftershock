using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Failsafe.Scripts.EffectSystem
{
    [System.Serializable]
    public class CustomPassDrawer : CustomPass
    {
        public Material EffectMaterial;
        private static readonly int _customMaterialEnabled = Shader.PropertyToID("_customMaterialEnabled");

        protected override void Execute(CustomPassContext ctx)
        {
            if (EffectMaterial == null)
                return;

            // Включаем параметр
            EffectMaterial.SetFloat(_customMaterialEnabled, 1f);

            // Рисуем fullscreen quad
            HDUtils.DrawFullScreen(ctx.cmd, EffectMaterial, ctx.cameraColorBuffer, null, 0);
        }
    }
}