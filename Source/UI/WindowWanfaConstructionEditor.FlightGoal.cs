using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>目标与到达使用原运动页、撤销和保存，不另建返程法术类型。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>为当前飞行明确选择来源或目标、是否更新及实际到达处理。</summary>
    /// <param name="flight">所选部位的这一次飞行。</param>
    private void FlightGoalRows(ConstructionOperation flight)
    {
        if (flight.Goal == null)
        {
            ActionRow("AddFlightGoal", T("FlightGoalHint"), () => Change(() => flight.Goal = new ConstructionFlightGoal()));
            return;
        }
        ConstructionFlightGoal goal = flight.Goal;
        Choice("FlightGoalAnchor", T("FlightGoalAnchor." + goal.Anchor), () =>
            menu.Show(T("FlightGoalAnchor"), Enum.GetValues(typeof(ConstructionFlightAnchor)).Cast<ConstructionFlightAnchor>()
                .Select(anchor => new UiOptionMenuOption
                {
                    Label = T("FlightGoalAnchor." + anchor), Selected = goal.Anchor == anchor,
                    Select = () => Change(() => goal.Anchor = anchor)
                }).ToArray()));
        Toggle("FlightGoalFollow", goal.Follow, value => goal.Follow = value);
        Choice("FlightArrival", T("FlightArrival." + goal.Arrival), () =>
            menu.Show(T("FlightArrival"), Enum.GetValues(typeof(ConstructionFlightArrival)).Cast<ConstructionFlightArrival>()
                .Select(arrival => new UiOptionMenuOption
                {
                    Label = T("FlightArrival." + arrival), Selected = goal.Arrival == arrival,
                    Select = () => Change(() => goal.Arrival = arrival)
                }).ToArray()));
        Number("FlightGoalRadius", goal.Radius, value => goal.Radius = value);
        Row("FlightGoalRules", T("FlightGoalHint"));
        ActionRow("RemoveFlightGoal", T("RemoveFlightGoalHint"), () => Change(() => flight.Goal = null));
    }
}
