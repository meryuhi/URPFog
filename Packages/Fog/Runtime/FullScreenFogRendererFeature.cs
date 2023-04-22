using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Meryuhi.Rendering
{
    [DisallowMultipleRendererFeature(FullScreenFog.Name)]
    [Tooltip(FullScreenFog.Name + " will process the related volume overrides in scenes.")]
    public sealed class FullScreenFogRendererFeature : ScriptableRendererFeature
    {
        public struct PassData
        {
            public Material Material;
            public CameraType RenderCamera;
            public RenderTextureDescriptor CameraTargetDescriptor;
        }

        class FullScreenFogRenderPass : ScriptableRenderPass
        {
            private PassData _passData;
            private RTHandle _copiedColor;

            private static readonly int BlitTextureShaderID = Shader.PropertyToID("_BlitTexture");

            private static readonly (string Name, FullScreenFogDensityMode Value)[] ModeShaderKeywords = Enum.GetValues(typeof(FullScreenFogDensityMode))
                .Cast<FullScreenFogDensityMode>()
                .Select(mode => ($"_{nameof(FullScreenFog.densityMode).ToUpper()}_{mode.ToString().ToUpper()}", mode)).ToArray();

            private static readonly (string Name, FullScreenFogMode Value)[] DistanceModeShaderKeywords = Enum.GetValues(typeof(FullScreenFogMode))
                .Cast<FullScreenFogMode>()
                .Select(mode => ($"_{nameof(FullScreenFog.mode).ToUpper()}_{mode.ToString().ToUpper()}", mode)).ToArray();
            private static readonly int MainParamsShaderID = Shader.PropertyToID("_MainParams");

            private static readonly (string Name, FullScreenFogNoiseMode Value)[] NoiseModeShaderKeywords = Enum.GetValues(typeof(FullScreenFogNoiseMode))
                .Cast<FullScreenFogNoiseMode>()
                .Select(mode => ($"_{nameof(FullScreenFog.noiseMode).ToUpper()}_{mode.ToString().ToUpper()}", mode)).ToArray();
            private static readonly int NoiseTexShaderID = Shader.PropertyToID("_NoiseTex");
            private static readonly int NoiseParamsShaderID = Shader.PropertyToID("_NoiseParams");
            private static readonly string PassColorCopyName = $"_{FullScreenFog.Name}PassColorCopy";

            public FullScreenFogRenderPass()
            {
                profilingSampler = new(FullScreenFog.Name);
            }

            public void Setup(PassData passData)
            {
                _passData = passData;
                var colorCopyDescriptor = _passData.CameraTargetDescriptor;
                colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, colorCopyDescriptor, name: PassColorCopyName);
            }

            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                ref var cameraData = ref renderingData.cameraData;
                if (_passData.Material == null || (cameraData.cameraType & _passData.RenderCamera) == 0)
                {
                    return;
                }
#if UNITY_EDITOR
                var sceneView = UnityEditor.SceneView.currentDrawingSceneView;
                if (sceneView != null && cameraData.camera == sceneView.camera && !sceneView.sceneViewState.fogEnabled)
                {
                    return;
                }
#endif
                var stack = VolumeManager.instance.stack;
                var fog = stack?.GetComponent<FullScreenFog>();
                if (fog == null || !fog.IsActive())
                {
                    return;
                }
                UpdateMaterial(fog);
                Render(context, ref cameraData);
            }

            private void UpdateMaterial(FullScreenFog fog)
            {
                var mode = fog.mode.value;
                foreach (var (Name, Value) in DistanceModeShaderKeywords)
                {
                    CoreUtils.SetKeyword(_passData.Material, Name, Value == mode);
                }
                var color = fog.color.value;
                color.a = fog.intensity.value;
                _passData.Material.color = color;

                var densityMode = fog.densityMode.value;
                foreach (var (Name, Value) in ModeShaderKeywords)
                {
                    CoreUtils.SetKeyword(_passData.Material, Name, Value == densityMode);
                }

                var fogParams = new Vector4();
                if (FullScreenFog.UseStartLine(mode))
                {
                    fogParams.x = fog.startLine.value;
                }
                if (FullScreenFog.UseEndLine(mode, densityMode))
                {
                    var delta = fog.endLine.value - fogParams.x;
                    fogParams.y = delta == 0 ? float.MaxValue : 1 / delta;
                }
                var isHeightMode = FullScreenFog.UseStartHeight(mode);
                if (isHeightMode)
                {
                    fogParams.x = fog.startHeight.value;
                }
                if (FullScreenFog.UseEndHeight(mode, densityMode))
                {
                    var delta = fogParams.x - fog.endHeight.value;
                    fogParams.y = delta == 0 ? float.MaxValue : 1 / delta;
                }
                if (FullScreenFog.UseIntensity(densityMode))
                {
                    fogParams.y = fog.density.value;
                }
                if (isHeightMode)/*Because the height fog calculation direction is reversed, we need to reverse the sign*/
                {
                    fogParams.y *= -1;
                }
                _passData.Material.SetVector(MainParamsShaderID, fogParams);

                foreach (var (Name, Value) in NoiseModeShaderKeywords)
                {
                    CoreUtils.SetKeyword(_passData.Material, Name, Value == fog.noiseMode.value);
                }
                var noiseMode = fog.noiseMode.value;
                if (FullScreenFog.UseNoiseTex(noiseMode))
                {
                    _passData.Material.SetTexture(NoiseTexShaderID, fog.noiseTexture.value == null ? Texture2D.whiteTexture : fog.noiseTexture.value);
                }
                if (FullScreenFog.UseNoiseIntensity(noiseMode))
                {
                    _passData.Material.SetVector(NoiseParamsShaderID, new Vector4(fog.noiseIntensity.value, fog.noiseScale.value, fog.noiseScrollSpeed.value.x, fog.noiseScrollSpeed.value.y));
                }
            }

            private void Render(ScriptableRenderContext context, ref CameraData cameraData)
            {
                var cmd = CommandBufferPool.Get();
                var cameraColor = cameraData.renderer.cameraColorTargetHandle;
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    ///This is something from <see cref="FullScreenPassRendererFeature"/>, maybe can be written better?
                    Blitter.BlitCameraTexture(cmd, cameraColor, _copiedColor);
                    _passData.Material.SetTexture(BlitTextureShaderID, _copiedColor);
                    CoreUtils.SetRenderTarget(cmd, cameraColor);
                    CoreUtils.DrawFullScreen(cmd, _passData.Material);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _copiedColor?.Release();
            }
        }

        /// <summary>
        /// An injection point for the pass. This is similar to RenderPassEvent enum but limits to only supported events.
        /// </summary>
        public enum InjectionPoint
        {
            BeforeRenderingSkybox = RenderPassEvent.BeforeRenderingSkybox,
            BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,
            BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
            AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing
        }

        /// <summary>
        /// Selection for when the effect is rendered.
        /// </summary>
        [SerializeField]
        private InjectionPoint _injectionPoint = InjectionPoint.BeforeRenderingPostProcessing;
        /// <summary>
        /// Selection for which camera type want to render.
        /// </summary>
        [SerializeField]
        private CameraType _renderCamera = CameraType.Game | CameraType.SceneView;
        /// <summary>
        /// Material the Renderer Feature uses to render the effect.
        /// </summary>
        [SerializeField]
        [Reload("Shaders/FullScreenFog.shadergraph")]
        private Shader _shader;

        private Material _material;
        private FullScreenFogRenderPass _renderPass;
        public static readonly string PackagePath = "Packages/moe.meryuhi.effects.fog";
        /// <inheritdoc/>
        public override void Create()
        {
#if UNITY_EDITOR
            ResourceReloader.TryReloadAllNullIn(this, PackagePath);
#endif
            if (_shader == null)
            {
                Debug.LogWarning($"Missing {FullScreenFog.Name} shader. {GetType().Name} will not execute. Check for missing reference in the assigned renderer.");
                return;
            }
            _material = CoreUtils.CreateEngineMaterial(_shader);
            _renderPass = new()
            {
                renderPassEvent = (RenderPassEvent)_injectionPoint
            };
            var requirements = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
            if (_renderPass.renderPassEvent > RenderPassEvent.BeforeRenderingTransparents)
            {
                ///According to <see cref="FullScreenPassRendererFeature.Create"/>, do not need <see cref="ScriptableRenderPassInput.Color"/>.
                requirements ^= ScriptableRenderPassInput.Color;
            }
            _renderPass.ConfigureInput(requirements);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                return;
            }
            renderer.EnqueuePass(_renderPass);
        }
        
        /// <inheritdoc/>
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            _renderPass?.Setup(new PassData
            {
                Material = _material, 
                RenderCamera = _renderCamera,
                CameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor,
            });
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _renderPass?.Dispose();
            CoreUtils.Destroy(_material);
        }
    }
}
