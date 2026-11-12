using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实结果结束旧步法；空中保留实际惯性，落地以后才继续收势。</summary>
internal sealed partial class BodyLocomotion
{
    /// <summary>只在真实结果确认后选择一次，预测不改写原规则。</summary>
    private BodyResponseState? response;

    /// <summary>结果当刻实际双足，转换到该时刻的身体朝向。</summary>
    private BodySupportPose responseSupport;

    /// <summary>沿用原版受力的实际重力，不依赖制作的腾起曲线。</summary>
    private float responseGravity;

    /// <summary>从已经确认的位置和速度交接，不追赶尚未完成的制作路径。</summary>
    /// <param name="value">同一次接触已经选择的恢复。</param>
    internal void BeginResponse(in BodyResponseState value)
    {
        BodySupportPose feet = Describe(current).Support;
        Quaternion inverse = Quaternion.AngleAxis(-action.Heading(current.Age), Vector3.forward);
        responseSupport = new BodySupportPose(Local(inverse * Map(feet.LeftFoot)),
            Local(inverse * Map(feet.RightFoot)), 0f, feet.LeftPlanted, feet.RightPlanted);
        response = value;
        float massGravity = Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
        responseGravity = .3f * massGravity * massGravity;
        current.Airborne = current.Rise > .000001f;
        if (!current.Airborne) { current.PlanarSpeed = Vector3.zero; current.VerticalSpeed = 0f; }
    }

    /// <summary>朝向和躯干转体采用同一结果时钟，不能继续读取被跳过的原动作。</summary>
    /// <param name="value">该时点的实际运动。</param>
    private Vector2 Angles(in State value)
    {
        if (!response.HasValue) return new Vector2(action.Heading(value.Age), action.StanceTurn(value.Age));
        action.ResponseAngles(value.Age - value.ResponseDelay, response.Value, out float heading, out float turn);
        return new Vector2(heading, turn);
    }

    /// <summary>先推进真实惯性与碰撞，再推进落地后的收势；预测只改局部副本。</summary>
    /// <param name="age">请求的世界时点。</param><param name="value">最近确认的运动副本。</param>
    /// <param name="terrain">已经采集的地表。</param>
    private State CalculateResponse(float age, State value, AbilityHeightField terrain)
    {
        while (value.Age < age)
        {
            float next = Mathf.Min(age, value.Age + .01f), seconds = next - value.Age;
            State candidate = value;
            candidate.Age = next;
            if (value.Airborne)
            {
                candidate.Root += value.PlanarSpeed * seconds;
                candidate.Root.z += value.VerticalSpeed * seconds - .5f * responseGravity * seconds * seconds;
                candidate.VerticalSpeed -= responseGravity * seconds;
                candidate.ResponseDelay += seconds;
            }

            Vector3 requested = candidate.Root;
            float speed = candidate.VerticalSpeed;
            SetResponseHeight(ref candidate, value, terrain);
            UpdateResponseFeet(ref candidate, terrain);
            if (!Allowed(value, candidate, terrain))
            {
                // 已经受阻的平面惯性直接截停，不积成下一次补走；竖直仍受同一地表限制。
                candidate.Root = new Vector3(value.Root.x, value.Root.y, requested.z);
                candidate.VerticalSpeed = speed;
                candidate.PlanarSpeed = Vector3.zero;
                SetResponseHeight(ref candidate, value, terrain);
                UpdateResponseFeet(ref candidate, terrain, value);
            }

            value = candidate;
        }

        if (age == current.Age)
        {
            SetResponseHeight(ref value, current, terrain);
            UpdateResponseFeet(ref value, terrain);
        }
        return value;
    }

    /// <summary>真实地面和顶棚截停惯性，腾起制作曲线不再写回高度。</summary>
    /// <param name="value">本步候选。</param><param name="previous">本段起点。</param>
    /// <param name="terrain">实际地表。</param>
    private void SetResponseHeight(ref State value, in State previous, AbilityHeightField terrain)
    {
        float floor = Floor(terrain, value.Root, previous.Root.z + .00001f, value.Height * .16f, value.Height * 1.04f);
        value.RequestedZ = value.Root.z;
        if (value.Root.z <= floor)
        {
            value.Root.z = floor;
            value.VerticalSpeed = 0f; value.PlanarSpeed = Vector3.zero;
        }
        else if (value.Root.z > previous.Root.z && CeilingHeight != null)
        {
            float ceiling = CeilingHeight(value.Root, value.Height * .16f, previous.Root.z + value.Height * 1.04f);
            float capped = Mathf.Max(floor, Mathf.Min(value.Root.z, ceiling - value.Height * 1.04f - .00001f));
            if (capped < value.Root.z) value.VerticalSpeed = 0f;
            value.Root.z = capped;
        }
        value.Rise = (value.Root.z - floor) / value.Height;
        value.Airborne = value.Rise > .000001f;
    }

    /// <summary>空中不产生支撑脚，落地后从实际双足连续转入所选恢复。</summary>
    /// <param name="value">本步完整运动。</param><param name="terrain">实际地表。</param>
    /// <param name="previous">阻挡回退时保留原锚点。</param>
    private void UpdateResponseFeet(ref State value, AbilityHeightField terrain, State? previous = null)
    {
        BodySupportPose support = action.ResponseSupport(value.Age - value.ResponseDelay, response.Value, responseSupport);
        if (value.Airborne) support = new BodySupportPose(support.LeftFoot, support.RightFoot, value.Rise, false, false);
        UpdateFeet(ref value, support, terrain, previous);
    }
}
