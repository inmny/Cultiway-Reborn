using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>原地抵住或抓持时保留真实接触姿态，时限、消耗和作用窗口仍按世界时间推进。</summary>
public readonly struct BodyPoseConstraint
{
    /// <summary>取得接触时的动作世界时间，不保存人物或外部模型。</summary>
    public readonly float Age;

    /// <summary>维持为一，解除时逐渐归还制作姿态。</summary>
    public readonly float Weight;

    /// <summary>记录已经确认的接触阶段。</summary>
    /// <param name="age">本次施放取得接触的世界秒数。</param>
    /// <param name="weight">维持或归还的权重。</param>
    public BodyPoseConstraint(float age, float weight = 1f)
    {
        Age = age;
        Weight = Mathf.Clamp01(weight);
    }

    /// <summary>只保留姿态阶段，不停止施放本身的时钟。</summary>
    /// <param name="age">正在推进的施放时间。</param>
    internal float Sample(float age) => Mathf.Lerp(age, Age, Weight);
}