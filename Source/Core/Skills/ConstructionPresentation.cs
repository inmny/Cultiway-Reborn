using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.Skills;

/// <summary>从确定构造及实际计划生成可读名称和说明，不解释绘制尺寸或战斗结果。</summary>
public static class ConstructionPresentation
{
    /// <summary>本地规则名固定采用原始术语，不随玩家切换语言而改变未完成创作。</summary>
    /// <param name="definition">本次已经生成的完整构造。</param>
    /// <param name="plan">对应的实际计划。</param>
    public static string Name(AbilityDefinition definition, CompiledAbility plan)
    {
        CompiledForm subject = plan.Nodes.FirstOrDefault(node => node.FragmentSource < 0 &&
                                                                 node.Resource.Meshes.Length > 0 &&
                                                                 node.ContactRole !=
                                                                 ConstructionContactRole.Decoration);
        string material = subject?.Matter.Id switch
        {
            "iron" => "金",
            "wood" => "木",
            "water" => subject.Matter.State == "solid" ? "冰" : "水",
            "ice" => "冰",
            "fire" => "火",
            _ => ""
        };
        string form = plan.HasPropagation ? "脉" : (subject?.Resource.Description.Id ?? plan.Body?.WeaponForm) switch
        {
            "sword" => "剑",
            "hand" => "掌",
            "branch" => "枝",
            "water_wave" => "浪",
            "water_flow" => "流",
            "water_whale" => "鲸",
            "fire_body" => "焰",
            "energy_ray" => "光",
            _ => plan.Body != null ? "劲" : "形"
        };
        ConstructionNode[] nodes = Nodes(definition.Root).ToArray();
        string change = plan.Nodes.Any(node => node.HasRay) ? "贯" : nodes.Any(node => node.Operations.Any(operation =>
            operation.Kind == ConstructionOperationKind.Fly && operation.WorldVelocity && operation.Acceleration?[2] < 0f)) ? "坠" :
            plan.HasPropagation ? "震" : nodes.Any(node => node.Distribution != null) ? "列" : plan.ContactBind > 0f
            ? "缚"
            : plan.ContactStagger > 0f
                ? "震"
                : plan.ContactLaunch > 0f
                    ? "升"
                    : plan.HasBarrier
                        ? "护"
                        : plan.ContactForce > 0f &&
                          plan.Nodes.Any(node => (node.Impact ?? plan.Impact).Force < 0f)
                            ? "摄"
                            : nodes.Any(node =>
                                node.Children.Any(child =>
                                    child.Activation?.Trigger == ConstructionChangeTrigger.ActorContact))
                                ? "触生"
                                : plan.HasRecovery
                                    ? "归"
                                    : definition.Binding?.Follow == true && definition.Root?.Form == "pivot" &&
                                      definition.Root.Operations.Any(op => op.Kind == ConstructionOperationKind.Spin)
                                        ? "环"
                                        : nodes.Any(node =>
                                            node.Operations.Any(op => op.Kind == ConstructionOperationKind.Gather))
                                            ? "聚"
                                            : nodes.Any(node =>
                                                node.Operations.Any(op =>
                                                    op.Kind == ConstructionOperationKind.Fracture))
                                                ? "裂"
                                                : nodes.Any(node =>
                                                    node.Operations.Any(op =>
                                                        op.Kind == ConstructionOperationKind.ConvertMatter))
                                                    ? "化"
                                                    : definition.Root?.Form == "pivot" &&
                                                      definition.Root.Children.Length > 1
                                                        ? "叠"
                                                        : plan.Nodes.Any(node =>
                                                            node.Flight?.TurnRate > 0f ||
                                                            node.FlightPath?.TurnRate > 0f)
                                                            ? "追"
                                                            : plan.HasWorldMaterials && nodes.Any(node =>
                                                                node.Operations.Any(op =>
                                                                    op.Kind == ConstructionOperationKind.Conduct))
                                                                ? "引雷"
                                                                : nodes.Any(node =>
                                                                    node.Operations.Any(op =>
                                                                        op.Kind == ConstructionOperationKind.Flow))
                                                                    ? "涌"
                                                                    : nodes.Any(node => node.Operations.Any(operation => operation.Kind == ConstructionOperationKind.Fly && operation.Ground == ConstructionGroundResponse.Follow))
                                                                        ? "沿"
                                                                        : definition.Binding?.Anchor == AbilityAnchor.Target && !definition.Binding.Follow &&
                                                                          definition.Root != null && !definition.Root.Operations.Any(operation => operation.Kind == ConstructionOperationKind.Fly)
                                                                            ? "现"
                                                                    : definition.Body != null && definition.Root != null
                                                                        ? "飞"
                                                                        : definition.Body != null
                                                                            ? "击"
                                                                            : definition.Binding?.Follow == true
                                                                                ? "随"
                                                                                : "行";
        return material + change + form + "诀";
    }

