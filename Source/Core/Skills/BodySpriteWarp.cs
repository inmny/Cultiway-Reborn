using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>原版图片与实际身体表面共用的数值变形，不读取相机或推进动作。</summary>
internal static class BodySpriteWarp
{
    /// <summary>先处理身体支撑，再衔接头部；返回真正的三维偏移，绘制只取它的投影。</summary>
    /// <param name="x">顶点相对脚下的横向距离。</param><param name="height">顶点离图片脚底高度。</param>
    /// <param name="center">图片横向中心。</param><param name="width">图片实际宽度。</param>
    /// <param name="shoulder">原肩高。</param><param name="torso">共同躯干偏移。</param>
    /// <param name="body">已经完成的身体姿态。</param>
    internal static Vector3 Displacement(float x, float height, float center, float width, float shoulder,
        Vector3 torso, BodyPose? body)
    {
        float weight = Mathf.SmoothStep(0, 1, height / shoulder);
        Vector3 below = body?.LeftLeg.HasValue == true ? Leg() : torso * weight;
        if (body?.Head.HasValue != true) return below;
        BodyPose pose = body.Value;
        BodyHeadPose head = pose.Head.Value;
        Vector3 relative = new(x - head.NativeCenter.x * pose.Height, 0, height - head.NativeCenter.z * pose.Height);
        Vector3 target = (head.A + head.B) * .5f + head.Delta * relative * head.NativeScale;
        Vector3 moved = target - pose.Foot - new Vector3(x, 0, height);
        float start = head.NativePivot.z * pose.Height;
        return Vector3.Lerp(below, moved, Mathf.SmoothStep(0, 1,
            Mathf.InverseLerp(start - pose.Height * .04f, start + pose.Height * .04f, height)));

        /// <summary>两段腿沿原图片高度衔接，同一髋膝足也用于实际接触。</summary>
        Vector3 Leg()
        {
            BodyPose active = body.Value;
            float hipHeight = active.Height * CompiledBodyAction.HipHeight;
            bool left = x - center < 0;
            BodyLegPose leg = (left ? active.LeftLeg : active.RightLeg).Value;
            float referenceX = (left ? -1f : 1f) * width * .2f;
            Vector3 hip = leg.Hip - active.Foot, knee = leg.Knee - active.Foot, foot = leg.Foot - active.Foot;
            Vector3 desired = height < hipHeight * .5f
                ? Vector3.Lerp(foot, knee, Mathf.Clamp01(height / (hipHeight * .5f)))
                : Vector3.Lerp(knee, hip, Mathf.Clamp01((height - hipHeight * .5f) / (hipHeight * .5f)));
            Vector3 delta = desired - new Vector3(referenceX, 0, Mathf.Min(height, hipHeight));
            if (height > hipHeight)
                delta = Vector3.Lerp(delta, torso, Mathf.InverseLerp(hipHeight, shoulder, height));
            return delta;
        }
    }
}