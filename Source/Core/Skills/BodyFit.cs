using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>另一种身体呈现的真实安装点和固定肢段；只含数字，创建后不再修改。</summary>
public sealed class BodyFit
{
    /// <summary>原版参考身体的实际固定腿骨，支撑过程也须遵守其长度。</summary>
    internal static readonly BodyFit NativeSupport = new(Vector3.zero, 0f, 0f,
        BodyLegFit.Native(true).Hip, BodyLegFit.Native(false).Hip,
        BodyLegFit.Native(true).Upper + BodyLegFit.Native(true).Lower,
        BodyLegFit.Native(false).Upper + BodyLegFit.Native(false).Lower,
        0f, 0f, 1f);

    /// <summary>按共同体高归一化的肩点。</summary>
    public readonly Vector3 Shoulder;

    /// <summary>按共同体高归一化的两段臂长。</summary>
    public readonly float Upper, Lower;

    /// <summary>按共同体高归一化的左右腿根。</summary>
    public readonly Vector3 LeftHip, RightHip;

    /// <summary>左右两条腿的固定总长。</summary>
    public readonly float LeftLeg, RightLeg;

    /// <summary>两条腿保留弯曲余量后的最短距离，深蹲也不能折过真实骨长。</summary>
    public readonly float LeftMinimum, RightMinimum;

    /// <summary>该呈现完整身体高度与共同体高的固定比例。</summary>
    public readonly float HeightScale;

    /// <summary>实际两段腿骨与足部包络，仅在腿部动作中使用。</summary>
    public readonly BodyLegFit LeftLegData, RightLegData;

    /// <summary>实际头部关节与几何。</summary>
    public readonly BodyHeadFit HeadData;

    /// <summary>在实际身体接入边界取得的固定比例，工作线程不再读取外部模型。</summary>
    /// <param name="shoulder">实际肩点。</param><param name="upper">上臂长度。</param>
    /// <param name="lower">前臂长度。</param><param name="leftHip">左腿根。</param>
    /// <param name="rightHip">右腿根。</param><param name="leftLeg">左腿总长。</param>
    /// <param name="rightLeg">右腿总长。</param>
    /// <param name="leftMinimum">左腿最短距离。</param><param name="rightMinimum">右腿最短距离。</param>
    /// <param name="heightScale">该呈现的完整身体高度比例。</param>
    /// <param name="leftLegData">实际左腿骨架及足部。</param><param name="rightLegData">实际右腿骨架及足部。</param>
    /// <param name="headData">实际头部骨架及几何。</param>
    public BodyFit(Vector3 shoulder, float upper, float lower, Vector3 leftHip, Vector3 rightHip,
        float leftLeg, float rightLeg, float leftMinimum, float rightMinimum, float heightScale,
        BodyLegFit leftLegData = null, BodyLegFit rightLegData = null, BodyHeadFit headData = null)
    {
        Shoulder = shoulder;
        Upper = upper;
        Lower = lower;
        LeftHip = leftHip;
        RightHip = rightHip;
        LeftLeg = leftLeg;
        RightLeg = rightLeg;
        LeftMinimum = leftMinimum;
        RightMinimum = rightMinimum;
        HeightScale = heightScale;
        LeftLegData = leftLegData;
        RightLegData = rightLegData;
        HeadData = headData;
    }

    /// <summary>从共同可站立位置向请求位移推进，各条真实腿同时限制最大进度。</summary>
    /// <param name="desired">原站立资料已经约束的躯干位移。</param><param name="turn">共同转体角度。</param>
    /// <param name="leftFoot">共同左足落点。</param><param name="rightFoot">共同右足落点。</param>
    /// <param name="swingLeft">旧腿部动作选定的摆腿。</param>
    /// <param name="leftPlanted">左足实际接地。</param><param name="rightPlanted">右足实际接地。</param>
    internal Vector3 Support(Vector3 desired, float turn, Vector3 leftFoot, Vector3 rightFoot, bool? swingLeft = null,
        bool leftPlanted = true, bool rightPlanted = true)
    {
        leftPlanted &= swingLeft != true;
        rightPlanted &= swingLeft != false;
        if (!leftPlanted && !rightPlanted) return desired;
        Quaternion rotation = Quaternion.AngleAxis(-turn, Vector3.forward);
        Vector3 left = leftFoot - rotation * LeftHip,
            right = rightFoot - rotation * RightHip;
        float lr = LeftLeg * BodyJointSolver.Extension, rr = RightLeg * BodyJointSolver.Extension;
        desired.z = Mathf.Max(desired.z, Mathf.Max(LeftMinimum - LeftHip.z, RightMinimum - RightHip.z));
        float low = Mathf.Min(0f,
            !leftPlanted ? Top(right, rr) :
            !rightPlanted ? Top(left, lr) : Mathf.Min(Top(left, lr), Top(right, rr)));
        Vector3 origin = new(0, 0, low), delta = desired - origin;
        float progress = 1f;
        if (leftPlanted)
        {
            Limit(left, lr);
            Limit(leftFoot - Vector3.forward * CompiledBodyAction.HipHeight, CompiledBodyAction.LegReach);
        }

        if (rightPlanted)
        {
            Limit(right, rr);
            Limit(rightFoot - Vector3.forward * CompiledBodyAction.HipHeight, CompiledBodyAction.LegReach);
        }

        return origin + delta * progress;

        // 所有约束均为球内区域，已知可站立点到请求点的线段只有一个离开边界。
        void Limit(Vector3 centre, float radius)
        {
            Vector3 relative = origin - centre;
            float square = delta.sqrMagnitude;
            if (square == 0f || (desired - centre).sqrMagnitude <= radius * radius) return;
            float dot = Vector3.Dot(relative, delta);
            progress = Mathf.Min(progress, Mathf.Clamp01((-dot + Mathf.Sqrt(Mathf.Max(0,
                dot * dot - square * (relative.sqrMagnitude - radius * radius)))) / square));
        }
    }

    /// <summary>固定水平脚距下可采用的最高躯干偏移。</summary>
    /// <param name="centre">球形支撑区域的中心。</param><param name="radius">真实腿长。</param>
    private static float Top(Vector3 centre, float radius) => centre.z +
                                                              Mathf.Sqrt(Mathf.Max(0f,
                                                                  radius * radius - centre.x * centre.x -
                                                                  centre.y * centre.y));
}

/// <summary>同一腕点在另一套实际肩肘中的解，不使用额外显示时钟。</summary>
public readonly struct BodyArmPose
{
    /// <summary>真实肩点、肘点和连续弯曲方向。</summary>
    public readonly Vector3 Shoulder, Elbow, Bend;

    /// <summary>固定两段长度。</summary>
    public readonly float Upper, Lower;

    /// <summary>与本轮腕点同刻的实际身体显示高度。</summary>
    public readonly float BodyHeight;

    /// <summary>保留本轮已经完成的实际接合。</summary>
    /// <param name="shoulder">肩点。</param><param name="elbow">肘点。</param><param name="bend">连续弯曲面。</param>
    /// <param name="upper">第一段长度。</param><param name="lower">第二段长度。</param>
    /// <param name="bodyHeight">本轮完整身体高度。</param>
    internal BodyArmPose(Vector3 shoulder, Vector3 elbow, Vector3 bend, float upper, float lower, float bodyHeight)
    {
        Shoulder = shoulder;
        Elbow = elbow;
        Bend = bend;
        Upper = upper;
        Lower = lower;
        BodyHeight = bodyHeight;
    }
}