    /// <summary>列出实际声明的全部资源，任选与同时支付采用明确连接词。</summary>
    /// <param name="plan">实际计划。</param>
    internal static string ResourceNames(CompiledAbility plan)
    {
        string[] names = plan.Resources.Select(ResourceName).ToArray();
        return names.Length == 1
            ? names[0]
            : string.Join(
                T(plan.ResourceMode == Cultiway.Core.SkillLibV3.SkillCastResourceRequirementMode.AnyOf
                    ? "PaymentAny"
                    : "PaymentAll"), names);
    }

    /// <summary>保存的短编号与正式报价中的长期编号共用显示名称。</summary>
    internal static string ResourceName(string id)
    {
        const string prefix = "Cultiway.SkillCastResource.";
        if (id.StartsWith(prefix, StringComparison.Ordinal))
            id = id.Substring(prefix.Length) switch { "NativeMana" => "mana", "YaoPower" => "yao_power", var name => name.ToLowerInvariant() };
        return LMTools.GetOrFallback("Cultiway.Construction.Resource." + id, id);
    }

    /// <summary>说明当前完整资料，不储存可能因改造过时的说明副本。</summary>
    /// <param name="definition">当前独立草稿或正式定义。</param>
    /// <param name="plan">同一资料的实际编译结果。</param>
    public static string Explain(AbilityDefinition definition, CompiledAbility plan) => Explain(plan);

