using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实接触只短暂改变发力部位附近的表面，不另开特效或动作时钟。</summary>
internal sealed partial class BodyActionBinding
{
    /// <summary>接触附近的表面按世界秒数自然消退。</summary>
    private const float FeedbackSeconds = .14f;

    /// <summary>最近一次已确认接触在本动作世界时钟中的年龄。</summary>
    private float feedbackAt = float.NegativeInfinity;

    /// <summary>实际发力部位坐标中的接触位置，回收时随原部位移动。</summary>
    private Vector3 feedbackLocal;

    /// <summary>由共同接触确认调用，预测和绘制不写入。</summary>
    /// <param name="pose">真实接触当刻姿态。</param><param name="point">共同接触位置。</param>
    /// <param name="age">本次施放的世界年龄。</param>
    internal void ConfirmSurfaceContact(in BodyPose pose, Vector3 point, float age)
    {
        feedbackLocal = Quaternion.Inverse(pose.EffectorRotation) * (point - pose.EffectorPoint) / pose.Height;
        feedbackAt = age;
    }

    /// <summary>反馈跟随实际发力部位，用世界年龄消退，暂停时保持同一资料。</summary>
    /// <param name="pose">本轮完成姿态。</param>
    private FormSurfaceFeedback SurfaceFeedback(in BodyPose pose)
    {
        float remaining = 1f - Mathf.Clamp01((pose.WorldAge - feedbackAt) / FeedbackSeconds);
        return remaining <= 0f ? default : new FormSurfaceFeedback(
            pose.EffectorPoint + pose.EffectorRotation * (feedbackLocal * pose.Height),
            pose.Height * .16f, remaining * remaining);
    }
}
