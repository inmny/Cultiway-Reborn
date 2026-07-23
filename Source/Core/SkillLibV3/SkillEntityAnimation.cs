using System;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

[Flags]
public enum SkillAnimationGameplayFlags : byte
{
    /// <summary>阶段只播放动画，不执行玩法逻辑。</summary>
    None = 0,

    /// <summary>阶段允许轨迹系统更新位置和朝向。</summary>
    Movement = 1 << 0,

    /// <summary>阶段允许检测并结算法术碰撞。</summary>
    Collision = 1 << 1,

    /// <summary>阶段允许执行元素掠地效果和词条 OnTravel 回调。</summary>
    TravelEffects = 1 << 2,

    /// <summary>阶段启用全部玩法逻辑。</summary>
    All = Movement | Collision | TravelEffects,
}

/// <summary>
/// 单个动画片段对实体本体与运动配置中动画参数的可选覆盖。
/// 未设置的参数继续继承上层配置。
/// </summary>
public sealed class SkillEntityAnimationSettings
{
    public static SkillEntityAnimationSettings Inherit { get; } = new(null, null);

    public float? FrameIntervalOverride { get; }
    public bool? LoopOverride { get; }

    private SkillEntityAnimationSettings(float? frameIntervalOverride, bool? loopOverride)
    {
        FrameIntervalOverride = frameIntervalOverride;
        LoopOverride = loopOverride;
    }

    /// <summary>
    /// 覆盖每秒播放的动画帧数。
    /// </summary>
    public SkillEntityAnimationSettings WithFrameRate(float framesPerSecond)
    {
        if (float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) || framesPerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        }

        return WithFrameInterval(1f / framesPerSecond);
    }

    /// <summary>
    /// 覆盖相邻动画帧之间的秒数。
    /// </summary>
    public SkillEntityAnimationSettings WithFrameInterval(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        return new SkillEntityAnimationSettings(seconds, LoopOverride);
    }

    /// <summary>
    /// 覆盖动画播放完毕后是否循环。
    /// </summary>
    public SkillEntityAnimationSettings WithLoop(bool loop)
    {
        return new SkillEntityAnimationSettings(FrameIntervalOverride, loop);
    }

    public float ResolveFrameInterval(float inheritedValue)
    {
        return Mathf.Max(0.01f, FrameIntervalOverride ?? inheritedValue);
    }

    public bool ResolveLoop(bool inheritedValue)
    {
        return LoopOverride ?? inheritedValue;
    }

    internal void Apply(ref AnimControllerMeta meta)
    {
        if (FrameIntervalOverride.HasValue) meta.frame_interval = ResolveFrameInterval(meta.frame_interval);
        if (LoopOverride.HasValue) meta.loop = LoopOverride.Value;
    }
}

/// <summary>
/// 法术动画生命周期中的一个独立动画片段。
/// </summary>
public sealed class SkillEntityAnimationClip
{
    public string EffectPath { get; }
    public Sprite[] Frames { get; }
    public SkillEntityAnimationSettings Settings { get; }

    internal SkillEntityAnimationClip(string effectPath, Sprite[] frames, SkillEntityAnimationSettings settings)
    {
        EffectPath = effectPath;
        Frames = frames;
        Settings = settings;
    }
}

/// <summary>
/// 隶属于单个法术实体的动画变体，不注册到全局资产库。
/// </summary>
public sealed class SkillEntityAnimation
{
    public SkillEntityAnimationClip Appearance { get; }
    public SkillEntityAnimationClip Runtime { get; }
    public SkillEntityAnimationClip Dissipation { get; }
    public float Scale { get; }
    public SkillAnimationGameplayFlags AppearanceGameplay { get; }
    public bool HasAppearance => Appearance != null;
    public bool HasDissipation => Dissipation != null;
    public bool HasLifecycle => HasAppearance || HasDissipation;

    private SkillEntityAnimation(
        SkillEntityAnimationClip runtime,
        float scale,
        SkillEntityAnimationClip appearance = null,
        SkillEntityAnimationClip dissipation = null,
        SkillAnimationGameplayFlags appearanceGameplay = SkillAnimationGameplayFlags.None)
    {
        Runtime = runtime;
        Scale = scale;
        Appearance = appearance;
        Dissipation = dissipation;
        AppearanceGameplay = appearanceGameplay;
    }

    public static SkillEntityAnimation Create(string runtimePath, float scale = 0.1f,
        SkillEntityAnimationSettings settings = null)
    {
        return new SkillEntityAnimation(CreateClip(runtimePath, settings), scale);
    }

    public SkillEntityAnimation WithAppearance(string effectPath,
        SkillEntityAnimationSettings settings = null,
        SkillAnimationGameplayFlags gameplay = SkillAnimationGameplayFlags.None)
    {
        if (Appearance != null)
        {
            throw new InvalidOperationException("同一个法术动画变体不能重复配置出现片段");
        }
        if ((gameplay & ~SkillAnimationGameplayFlags.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gameplay));
        }

        ValidateTransientSettings(settings, nameof(settings));
        return new SkillEntityAnimation(
            Runtime,
            Scale,
            CreateClip(effectPath, settings),
            Dissipation,
            gameplay);
    }

    public SkillEntityAnimation WithDissipation(string effectPath,
        SkillEntityAnimationSettings settings = null)
    {
        if (Dissipation != null)
        {
            throw new InvalidOperationException("同一个法术动画变体不能重复配置消散片段");
        }

        ValidateTransientSettings(settings, nameof(settings));
        return new SkillEntityAnimation(
            Runtime,
            Scale,
            Appearance,
            CreateClip(effectPath, settings),
            AppearanceGameplay);
    }

    private static SkillEntityAnimationClip CreateClip(string effectPath, SkillEntityAnimationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(effectPath))
        {
            throw new ArgumentException("法术动画片段必须提供资源路径", nameof(effectPath));
        }

        Sprite[] frames = SkillEntityAsset.LoadOrderedFrames(effectPath);
        if (frames.Length == 0)
        {
            throw new InvalidOperationException($"法术动画片段未加载到任何帧: {effectPath}");
        }

        return new SkillEntityAnimationClip(
            effectPath,
            frames,
            settings ?? SkillEntityAnimationSettings.Inherit);
    }

    private static void ValidateTransientSettings(SkillEntityAnimationSettings settings, string paramName)
    {
        if (settings != null && settings.LoopOverride == true)
        {
            throw new ArgumentException("出现和消散片段必须是非循环动画", paramName);
        }
    }
}