    /// <summary>说明直接来自正式运行使用的计划。</summary>
    /// <param name="plan">已编译的法术。</param>
    public static string Explain(CompiledAbility plan)
    {
        var lines = new List<string>
        {
            F("Basics", plan.Cost, ResourceNames(plan), plan.Lifetime, plan.Cooldown),
            plan.Nodes.Any(n => n.Resource.Meshes.Length > 0)
                ? F("Forms", plan.Nodes.Count(n => n.Resource.Meshes.Length > 0),
                    string.Join("、", plan.Nodes.Where(n => n.Resource.Meshes.Length > 0)
                        .Select(n => Material(n.Matter) + " " + Form(n.Resource.Description.Id)).Distinct()))
                : T("BodyOnly")
        };
        foreach (var input in plan.Inventory)
            lines.Add(F("InventoryInput", input.Id, LMTools.GetOrFallback(input.Shape, input.Shape.Substring(input.Shape.LastIndexOf('.') + 1)), input.Count));
        lines.AddRange(plan.Nodes.SelectMany(node => (node.Impact ?? plan.Impact).WorldEffects
            .Where(effect => effect.MaterialVolume > 0).Select(effect => F("WorldMaterial", Form(node.Resource.Description.Id),
                L("Cultiway.Construction.WorldAction." + effect.Action.Id), effect.MaterialVolume))).Distinct());
        string[] distributions = plan.Nodes.Where(node => node.DistributionSource.HasValue)
            .Select(node => L("Cultiway.Construction.DistributionSource." + node.DistributionSource.Value)).Distinct().ToArray();
        if (distributions.Length > 0) lines.Add(F("Distributions", string.Join("、", distributions)));
        if (plan.Body != null) lines.Add(F("Body", plan.Body.ActiveFrom, plan.Body.ActiveUntil));
        if (plan.ContactDamageScale > 0f)
            lines.Add(F("Damage", string.Join("、",
                plan.ActorImpacts.Select(impact => impact.Targets).Distinct().Select(relation => T("Target." + relation))), plan.ContactDamageScale));
        lines.AddRange(plan.Nodes.Where(node => node.RadialMotion is { ChannelLength: 0f }).Select(node =>
            F("RadialMotion", Form(node.Resource.Description.Id), node.RadialMotion.Radius,
                node.RadialMotion.Speed, node.RadialMotion.Interval, T("Target." + node.RadialMotion.Targets))).Distinct());
        lines.AddRange(plan.Nodes.Where(node => node.RadialMotion is { ChannelLength: > 0f }).Select(node =>
            F("WindChannel", Form(node.Resource.Description.Id), node.RadialMotion.ChannelLength, node.RadialMotion.Radius,
                node.RadialMotion.Speed, node.RadialMotion.Interval, T("Target." + node.RadialMotion.Targets))).Distinct());
        lines.AddRange(plan.Nodes.Where(node => node.RadialMotion is { GroundItemLimit: > 0 }).Select(node =>
            F("RadialGroundItems", node.RadialMotion.GroundItemLimit)).Distinct());
        lines.AddRange(plan.Nodes.Where(node => node.WeaponChamber != null).Select(node =>
            F("WeaponChamber", node.WeaponChamber.Radius, node.WeaponChamber.Depth, node.WeaponChamber.ReleaseSpeed)).Distinct());
        lines.AddRange(plan.Nodes.Where(node => node.Activation?.InheritAbsorbedForce == true).Select(node =>
            F("AbsorbedForceOutlet", Form(plan.Nodes[node.Activation.Source].Resource.Description.Id),
                Form(node.Resource.Description.Id), node.Activation.Delay)).Distinct());
        lines.AddRange(plan.Nodes.Where(node => node.Activation?.KeepSourcePosition == true).Select(node =>
            F("KeepSourcePosition", Form(node.Resource.Description.Id),
                Form(plan.Nodes[node.Activation.Source].Resource.Description.Id))).Distinct());
        if (plan.ContactForce > 0f) lines.Add(F("Force", plan.ContactForce));
        if (plan.ContactStagger > 0f) lines.Add(F("Stagger", plan.ContactStagger));
        if (plan.ContactBind > 0f) lines.Add(F("Bind", plan.ContactBind));
        if (plan.ContactLaunch > 0f) lines.Add(F("Launch", plan.ContactLaunch));
        foreach (var impact in plan.ActorImpacts)
        {
            string targets = T("Target." + impact.Targets);
            if (impact.ReleaseGrab != ConstructionGrabRelease.None)
                lines.Add(F("ReleaseGrab", targets, L("Cultiway.Construction.ReleaseGrab." + impact.ReleaseGrab)));
            if (impact.EquipmentRepair is { } repair)
                lines.Add(F("EquipmentRepair", targets, L("Cultiway.Construction.EquipmentSlot." + repair.Slot),
                    repair.MaterialInput, repair.Durability));
            if (impact.Signal is { } signal)
                lines.Add(F("Signal", targets, signal.DisplayName, signal.Duration));
            foreach (var conditional in impact.ConditionalContacts)
                if (conditional.RequiredStatus != null)
                    lines.Add(F("RequiredStatus", targets, conditional.RequiredStatus.GetName()));
            foreach (var restoration in impact.ConditionalContacts.SelectMany(rule =>
                         new[] { rule.Outcome, rule.Otherwise, rule.OnKill, rule.OnSurvive })
                         .Concat(impact.RandomContact?.Outcomes ?? Array.Empty<CompiledConstructionContactOutcome>())
                         .Concat(impact.Spreads.Select(spread => spread.Outcome))
                         .Select(outcome => outcome?.Restoration).Append(impact.Restoration))
                if (restoration?.RemovedStatus != null)
                    lines.Add(F("RemoveStatus", restoration.RemovedStatus.GetName(), restoration.RemovalMaximum,
                        T(restoration.RemovalOwnSourceOnly ? "OwnStatusSource" : "AnyStatusSource")));
            if (impact.AgeYears > 0) lines.Add(F("AgingActors", targets, impact.AgeYears));
            if (impact.EquipmentErosion != null)
                lines.Add(F("AgingEquipment", targets, string.Join("、", impact.EquipmentErosion.Slots.Select(slot =>
                    L("Cultiway.Construction.EquipmentSlot." + slot))), impact.EquipmentErosion.Fraction));
            if (impact.InventoryAging != null)
                lines.Add(F("AgingInventory", targets, impact.InventoryAging.Seconds,
                    impact.InventoryAging.MaximumItems, impact.InventoryAging.Shapes.Count == 0 ? T("AllInventoryShapes") :
                    string.Join("、", impact.InventoryAging.Shapes.Select(shape => LMTools.GetOrFallback(shape,
                        shape.Substring(shape.LastIndexOf('.') + 1))))));
        }
        if (plan.HasBarrier) lines.Add(T("Barrier"));
        if (plan.Binding != null)
            lines.Add(F("Placement", L("Cultiway.Construction.Anchor." + plan.Binding.Anchor),
                T(plan.Binding.Follow ? "Following" : "Fixed")));
        if (plan.ConcurrentLimit > 0) lines.Add(F("ConcurrentLimit", plan.ConcurrentLimit));
        if (plan.Binding?.Anchor == AbilityAnchor.Between)
            lines.Add(F("BetweenFraction", plan.Binding.Fraction));
        if (plan.Binding?.ContactAtDestination == true) lines.Add(T("DestinationOnly"));
        if (plan.Nodes.Any(node => node.SourceLoss == ConstructionSourceLoss.Continue)) lines.Add(T("IndependentSourceLoss"));
        if (plan.Binding?.Anchor == AbilityAnchor.Target && !plan.Binding.Follow &&
            !plan.Nodes[0].Operations.Any(operation => operation.Contract.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Ray))
            lines.Add(T("DirectPlacement"));
        var processes = new List<string>();
        foreach (CompiledForm node in plan.Nodes)
        {
            string form = Form(node.Resource.Description.Id);
            if (node.Correspondence is { } link)
            {
                string[] effects = new[] { ConstructionCorrespondenceEffects.Restoration, ConstructionCorrespondenceEffects.Status, ConstructionCorrespondenceEffects.Force }
                    .Where(effect => (link.Effects & effect) != 0).Select(effect => L("Cultiway.Construction.CorrespondenceEffect." + effect)).ToArray();
                string window = float.IsPositiveInfinity(link.ActiveUntil) ? F("CorrespondenceOpen", link.ActiveFrom) : F("CorrespondenceWindow", link.ActiveFrom, link.ActiveUntil);
                lines.Add(F("Correspondence", form, link.Id, L("Cultiway.Construction.CorrespondenceTarget." + link.Target),
                    link.DamageFraction, effects.Length == 0 ? T("CorrespondenceNoExtras") : string.Join("、", effects), window));
            }
            if (node.Activation != null) processes.Add(F("Appears", form, Condition(node.Activation)));
            int changeIndex = 0;
            foreach (CompiledOperationRequirement operation in node.Operations)
            {
                if (operation.Location.StartsWith("Steps/", StringComparison.Ordinal)) continue;
                var action = node.EventOperations.FirstOrDefault(value => value.Path == operation.Location);
                var kind = operation.Contract.Kind;
                CompiledMatterChange? change = kind == ConstructionOperationKind.ConvertMatter && action == null
                    ? node.Changes[changeIndex++] : null;
                string when = action == null && change.HasValue && change.Value.Trigger != ConstructionChangeTrigger.Time
                    ? T("Condition." + change.Value.Trigger) : When(operation.At, action?.When);
                processes.Add(F("Process", when, form, DescribeOperation(node, kind, action, change?.Matter)));
            }
        }
        foreach (var step in plan.Steps.Where(value => value.Id != null && !value.Native))
        {
            var action = step.Action;
            var node = plan.Nodes[step.Node];
            string detail = DescribeOperation(node, action.Kind, action);
            if (action.Kind == ConstructionOperationKind.Ray)
                processes.Add(F("Process", When(action.At, action.When), Form(node.Resource.Description.Id), detail));
            else processes.Add(F("NamedProcess", step.Id, detail));
        }

        string[] unique = processes.Distinct().ToArray();
        lines.AddRange(unique.Take(12));
        if (unique.Length > 12) lines.Add(F("More", unique.Length - 12));
        if (plan.Parent is { } parent) lines.Add(F("Parent", ChangeName(parent.Change), parent.Revision));
        return string.Join("\n", lines);
    }

