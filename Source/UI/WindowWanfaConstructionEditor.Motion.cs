using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>运行中运动变化的普通参数，复用现有菜单及撤销保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前查看的事件操作及飞行段。</summary>
    private int motionChoice, flightChoice;

    /// <summary>列出、增加或修改当前主体上的有限操作。</summary>
    /// <param name="node">独立草稿的当前部位。</param>
    private void MotionRows(ConstructionNode node)
    {
        Row("MotionRules", T("MotionRulesHint"));
        ActionRow("AddMotion", T("AddMotionHint"), () => menu.Show(T("AddMotion"),
            new[] { ConstructionOperationKind.Stop, ConstructionOperationKind.Detach, ConstructionOperationKind.Fly, ConstructionOperationKind.Spin }.Select(kind => new UiOptionMenuOption
            { Label = T("MotionKind." + kind), Select = () => Change(() => { motionChoice = node.Operations.Count(o => o.When != null); node.Operations = node.Operations.Concat(new[] { new ConstructionOperation { Kind = kind, When = new ConstructionCondition() } }).ToArray(); }) }).ToArray()));
        ConstructionOperation[] choices = node.Operations.Where(o => o.When != null).ToArray();
        if (choices.Length == 0) return;
        motionChoice = Math.Min(motionChoice, choices.Length - 1); ConstructionOperation operation = choices[motionChoice];
        Choice("MotionChoice", string.Format(T("MotionChoiceName"), motionChoice + 1, T("MotionKind." + operation.Kind)), () => menu.Show(T("MotionChoice"),
            choices.Select((item, index) => new UiOptionMenuOption { Label = string.Format(T("MotionChoiceName"), index + 1, T("MotionKind." + item.Kind)), Selected = item == operation,
                Select = () => { motionChoice = index; Refresh(); } }).ToArray()));
        ActionRow("RemoveMotion", "", () => Change(() => node.Operations = node.Operations.Where(o => o != operation).ToArray()));
        ConstructionCondition condition = operation.When;
        string sourceName = condition.Source == "." ? T("EventSelf") : condition.Source == ".." ? T("ActivationParent") : condition.Source == "body" ? T("BodyOnly") :
            previewPlan?.Nodes.Select((source, index) => new { source.Path, Label = EventPartName(source, index) }).FirstOrDefault(source => source.Path == condition.Source)?.Label ?? T("ActivationUnknownSource");
        Choice("MotionSource", sourceName, () => ChooseActivationSource(condition));
        Choice("MotionTrigger", T("Event." + condition.Trigger), () => menu.Show(T("MotionTrigger"),
            new[] { ConstructionChangeTrigger.ActorContact, ConstructionChangeTrigger.Clash, ConstructionChangeTrigger.End, ConstructionChangeTrigger.MatterChanged }.Select(trigger => new UiOptionMenuOption
            { Label = T("Event." + trigger), Selected = condition.Trigger == trigger, Select = () => Change(() => { condition.Trigger = trigger; if (trigger != ConstructionChangeTrigger.MatterChanged) { condition.Matter = null; condition.State = null; } }) }).ToArray()));
        Number("MotionEarliest", operation.At, value => operation.At = value);
        Number("MotionDelay", condition.Delay, value => condition.Delay = value);
        if (condition.Trigger == ConstructionChangeTrigger.MatterChanged)
        {
            Toggle("MotionMaterialFilter", condition.Matter != null, value => { condition.Matter = value ? "ice" : null; condition.State = null; });
            if (condition.Matter != null) Choice("MotionOutputMatter", MaterialName(AbilityMatters.Resolve(condition.Matter, condition.State, "运动条件")), () => menu.Show(T("MotionOutputMatter"),
                AbilityMatters.All.Select(matter => new UiOptionMenuOption { Label = MaterialName(matter), Selected = condition.Matter == matter.Id && (condition.State == null || condition.State == matter.State),
                    Select = () => Change(() => { condition.Matter = matter.Id; condition.State = matter.State; }) }).ToArray(), true));
        }
        if (operation.Kind is not (ConstructionOperationKind.Fly or ConstructionOperationKind.Spin)) return;
        for (int axis = 0; axis < 3; axis++)
        { int component = axis; Number("MotionVector" + new[] { "X", "Y", "Z" }[axis], operation.Vector[axis], value => operation.Vector[component] = value); }
        if (operation.Kind != ConstructionOperationKind.Fly) return;
        Number("MotionHoming", operation.TurnRate, value => operation.TurnRate = value);
        Toggle("MotionInheritVelocity", operation.InheritVelocity, value => operation.InheritVelocity = value);
        FlightPhysicsRows(operation, "Motion");
        ActionRow("MotionPath", T("MotionPathHint"), () => { flightChoice = node.Operations.Where(o => o.Kind == ConstructionOperationKind.Fly).ToList().IndexOf(operation); page = 1; Refresh(); });
    }

    /// <summary>初始和条件飞行共用加速度、方向及着地参数。</summary>
    /// <param name="operation">真实飞行资料。</param>
    /// <param name="prefix">字段所在的参数组。</param>
    private void FlightPhysicsRows(ConstructionOperation operation, string prefix)
    {
        Toggle(prefix + "WorldVelocity", operation.WorldVelocity, value => operation.WorldVelocity = value);
        Choice(prefix + "GroundResponse", T("Ground." + operation.Ground), () => menu.Show(T(prefix + "GroundResponse"),
            Enum.GetValues(typeof(ConstructionGroundResponse)).Cast<ConstructionGroundResponse>().Select(response => new UiOptionMenuOption
            { Label = T("Ground." + response), Selected = operation.Ground == response, Select = () => Change(() => operation.Ground = response) }).ToArray()));
        Toggle(prefix + "Acceleration", operation.Acceleration != null, value => operation.Acceleration = value ? new float[3] : null);
        if (operation.Acceleration == null) return;
        for (int axis = 0; axis < 3; axis++)
        { int component = axis; Number(prefix + "Acceleration" + new[] { "X", "Y", "Z" }[axis], operation.Acceleration[axis], value => operation.Acceleration[component] = value); }
    }

    /// <summary>路径页编辑明确选中的飞行段，不把条件飞行误改成初始飞行。</summary>
    /// <param name="node">当前主体。</param>
    private ConstructionOperation SelectedFlight(ConstructionNode node)
    {
        ConstructionOperation[] flights = node.Operations.Where(o => o.Kind == ConstructionOperationKind.Fly).ToArray();
        flightChoice = Math.Min(flightChoice, Math.Max(0, flights.Length - 1));
        return flights.Length == 0 ? null : flights[flightChoice];
    }

    /// <summary>使用普通文字区分飞行段的开始方式。</summary>
    /// <param name="operation">实际飞行段。</param>
    /// <param name="index">当前顺序。</param>
    private static string FlightName(ConstructionOperation operation, int index) => string.Format(T("FlightName"), index + 1, T(operation.When == null ? "FlightTimed" : "FlightEvent"));
}
