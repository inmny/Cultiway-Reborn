using System;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 单个动画变体对实体本体与运动配置中动画参数的可选覆盖。
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
/// 隶属于单个法术实体的动画配置，不注册到全局资产库。
/// </summary>
public sealed class SkillEntityAnimation
{
    public string EffectPath { get; }
    public Sprite[] Frames { get; }
    public float Scale { get; }
    public SkillEntityAnimationSettings Settings { get; }

    internal SkillEntityAnimation(string effectPath, Sprite[] frames, float scale,
        SkillEntityAnimationSettings settings = null)
    {
        EffectPath = effectPath;
        Frames = frames;
        Scale = scale;
        Settings = settings ?? SkillEntityAnimationSettings.Inherit;
    }
}
