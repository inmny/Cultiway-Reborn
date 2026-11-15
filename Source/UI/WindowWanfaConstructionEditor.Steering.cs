using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>在原运动页分别修改实际位置和追踪方向，不把两种曲线混成同一含义。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前选择修改方向曲线，窗口重新打开时恢复位置曲线。</summary>
    private bool steeringCurve;

    /// <summary>目标连接共用的方向偏转参数。</summary>
    /// <param name="goal">当前完整目标。</param>
    private void SteeringRows(ConstructionFlightGoal goal)
    {
        if (goal.Steering == null)
        {
            ActionRow("AddSteering", T("SteeringHint"), () => Change(() =>
            { goal.Steering = new ConstructionFlightSteering { Side = .25f }; steeringCurve = true; }));
            return;
        }
        Number("SteeringSide", goal.Steering.Side, value => goal.Steering.Side = value);
        Number("SteeringLift", goal.Steering.Lift, value => goal.Steering.Lift = value);
        Number("SteeringDecay", goal.Steering.Decay, value => goal.Steering.Decay = value);
        ActionRow("RemoveSteering", T("SteeringHint"), () => Change(() => { goal.Steering = null; steeringCurve = false; }));
    }

    /// <summary>同一位置只显示正在修改的一条曲线，复用普通关键点表单。</summary>
    /// <param name="flight">当前实际飞行。</param>
    private bool SteeringCurveRows(ConstructionOperation flight)
    {
        ConstructionFlightSteering steering = flight.Goal?.Steering;
        if (steering == null) return false;
        Choice("CurvePurpose", T(steeringCurve ? "CurvePurpose.Steering" : "CurvePurpose.Position"), () =>
            menu.Show(T("CurvePurpose"), new[]
            {
                new UiOptionMenuOption { Label = T("CurvePurpose.Position"), Selected = !steeringCurve, Select = () => { steeringCurve = false; Refresh(); } },
                new UiOptionMenuOption { Label = T("CurvePurpose.Steering"), Selected = steeringCurve, Select = () => { steeringCurve = true; Refresh(); } }
            }));
        if (!steeringCurve) return false;
        Row("SteeringPathRules", T("SteeringHint"));
        if (steering.Path == null)
            ActionRow("AddSteeringPath", T("SteeringPathHint"), () => Change(() =>
            {
                Vector3 delta = new(-steering.Side, -steering.Lift, 0);
                steering.Path = new ConstructionPath { Forward = null, Keys = new[]
                {
                    new ConstructionPathKey { Out = new[] { delta.x / 3f, delta.y / 3f, 0f } },
                    new ConstructionPathKey { At = .75f, Point = new[] { delta.x, delta.y, 0f }, In = new[] { -delta.x / 3f, -delta.y / 3f, 0f } }
                } };
            }));
        else
        {
            ActionRow("RemoveSteeringPath", T("SteeringPathHint"), () => Change(() => steering.Path = null));
            CurveRows(steering.Path, steering: true);
        }
        return true;
    }
}
