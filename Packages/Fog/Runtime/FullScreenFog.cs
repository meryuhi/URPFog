using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Meryuhi.Rendering
{
    /// <summary>
    /// Fog mode
    /// </summary>
    public enum FullScreenFogMode
    {
        /// <summary>
        /// Directly use the depth value calculation, which is consistent with Unity.
        /// </summary>
        Depth,
        /// <summary>
        /// Using distance for calculation, the calculation will be more complex, but it looks better.
        /// </summary>
        Distance,
        /// <summary>
        /// Use the height in Y asix for calculation.
        /// </summary>
        Height,
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="FullScreenFogMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class FullScreenFogModeParameter : VolumeParameter<FullScreenFogMode>
    {
        /// <summary>
        /// Create a new <see cref="FullScreenFogModeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public FullScreenFogModeParameter(FullScreenFogMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// Density mode
    /// </summary>
    public enum FullScreenFogDensityMode
    {
        /// <summary>
        /// Linear fog.
        /// </summary>
        Linear,
        /// <summary>
        /// Exponential fog.
        /// </summary>
        Exponential,
        /// <summary>
        /// Exponential squared fog (default).
        /// </summary>
        ExponentialSquared,
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="FullScreenFogDensityMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class FullScreenFogDensityModeParameter : VolumeParameter<FullScreenFogDensityMode>
    {
        /// <summary>
        /// Create a new <see cref="FullScreenFogDensityModeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public FullScreenFogDensityModeParameter(FullScreenFogDensityMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// Noise mode
    /// </summary>
    public enum FullScreenFogNoiseMode
    {
        /// <summary>
        /// Disable.
        /// </summary>
        Off,
        /// <summary>
        /// Procedurally generated noise effects, possibly expensive.
        /// </summary>
        Procedural,
        /// <summary>
        /// Use a custom noise texture.
        /// </summary>
        Texture,
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="FullScreenFogNoiseMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class FullScreenFogNoiseModeParameter : VolumeParameter<FullScreenFogNoiseMode>
    {
        /// <summary>
        /// Create a new <see cref="FullScreenFogNoiseModeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public FullScreenFogNoiseModeParameter(FullScreenFogNoiseMode value, bool overrideState = false) : base(value, overrideState) { }
    }


    /// <summary>
    /// A volume component that holds settings for the Full Screen Fog effect.
    /// </summary>
    [Serializable, VolumeComponentMenuForRenderPipeline(nameof(Meryuhi) + "/Full Screen Fog", typeof(UniversalRenderPipeline))]
    public class FullScreenFog : VolumeComponent, IPostProcessComponent
    {
        public const string Name = "Full Screen Fog";
        [Header("Fog")]
        /// <summary>
        /// Calculation mode of the fog.
        /// </summary>
        [Tooltip("Calculation mode of the fog.")]
        public FullScreenFogModeParameter mode = new(FullScreenFogMode.Depth);
        /// <summary>
        /// Amount of the fog.
        /// </summary>
        [Tooltip("Amount of the fog.")]
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);
        /// <summary>
        /// Fog color.
        /// </summary>
        [Tooltip("Fog color.")]
        public ColorParameter color = new(Color.gray, true, false, true);
        /// <summary>
        /// Density mode of the fog.
        /// </summary>
        [Tooltip("Density mode of the fog.")]
        public FullScreenFogDensityModeParameter densityMode = new(FullScreenFogDensityMode.ExponentialSquared);
        /// <summary>
        /// Start depth or distance.
        /// </summary>
        [Tooltip("Start depth or distance.")]
        public FloatParameter startLine = new(0f);
        internal static bool UseStartLine(FullScreenFogMode mode) => mode == FullScreenFogMode.Depth || mode == FullScreenFogMode.Distance;
        /// <summary>
        /// End depth or distance.
        /// </summary>
        [Tooltip("End depth or distance.")]
        public FloatParameter endLine = new(10f);
        internal static bool UseEndLine(FullScreenFogMode mode, FullScreenFogDensityMode densityMode) => UseStartLine(mode) && densityMode == FullScreenFogDensityMode.Linear;
        /// <summary>
        /// Start height.
        /// </summary>
        [Tooltip("Start height.")]
        public FloatParameter startHeight = new(5f);
        internal static bool UseStartHeight(FullScreenFogMode mode) => mode == FullScreenFogMode.Height;
        /// <summary>
        /// End height.
        /// </summary>
        [Tooltip("End height.")]
        public FloatParameter endHeight = new(0f);
        internal static bool UseEndHeight(FullScreenFogMode mode, FullScreenFogDensityMode densityMode) => UseStartHeight(mode) && densityMode == FullScreenFogDensityMode.Linear;
        /// <summary>
        /// Factor of the density mode.
        /// </summary>
        [Tooltip("Factor of the density mode.")]
        public ClampedFloatParameter density = new (0.1f, 0f, 1f);
        internal static bool UseIntensity(FullScreenFogDensityMode mode) => mode == FullScreenFogDensityMode.Exponential || mode == FullScreenFogDensityMode.ExponentialSquared;

        [Header("Noise")]
        /// <summary>
        /// Noise mode for the fog.
        /// </summary>
        [Tooltip("Noise mode for the fog.")]
        public FullScreenFogNoiseModeParameter noiseMode = new(FullScreenFogNoiseMode.Off);
        /// <summary>
        /// Texture used by the noise.
        /// </summary>
        [Tooltip("Texture used by the noise.")]
        public TextureParameter noiseTexture = new(null);
        internal static bool UseNoiseTex(FullScreenFogNoiseMode noiseMode) => noiseMode == FullScreenFogNoiseMode.Texture;
        /// <summary>
        /// Mixing strength of the noise.
        /// </summary>
        [Tooltip("Mixing strength of the noise.")]
        public ClampedFloatParameter noiseIntensity = new(0.5f, 0f, 1f);
        internal static bool UseNoiseIntensity(FullScreenFogNoiseMode noiseMode) => noiseMode != FullScreenFogNoiseMode.Off;
        /// <summary>
        /// Scaling of the noise.
        /// </summary>
        [Tooltip("Scaling of the noise.")]
        public MinFloatParameter noiseScale = new(1f, 0f);
        /// <summary>
        /// Scrolling speed of the noise.
        /// </summary>
        [Tooltip("Scrolling speed of the noise.")]
        public Vector2Parameter noiseScrollSpeed = new(Vector2.one);

        /// <inheritdoc/>
        public bool IsActive()
        {
            return intensity != 0;
        }

        /// <inheritdoc/>
        public bool IsTileCompatible() => true;
    }
}
