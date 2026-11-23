using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pucaci.Rendering
{
    public class CartoonOutlineFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class OutlineSettings
        {
            public bool isEnabled = true;
            public Color outlineColor = new Color(0.02f, 0.02f, 0.02f, 1f);
            [Range(0.5f, 3.5f)] public float outlineThickness = 1.25f;
            [Range(1f, 50f)] public float depthSensitivity = 12f;
            [Range(0.001f, 0.5f)] public float depthThreshold = 0.04f;
            [Range(0.1f, 10f)] public float normalSensitivity = 1.8f;
            [Range(0.01f, 1f)] public float normalThreshold = 0.25f;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        [SerializeField] private OutlineSettings settings = new OutlineSettings();
        [SerializeField] private Shader outlineShader;

        private Material outlineMaterial;
        private CartoonOutlinePass outlinePass;

        public static CartoonOutlineFeature Instance { get; private set; }
        public OutlineSettings Settings => settings;

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        private static readonly int DepthSensitivityId = Shader.PropertyToID("_DepthSensitivity");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int NormalSensitivityId = Shader.PropertyToID("_NormalSensitivity");
        private static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");

        public override void Create()
        {
            Instance = this;

            if (outlineShader == null)
            {
                outlineShader = Shader.Find("Pucaci/CartoonOutline");
            }

            if (outlineShader != null)
            {
                outlineMaterial = CoreUtils.CreateEngineMaterial(outlineShader);
            }

            outlinePass = new CartoonOutlinePass(settings, outlineMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!settings.isEnabled || outlineMaterial == null) return;

            // Jangan gambar di preview camera atau reflection probe
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView) return;

            outlineMaterial.SetColor(OutlineColorId, settings.outlineColor);
            outlineMaterial.SetFloat(OutlineThicknessId, settings.outlineThickness);
            outlineMaterial.SetFloat(DepthSensitivityId, settings.depthSensitivity);
            outlineMaterial.SetFloat(DepthThresholdId, settings.depthThreshold);
            outlineMaterial.SetFloat(NormalSensitivityId, settings.normalSensitivity);
            outlineMaterial.SetFloat(NormalThresholdId, settings.normalThreshold);

            renderer.EnqueuePass(outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(outlineMaterial);
        }

        private class CartoonOutlinePass : ScriptableRenderPass
        {
            private readonly OutlineSettings passSettings;
            private readonly Material material;
            private const string ProfilerTag = "Cartoon Outline Pass";

            public CartoonOutlinePass(OutlineSettings settings, Material mat)
            {
                passSettings = settings;
                material = mat;
                // Minta URP menyiapkan Depth Texture dan Normal Texture
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null) return;

                var cmd = CommandBufferPool.Get(ProfilerTag);
                using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTag)))
                {
                    CoreUtils.DrawFullScreen(cmd, material);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