    /// <summary>把已经解析的操作及参数换成当前语言。</summary>
    private static string DescribeOperation(CompiledForm node, ConstructionOperationKind kind,
        CompiledEventOperation action, AbilityMatter matter = null)
    {
        if (kind == ConstructionOperationKind.ConvertMatter) return F("Convert", Material(action?.Matter ?? matter));
        if (kind == ConstructionOperationKind.Fly)
        {
            var flight = action?.Flight ?? node.Flight;
            if (flight.WorldVelocity && flight.Acceleration.z < 0f) return T("Falling");
            if (flight.Ground == ConstructionGroundResponse.Follow) return T("GroundFlight");
            if (flight.Path != null) return T("Curve");
            if (flight.TurnRate > 0f) return T("Homing");
        }
        if (action?.Ray is { } ray) return F("Ray", ray.Length, T(ray.FollowTarget ? "RayFollowing" : "RayFixed"), ray.TurnRate);
        if (kind == ConstructionOperationKind.Leave)
        {
            var residue = node.Residue;
            string text = residue.Lifetime > 0f ? F("Residue", residue.Lifetime) : T("ResiduePermanent");
            return residue.Erosion == null ? text : text + " " +
                F("ResidueErosion", residue.Erosion.Seconds, residue.Erosion.Fraction) + ProcessTimeDescription(residue.Erosion.Pace);
        }
        switch (action?.Parameters)
        {
            case CompiledErosion erosion:
                return (erosion.World == null ? F("SelfErosion", erosion.Seconds, erosion.Fraction) :
                    F("WorldErosion", erosion.Seconds, erosion.Fraction, WorldSelectionName(erosion.World), erosion.World.MaximumVolume,
                        T(erosion.World.OwnedOnly ? "OwnWorldMaterials" : "AnyWorldMaterials"))) + ProcessTimeDescription(erosion.Pace);
            case CompiledErosionRetiming retiming:
                return F("RetimeErosion", WorldSelectionName(retiming.World), retiming.World.MaximumVolume,
                           T(retiming.World.OwnedOnly ? "OwnWorldMaterials" : "AnyWorldMaterials")) +
                       (retiming.Pace == null ? " " + T("RestoreProcessTime") : ProcessTimeDescription(retiming.Pace));
            case CompiledConstructionSignal signal when kind == ConstructionOperationKind.SendSignal:
                return F("DirectSignal", signal.DisplayName, signal.Duration);
            case float radius when kind is ConstructionOperationKind.MapSpace or ConstructionOperationKind.MapArtifact:
                return F("Mapping." + kind, radius);
            case ValueTuple<float, int?, float> mapping when kind == ConstructionOperationKind.MapActor:
                string detail = F("Mapping." + kind, mapping.Item1);
                if (mapping.Item2.HasValue) detail += " " + F("MappingCapacity", mapping.Item2.Value);
                if (mapping.Item3 > 0f) detail += " " + F("MappingWait", mapping.Item3);
                return detail;
        }
        return T("Operation." + kind);
    }

