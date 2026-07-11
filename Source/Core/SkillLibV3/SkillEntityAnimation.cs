using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 隶属于单个法术实体的动画配置，不注册到全局资产库。
/// </summary>
public sealed class SkillEntityAnimation
{
    public string EffectPath { get; }
    public Sprite[] Frames { get; }
    public float Scale { get; }

    internal SkillEntityAnimation(string effectPath, Sprite[] frames, float scale)
    {
        EffectPath = effectPath;
        Frames = frames;
        Scale = scale;
    }
}
