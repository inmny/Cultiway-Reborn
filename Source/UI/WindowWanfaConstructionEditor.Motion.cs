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
            new[]
                {
                    ConstructionOperationKind.Ready, ConstructionOperationKind.Stop, ConstructionOperationKind.Detach, ConstructionOperationKind.Fly,
                    ConstructionOperationKind.Spin, ConstructionOperationKind.Grow, ConstructionOperationKind.Animate, ConstructionOperationKind.Deform,
                    ConstructionOperationKind.ConvertMatter, ConstructionOperationKind.Gather,
                    ConstructionOperationKind.Fracture, ConstructionOperationKind.Flow,
                    ConstructionOperationKind.Recover, ConstructionOperationKind.Erode, ConstructionOperationKind.RetimeErosion,
                    ConstructionOperationKind.Leave, ConstructionOperationKind.Conduct, ConstructionOperationKind.Propagate,
                    ConstructionOperationKind.Ray, ConstructionOperationKind.PassRemainder
                }.Where(kind => kind != ConstructionOperationKind.Leave || !node.Operations.Any(o => o.Kind == kind))
                .Where(kind => kind != ConstructionOperationKind.Ray ||
                    Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Span != null && !node.Operations.Any(o => o.Ray != null))
                .Where(kind => kind != ConstructionOperationKind.RetimeErosion || Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Meshes.Length > 0)
                .Where(kind => kind != ConstructionOperationKind.PassRemainder || node.ContactRole != ConstructionContactRole.Decoration &&
                    Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Meshes.Length > 0)
                .Where(kind => kind != ConstructionOperationKind.Gather || part > 0).Where(kind =>
                    kind != ConstructionOperationKind.Fracture ||
                    Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Meshes.Length > 1 &&
                    !node.Operations.Any(o => o.Kind == kind))
                .Where(kind => kind != ConstructionOperationKind.Deform || Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Deformable).Select(kind => new UiOptionMenuOption
                {
                    Label = T("MotionKind." + kind), Select = () => Change(() =>
                    {
                        if (kind == ConstructionOperationKind.ConvertMatter) node.FollowMatter = true;
                        motionChoice = node.Operations.Count(o => o.When != null);
                        node.Operations = node.Operations
                            .Concat(new[] { NewProcessOperation(node, kind, session.Draft.Lifetime) }).ToArray();
                    })
                }).ToArray()));
        ConstructionOperation[] choices = node.Operations.Where(o => o.When != null).ToArray();
        if (choices.Length == 0) return;
        motionChoice = Math.Min(motionChoice, choices.Length - 1); ConstructionOperation operation = choices[motionChoice];
        Choice("MotionChoice", string.Format(T("MotionChoiceName"), motionChoice + 1, T("MotionKind." + operation.Kind)), () => menu.Show(T("MotionChoice"),
            choices.Select((item, index) => new UiOptionMenuOption { Label = string.Format(T("MotionChoiceName"), index + 1, T("MotionKind." + item.Kind)), Selected = item == operation,
                Select = () => { motionChoice = index; Refresh(); } }).ToArray()));
        ActionRow("RemoveMotion", "", () => Change(() => node.Operations = node.Operations.Where(o => o != operation).ToArray()));
        Number("MotionEarliest", operation.At, value => operation.At = value);
        OperationConditionRows(operation);
        OperationParameters(node, operation);
    }

    /// <summary>命名、定时和条件操作共用真实事件表单。</summary>
    /// <param name="operation">当前独立草稿的操作。</param>
    private void OperationConditionRows(ConstructionOperation operation)
    {
        ConstructionCondition condition = operation.When;
        if (condition == null) return;
        string sourceName = condition.Source == "." ? T("EventSelf") : condition.Source == ".." ? T("ActivationParent") : condition.Source == "body" ? T("BodyOnly") :
            previewPlan?.Nodes.Select((source, index) => new { source.Path, Label = EventPartName(source, index) }).FirstOrDefault(source => source.Path == condition.Source)?.Label ?? T("ActivationUnknownSource");
        Choice("MotionSource", sourceName, () => ChooseActivationSource(condition));
        ConditionTriggerRows(condition, "Motion");
        Number("MotionDelay", condition.Delay, value => condition.Delay = value);
        if (condition.Trigger == ConstructionChangeTrigger.MatterChanged)
        {
            Toggle("MotionMaterialFilter", condition.Matter != null, value => { condition.Matter = value ? "ice" : null; condition.State = null; });
            if (condition.Matter != null) Choice("MotionOutputMatter", MaterialName(condition.Matter, condition.State), () => menu.Show(T("MotionOutputMatter"),
                AbilityMatters.All.Select(matter => new UiOptionMenuOption { Label = MaterialName(matter), Selected = condition.Matter == matter.Id && (condition.State == null || condition.State == matter.State),
                    Select = () => Change(() => { condition.Matter = matter.Id; condition.State = matter.State; }) }).ToArray(), true));
        }

    }

    /// <summary>不同声明方式共用操作参数，不另建一份简化的执行资料。</summary>
    /// <param name="node">提供形体和制作动作的实际部位。</param>
    /// <param name="operation">当前选择的同一份操作。</param>
    private void OperationParameters(ConstructionNode node, ConstructionOperation operation)
    {
        bool triggered = operation.When != null || session.Draft.Steps.Any(step => step.Operation == operation);
        if (operation.Kind == ConstructionOperationKind.Fly)
        { FlightParameters(operation); return; }
        if (operation.Kind == ConstructionOperationKind.ChangeSpeed)
            Number("OperationSpeed", operation.Speed, value => operation.Speed = value);
        if (operation.Kind == ConstructionOperationKind.Deform) DeformationParameters(node, operation);
        if (operation.Kind == ConstructionOperationKind.ConsumeInventory) InventoryUseParameters(operation);
        if (operation.Kind == ConstructionOperationKind.Flow) FlowParameters(operation.Flow);
        if (operation.Kind == ConstructionOperationKind.Ray) RayParameters(operation.Ray);
        if (operation.Kind == ConstructionOperationKind.Propagate)
            PropagationParameters(operation.Propagation ??= new ConstructionPropagation());
        if (operation.Kind == ConstructionOperationKind.Conduct)
            ConductionParameters(operation.Conduction ??= new ConstructionConduction());
        if (operation.Kind == ConstructionOperationKind.Leave) ResidueParameters(node, operation);
        if (operation.Kind == ConstructionOperationKind.Recover) RecoveryParameters(operation);
        if (operation.Kind == ConstructionOperationKind.Erode) ErosionParameters(node, operation);
        if (operation.Kind == ConstructionOperationKind.RetimeErosion) RetimeParameters(node, operation);
        if (operation.Kind == ConstructionOperationKind.Fracture) FractureParameters(node, operation.Fracture);
        if (operation.Kind == ConstructionOperationKind.Gather) GatherParameters(node, operation);
        if (operation.Kind == ConstructionOperationKind.Grow)
        {
            Number("ProcessSeconds", operation.Seconds, value => operation.Seconds = value);
            if (triggered) Number("ProcessTargetScale", operation.TargetScale ?? 1f, value => operation.TargetScale = value);
        }
        if (operation.Kind == ConstructionOperationKind.Animate)
        {
            Choice("ProcessClip", ProcessClipName(node, operation.Clip), () => menu.Show(T("ProcessClip"),
                Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Description.Motions.Select(motion => new UiOptionMenuOption
                { Label = ProcessClipName(node, motion.Id), Selected = operation.Clip == motion.Id, Select = () => Change(() => operation.Clip = motion.Id) }).ToArray()));
            Number("ProcessSeconds", operation.Seconds, value => operation.Seconds = value);
            if (triggered) Number("ProcessBlendSeconds", operation.BlendSeconds ?? .15f, value => operation.BlendSeconds = value);
            Toggle("OperationHold", operation.Hold != null, value => operation.Hold = value ? new ConstructionHold { Seconds = .25f } : null);
            if (operation.Hold != null)
            {
                Number("HoldAt", operation.Hold.At, value => operation.Hold.At = value);
                Number("HoldSeconds", operation.Hold.Seconds, value => operation.Hold.Seconds = value);
            }
        }

        if (operation.Kind == ConstructionOperationKind.ConvertMatter)
            MatterConversionParameters(node, operation);
        if (operation.Kind is not (ConstructionOperationKind.Deflect or ConstructionOperationKind.Spin)) return;
        for (int axis = 0; axis < 3; axis++)
        { int component = axis; Number("MotionVector" + new[] { "X", "Y", "Z" }[axis], operation.Vector[axis], value => operation.Vector[component] = value); }
        if (operation.Kind == ConstructionOperationKind.Deflect)
            Toggle("MotionWorldVelocity", operation.WorldVelocity, value => operation.WorldVelocity = value);
    }

    /// <summary>动作在界面显示普通名称，缺少专用翻译时显示实际顺序。</summary>
    /// <param name="node">实际资源。</param>
    /// <param name="clip">明确制作动作。</param>
    private static string ProcessClipName(ConstructionNode node, string clip)
    {
        var motions = Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Description.Motions;
        int index = Array.FindIndex(motions, motion => motion.Id == clip);
        return Cultiway.Utils.Extension.LMTools.GetOrFallback("Cultiway.Construction.Clip." + clip, string.Format(T("ProcessClipNumber"), index + 1));
    }

    /// <summary>按操作需要补齐明确参数，仍由同一编译器决定是否可用。</summary>
    /// <param name="node">实际资源部位。</param>
    /// <param name="kind">新增的过程。</param>
    /// <param name="lifetime">整次存在上限。</param>
    private ConstructionOperation NewProcessOperation(ConstructionNode node, ConstructionOperationKind kind, float lifetime)
    {
        return new ConstructionOperation
        {
            Kind = kind,
            InventoryUse = kind == ConstructionOperationKind.ConsumeInventory ? new ConstructionInventoryUse { Input = session.Draft.Inventory?.FirstOrDefault()?.Id } : null,
            Deformation = kind == ConstructionOperationKind.Deform ? new ConstructionDeformation() : null,
            Ray = kind == ConstructionOperationKind.Ray ? new ConstructionRay() : null,
            Residue = kind == ConstructionOperationKind.Leave ? new ConstructionResidue() : null,
            Conduction = kind == ConstructionOperationKind.Conduct ? new ConstructionConduction() : null,
            Propagation = kind == ConstructionOperationKind.Propagate ? new ConstructionPropagation() : null,
            Recovery = kind == ConstructionOperationKind.Recover ? new ConstructionRecovery() : null,
            Erosion = kind == ConstructionOperationKind.Erode ? new ConstructionErosion() : null,
            WorldMatter = kind == ConstructionOperationKind.RetimeErosion ? new ConstructionWorldMatter { FromMatter = "wood", FromState = "solid" } : null,
            TimeWindows = kind == ConstructionOperationKind.RetimeErosion ? new[] { new ConstructionProcessWindow { Seconds = .5f, Rate = 0f } } : null,
            Flow = kind == ConstructionOperationKind.Flow ? new ConstructionFlow() : null,
            Fracture = kind == ConstructionOperationKind.Fracture ? NewFracture(node, lifetime) : null,
            When = kind == ConstructionOperationKind.Fracture
                ? new ConstructionCondition { Trigger = ConstructionChangeTrigger.ForceBelow, Threshold = .5f }
                : kind == ConstructionOperationKind.Propagate
                    ? new ConstructionCondition { Trigger = ConstructionChangeTrigger.WorldContact }
                    : new ConstructionCondition { Trigger = kind == ConstructionOperationKind.PassRemainder && node.ContactRole == ConstructionContactRole.Barrier
                        ? ConstructionChangeTrigger.Clash : ConstructionChangeTrigger.ActorContact },
            Seconds = kind is ConstructionOperationKind.Grow or ConstructionOperationKind.Gather or ConstructionOperationKind.Deform ? 1f :
                kind == ConstructionOperationKind.Recover ? .6f : kind == ConstructionOperationKind.Erode ? Math.Min(1f, lifetime) : 0f,
            TargetScale = kind == ConstructionOperationKind.Grow ? 2f : null,
            Matter = kind == ConstructionOperationKind.ConvertMatter ? "ice" : null,
            Clip = kind == ConstructionOperationKind.Animate
                ? Cultiway.Core.Visuals.Forms.FormResources.Load(node.Form).Description.Motions.FirstOrDefault()?.Id
                : null
        };
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
    private static string FlightName(ConstructionOperation operation, int index) => string.Format(T("FlightName"),
        index + 1, T(operation.When == null ? "FlightTimed" : "FlightEvent"));
}