    private static string WorldSelectionName(CompiledWorldMatter selection) => selection.DisplayMatter == null ? T("AllWorldMaterials") :
        selection.FromState == null ? F("AllMaterialStates", Material(selection.DisplayMatter)) : Material(selection.DisplayMatter);

    private static string ProcessTimeDescription(CompiledProcessTime pace) => pace == null ? "" :
        " " + F("ProcessTime", string.Join(T("ProcessTimeSeparator"), pace.Describe().Select(window =>
            F(window.Rate == 0f ? "ProcessTimePause" : "ProcessTimeRate", window.At, window.Seconds, window.Rate))));

    private static string When(float at, CompiledConstructionCondition condition) => condition == null ? F("At", at) :
        at > 0f ? F("Gated", at, Condition(condition)) : Condition(condition);

    /// <summary>条件说明与真实时钟、阈值和额外等待一致。</summary>
    /// <param name="condition">实际开始条件。</param>
    private static string Condition(CompiledConstructionCondition condition)
    {
        string text = T("Condition." + condition.Trigger);
        if (condition.Trigger is ConstructionChangeTrigger.ForceBelow or ConstructionChangeTrigger.SizeAbove or ConstructionChangeTrigger.SizeBelow or
            ConstructionChangeTrigger.DistanceFromFlightStart or ConstructionChangeTrigger.GroundRouteDistance)
            text += F(condition.Trigger == ConstructionChangeTrigger.GroundRouteDistance ? "GroundRouteThreshold" : "Threshold", condition.Threshold);
        if (condition.Delay > 0f) text += F("Delay", condition.Delay);
        return text;
    }

