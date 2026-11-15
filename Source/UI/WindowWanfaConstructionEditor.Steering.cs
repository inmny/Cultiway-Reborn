using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>在原运动页分别修改实际位置和追踪方向，不把两种曲线混成同一含义。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>零为实际位置，一为方向偏转，二为地图追逐点；重新打开时恢复实际位置。</summary>
    private int curvePurpose;

    /// <summary>目标连接共用的方向偏转参数。</summary>
    /// <param name="goal">当前完整目标。</param>
    private void SteeringRows(ConstructionFlightGoal goal)
    {
        if (goal.Steering == null)
        {
            ActionRow("AddSteering", T("SteeringHint"), () => Change(() =>
            { goal.Steering = new ConstructionFlightSteering { Side = .25f }; curvePurpose = 1; }));
            return;
        }
        Number("SteeringSide", goal.Steering.Side, value => goal.Steering.Side = value);
        Number("SteeringLift", goal.Steering.Lift, value => goal.Steering.Lift = value);
        Number("SteeringDecay", goal.Steering.Decay, value => goal.Steering.Decay = value);
        ActionRow("RemoveSteering", T("SteeringHint"), () => Change(() => { goal.Steering = null; curvePurpose = 0; }));
    }

    /// <summary>在同一运动页选择实际位置、方向或追逐点曲线，复用普通关键点表单。</summary>
    /// <param name="flight">当前实际飞行。</param>
    private bool FlightCurveRows(ConstructionOperation flight)
    {
        ConstructionFlightGoal goal = flight.Goal;
        if (goal == null) return false;
        ConstructionFlightSteering steering = goal.Steering;
        if (curvePurpose == 1 && steering == null || curvePurpose == 2 && goal.Offset == null && goal.OffsetPath == null) curvePurpose = 0;
        string[] purposes = { "CurvePurpose.Position", "CurvePurpose.Steering", "CurvePurpose.Offset" };
        var options = new System.Collections.Generic.List<UiOptionMenuOption>();
        for (int i = 0; i < purposes.Length; i++)
        {
            if (i == 1 && steering == null || i == 2 && goal.Offset == null && goal.OffsetPath == null) continue;
            int choice = i;
            options.Add(new UiOptionMenuOption { Label = T(purposes[i]), Selected = curvePurpose == i,
                Select = () => { curvePurpose = choice; curveLayer = 0; Refresh(); } });
        }
        if (options.Count > 1) Choice("CurvePurpose", T(purposes[curvePurpose]), () => menu.Show(T("CurvePurpose"), options.ToArray()));
        if (curvePurpose == 0) return false;
        if (curvePurpose == 2)
        {
            Row("OffsetPathRules", T("OffsetPathHint"));
            if (goal.OffsetPath == null)
                ActionRow("AddOffsetPath", T("OffsetPathHint"), () => Change(() => goal.OffsetPath = new ConstructionPath
                { Forward = null, Keys = new[] { new ConstructionPathKey(), new ConstructionPathKey { At = .5f } } }));
            else
            {
                ActionRow("RemoveOffsetPath", T("OffsetPathHint"), () => Change(() => goal.OffsetPath = null));
                CurveRows(goal.OffsetPath, offset: true);
            }
            return true;
        }
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
