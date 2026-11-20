using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;

namespace Cultiway.UI;

/// <summary>通过普通选项编辑真实事件开始条件，不建立多招配方。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>开始与运行中过程共用事件及阈值表单。</summary>
    /// <param name="condition">独立草稿中的明确条件。</param>
    /// <param name="prefix">当前字段组。</param>
    private void ConditionTriggerRows(ConstructionCondition condition, string prefix)
    {
        Choice(prefix + "Trigger", T("Event." + condition.Trigger), () => menu.Show(T(prefix + "Trigger"),
            Enum.GetValues(typeof(ConstructionChangeTrigger)).Cast<ConstructionChangeTrigger>().Where(trigger => trigger != ConstructionChangeTrigger.Time).Select(trigger => new UiOptionMenuOption
            {
                Label = T("Event." + trigger), Selected = condition.Trigger == trigger,
                Select = () => Change(() =>
                {
                    condition.Trigger = trigger;
                    if (condition is ConstructionActivation activation)
                    {
                        if (trigger != ConstructionChangeTrigger.Clash) activation.InheritAbsorbedForce = false;
                        if (trigger == ConstructionChangeTrigger.Handoff)
                        {
                            activation.InheritRemainder = true;
                            activation.Placement = ConstructionEventPlacement.Event;
                        }
                        else if (trigger != ConstructionChangeTrigger.End)
                        {
                            activation.InheritRemainder = false;
                            activation.KeepSourcePosition = false;
                            activation.ResetContacts = false;
                        }
                    }
                    if (trigger != ConstructionChangeTrigger.MatterChanged) { condition.Matter = null; condition.State = null; }
                    condition.Threshold = trigger switch
                    {
                        ConstructionChangeTrigger.ForceBelow or ConstructionChangeTrigger.SizeBelow => .5f,
                        ConstructionChangeTrigger.SizeAbove => 1f,
                        ConstructionChangeTrigger.DistanceFromFlightStart or ConstructionChangeTrigger.GroundRouteDistance => 5f,
                        _ => null
                    };
                })
            }).ToArray()));
        if (condition.Threshold.HasValue)
        {
            string field = condition.Trigger switch
            {
                ConstructionChangeTrigger.ForceBelow => "ForceThreshold",
                ConstructionChangeTrigger.DistanceFromFlightStart => "FlightDistanceThreshold",
                ConstructionChangeTrigger.GroundRouteDistance => "GroundRouteThreshold",
                _ => "SizeThreshold"
            };
            Number(prefix + field, condition.Threshold.Value, value => condition.Threshold = value);
        }

        if (condition.Trigger is ConstructionChangeTrigger.GroundRouteDistance or ConstructionChangeTrigger.WaterRouteArrival
            or ConstructionChangeTrigger.SignalDelivery or ConstructionChangeTrigger.Handoff)
            Row("ArrivalRules", T("EventHint." + condition.Trigger));
    }

    /// <summary>编辑当前部位的事件、来源、放置及明确延迟。</summary>
    /// <param name="node">独立草稿中的部位。</param>
    private void ActivationRows(ConstructionNode node)
    {
        Row("ActivationRules", T("ActivationRulesHint"));
        Toggle("ConditionalBirth", node.Activation != null, value => node.Activation = value ? new ConstructionActivation
        { Source = part == 0 && session.Draft.Body != null ? "body" : ".." } : null);
        ConstructionActivation gate = node.Activation;
        if (gate == null) return;
        string sourceName = gate.Source == ".." ? T("ActivationParent") : gate.Source == "body" ? T("BodyOnly") :
            previewPlan?.Nodes.Select((source, index) => new { source.Path, Label = EventPartName(source, index) }).FirstOrDefault(source => source.Path == gate.Source)?.Label ?? T("ActivationUnknownSource");
        Choice("ActivationSource", sourceName, () => ChooseActivationSource(gate));
        ConditionTriggerRows(gate, "Activation");
        Number("ActivationDelay", gate.Delay, value => gate.Delay = value);
        if (gate.Trigger == ConstructionChangeTrigger.Handoff)
        {
            Row("EventPlacement", T("EventPlacement.Event"));
            Toggle("KeepSourcePosition", gate.KeepSourcePosition, value => gate.KeepSourcePosition = value);
            Toggle("ResetContacts", gate.ResetContacts, value => gate.ResetContacts = value);
            return;
        }
        Choice("EventPlacement", T("EventPlacement." + gate.Placement), () => menu.Show(T("EventPlacement"),
            Enum.GetValues(typeof(ConstructionEventPlacement)).Cast<ConstructionEventPlacement>().Select(placement => new UiOptionMenuOption
            {
                Label = T("EventPlacement." + placement), Selected = gate.Placement == placement,
                Select = () => Change(() =>
                {
                    gate.Placement = placement;
                    if (placement == ConstructionEventPlacement.Event) return;
                    gate.InheritRemainder = false; gate.InheritAbsorbedForce = false;
                    gate.KeepSourcePosition = false; gate.ResetContacts = false;
                })
            }).ToArray()));
        if (gate.Trigger == ConstructionChangeTrigger.Clash && gate.Placement == ConstructionEventPlacement.Event)
            Toggle("InheritAbsorbedForce", gate.InheritAbsorbedForce, value => gate.InheritAbsorbedForce = value);
        if (gate.Trigger == ConstructionChangeTrigger.End && gate.Placement == ConstructionEventPlacement.Event)
        {
            Toggle("InheritRemainder", gate.InheritRemainder, value =>
            {
                gate.InheritRemainder = value;
                if (value) return;
                gate.KeepSourcePosition = false; gate.ResetContacts = false;
            });
            if (gate.InheritRemainder)
            {
                Toggle("KeepSourcePosition", gate.KeepSourcePosition, value => gate.KeepSourcePosition = value);
                Toggle("ResetContacts", gate.ResetContacts, value => gate.ResetContacts = value);
            }
        }
        if (gate.Trigger != ConstructionChangeTrigger.MatterChanged) return;
        Toggle("EventMaterialFilter", gate.Matter != null, value => { gate.Matter = value ? "ice" : null; gate.State = null; });
        if (gate.Matter == null) return;
        Choice("EventOutputMatter", MaterialName(gate.Matter, gate.State), () => menu.Show(T("EventOutputMatter"),
            AbilityMatters.All.Select(matter => new UiOptionMenuOption
            {
                Label = MaterialName(matter), Selected = gate.Matter == matter.Id && (gate.State == null || gate.State == matter.State),
                Select = () => Change(() => { gate.Matter = matter.Id; gate.State = matter.State; })
            }).ToArray(), true));
    }

    /// <summary>来源选择显示部位序号和实际形体名，不要求玩家理解内部路径。</summary>
    /// <param name="node">共用编译结果中的部位。</param>
    /// <param name="index">实际展开顺序。</param>
    private static string EventPartName(CompiledForm node, int index) => string.Format(T("ActivationPart"), index + 1,
        node.FragmentSource >= 0 ? string.Format(T("FractureSource"), FracturePartName(node.Resource.Description.Parts[0].Id, index)) :
        LMTools.GetOrFallback("Cultiway.Construction.Form." + node.Resource.Description.Id, node.Resource.Description.Id));

    /// <summary>只暂时去掉条件以列出真实展开部位，无效条件也能重新选择来源。</summary>
    /// <param name="gate">正在修改的独立条件。</param>
    private void ChooseActivationSource(ConstructionCondition gate)
    {
        CompiledAbility plan = previewPlan;
        if (plan == null)
        {
            AbilityDefinition structure = ConstructionEditing.Copy(session.Draft);
            ClearConditions(structure.Root, true);
            try { plan = AbilityCompiler.Compile(structure); }
            catch (InvalidDataException error) { state.text = string.Format(T("Invalid"), error.Message); return; }
        }
        var options = plan.Nodes.Select((node, index) => (node, index)).Where(item => !item.node.IsInterruptFall)
            .Select(item => new UiOptionMenuOption
        {
            Label = EventPartName(item.node, item.index), Selected = gate.Source == item.node.Path,
            Select = () => Change(() => gate.Source = item.node.Path)
        }).Prepend(new UiOptionMenuOption
        { Label = T("ActivationParent"), Selected = gate.Source == "..", Select = () => Change(() => gate.Source = "..") }).ToList();
        if (session.Draft.Body != null) options.Insert(0, new UiOptionMenuOption
        { Label = T("BodyOnly"), Selected = gate.Source == "body", Select = () => Change(() => gate.Source = "body") });
        if (gate is not ConstructionActivation) options.Insert(0, new UiOptionMenuOption
        { Label = T("EventSelf"), Selected = gate.Source == ".", Select = () => Change(() => gate.Source = ".") });
        menu.Show(T("ActivationSource"), options, true);
        void ClearConditions(ConstructionNode node, bool root = false)
        {
            if (node == null) return;
            // 只为列出可选来源。保留独立放置，避免已经能飞行的后段被当作仍附着。
            node.Activation = !root && node.Activation?.Placement == ConstructionEventPlacement.Event
                ? new ConstructionActivation { Source = "..", Trigger = ConstructionChangeTrigger.End }
                : null;
            node.Operations = node.Operations.Where(operation => operation.When == null).ToArray();
            foreach (ConstructionNode child in node.Children) ClearConditions(child);
        }
    }
}