    /// <summary>直接来源的规则名称采用当前语言，原法术的专名仍保持。</summary>
    /// <param name="change">正式记录的改变。</param>
    private static string ChangeName(string change) => T("Change." + (change switch
    {
        "换材" => "Material",
        "换形" => "Form",
        "触生" => "ContactGrowth",
        "排布变化" => "Distribution",
        "随身" => "Follow",
        "环绕" => "Orbit",
        "直接放置" => "Placed",
        "射线变化" => "Ray",
        "范围推拉" => "RadialMotion",
        "交锋规则" => "ClashRules",
        "存续规则" => "Persistence",
        "两点连接" => "Between",
        "逐段成形" => "Formation",
        "结构形变" => "Deformation",
        "库存材料" => "Inventory",
        "材料交出" => "MaterialTransfer",
        "过程时间" => "ProcessTime",
        "指定解除" => "RemoveStatus",
        "抓持解救" => "RescueGrab",
        "装备修复" => "RepairEquipment",
        "讯号条件" => "SignalCondition",
        "传播接续" => "PropagationFollowup",
        "调整侵蚀节奏" => "RetimeErosion",
        "主体侵蚀" => "SelfErosion",
        "世界侵蚀" => "WorldErosion",
        "留置侵蚀" => "ResidueErosion",
        "映像传递" => "Correspondence",
        "形体过门" => "MapSpace", "生灵过门" => "MapActor", "法宝过门" => "MapArtifact",
        "生灵老化" => "AgingActors",
        "装备腐蚀" => "AgingEquipment",
        "库存催朽" => "AgingInventory",
        "余量续形" => "Remainder",
        "液态流散" => "Flow",
        "收束回能" => "Recovery",
        "世界留置" => "Residue",
        "复生" => "Repeat",
        "显化" => "Project",
        "收腕" => "TuckWrist",
        "改造" => "Edit",
        "另创" => "Copy",
        _ => "Other"
    }));

    /// <summary>材料及物态名称与编辑表单共用。</summary>
    /// <param name="matter">已经解析的实际材料。</param>
    private static string Material(AbilityMatter matter) =>
        L("Cultiway.Construction.Material." + matter.Id + "." + matter.State);

    /// <summary>形体名称与编辑表单共用。</summary>
    /// <param name="id">实际资源编号。</param>
    private static string Form(string id) => L("Cultiway.Construction.Form." + id);

    /// <summary>未知扩展资源保留其可定位名称，已有词条采用公共本地化。</summary>
    /// <param name="key">完整文本编号。</param>
    private static string L(string key) => LMTools.GetOrFallback(key, key.Substring(key.LastIndexOf('.') + 1));

    /// <summary>本功能的固定文本。</summary>
    /// <param name="key">功能内编号。</param>
    private static string T(string key) => L("Cultiway.ConstructionText." + key);

    /// <summary>把已解析的事实放入当前语言文本。</summary>
    /// <param name="key">功能内格式编号。</param>
    /// <param name="values">实际数值与文本。</param>
    private static string F(string key, params object[] values) => string.Format(T(key), values);

    /// <summary>只遍历声明部位，集合展开和碎片数量由计划说明。</summary>
    /// <param name="node">可为空的生成结构。</param>
    private static IEnumerable<ConstructionNode> Nodes(ConstructionNode node)
    {
        if (node == null) yield break;
        yield return node;
        foreach (ConstructionNode child in node.Children)
        foreach (ConstructionNode item in Nodes(child))
            yield return item;
    }
}
