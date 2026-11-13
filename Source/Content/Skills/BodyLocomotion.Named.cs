using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际命名足使用共同位移确认、腾起、阻挡和落地，不创建人形足。</summary>
internal sealed partial class BodyLocomotion
{
    /// <summary>本次实际非人结构。</summary>
    private readonly BodyJointBinding joint;
    /// <summary>各实际足自己的自然落点，采用起势局部方向和体高单位。</summary>
    private readonly BodyFootPose[] naturalFeet;

    /// <summary>接地足保留世界锚点，抬足和腾起后才按实际自然落点与请求重新定位。</summary>
    /// <param name="value">本步候选。</param><param name="support">实际命名足的制作请求。</param>
    /// <param name="terrain">共同地表。</param><param name="previous">阻挡回退时的原锚点。</param>
    private void UpdateNamedFeet(ref State value, BodySupportPose support, AbilityHeightField terrain, State? previous)
    {
        State old = previous ?? value;
        var feet = new BodyFootPose[support.Parts.Length];
        Quaternion heading = Quaternion.AngleAxis(Angles(value).x, Vector3.forward);
        bool acting = action.PartAngles(value.Age - value.ResponseDelay, response).sqrMagnitude > .000001f;
        for (int i = 0; i < feet.Length; i++)
        {
            BodyFootPose request = support.Parts[i];
            bool planted = request.Planted && !value.Airborne && (request.Part != action.Part.Id || !acting);
            bool kept = planted && old.Feet?[i].Planted == true;
            Vector3 point = kept ? old.Feet[i].Point :
                value.Root + heading * Map(naturalFeet[i].Point + request.Point) * value.Height;
            float floor = Floor(terrain, point, Mathf.Max(value.Root.z, point.z) + .00001f, 0f);
            // 滚动后原足面中心可以高于地面，踩实的是足缘，不能把中心再次压到地面。
            if (!kept && old.Feet != null) point.z = value.Airborne ? Mathf.Max(floor, point.z) : floor + request.Point.z * value.Height;
            Matrix4x4? plant = planted ? old.Feet == null ? naturalFeet[i].Plant : old.Feet[i].Planted ? old.Feet[i].Plant : null : null;
            feet[i] = new BodyFootPose(request.Part, point, planted, plant);
        }
        BodySurfaceTransform? seed = old.Joint == null ? null : new BodySurfaceTransform(old.Joint.Root.Pivot,
            old.Joint.Root.Point + value.Root - old.Root, old.Joint.Root.Rotation);
        value.Joint = joint.PlaceFeet(value.Root, heading * forward, terrain, new BodySupportPose(feet, value.Rise), out value.Feet, seed);
    }

    /// <summary>结果发生时冻结各实际足的当前位置，不沿用被跳过的步法。</summary>
    /// <param name="inverse">结果时身体转向的逆变换。</param>
    private BodySupportPose NamedResponse(Quaternion inverse)
    {
        var feet = new BodyFootPose[current.Feet.Length];
        for (int i = 0; i < feet.Length; i++)
        {
            BodyFootPose foot = current.Feet[i];
            Vector3 offset = Local(inverse * (foot.Point - current.Root)) / current.Height - naturalFeet[i].Point;
            feet[i] = new BodyFootPose(foot.Part, offset, foot.Planted);
        }
        return new BodySupportPose(feet, 0f);
    }
}
