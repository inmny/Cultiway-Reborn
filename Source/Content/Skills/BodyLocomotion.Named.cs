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
        for (int i = 0; i < feet.Length; i++)
        {
            BodyFootPose request = support.Parts[i];
            bool planted = request.Planted && !value.Airborne;
            Vector3 point = planted && old.Feet?[i].Planted == true ? old.Feet[i].Point :
                value.Root + heading * Map(naturalFeet[i].Point + request.Point) * value.Height;
            float floor = Floor(terrain, point, Mathf.Max(value.Root.z, point.z) + .00001f, 0f);
            point.z = value.Airborne ? Mathf.Max(floor, point.z) : floor + request.Point.z * value.Height;
            feet[i] = new BodyFootPose(request.Part, point, planted);
        }
        value.Feet = feet;
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
