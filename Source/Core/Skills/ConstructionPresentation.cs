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
    /// <param name="definition">当前完整资料。</param>
    private static string ResourceNames(AbilityDefinition definition)
    {
        string Name(string id) => L("Cultiway.Construction.Resource." + id);
        string[] names = new[] { definition.Resource }.Concat(definition.AdditionalResources).Select(Name).ToArray();
        return names.Length == 1
            ? names[0]
            : string.Join(
                T(definition.ResourceMode == Cultiway.Core.SkillLibV3.SkillCastResourceRequirementMode.AnyOf
                    ? "PaymentAny"
                    : "PaymentAll"), names);
    }

    /// <summary>说明当前完整资料，不储存可能因改造过时的说明副本。</summary>
    /// <param name="definition">当前独立草稿或正式定义。</param>
    /// <param name="plan">同一资料的实际编译结果。</param>
    public static string Explain(AbilityDefinition definition, CompiledAbility plan)
    {
        var lines = new List<string>
        {
            F("Basics", definition.Cost, ResourceNames(definition),
                plan.Lifetime, definition.Cooldown),
            plan.Nodes.Any(n => n.Resource.Meshes.Length > 0)
                ? F("Forms", plan.Nodes.Count(n => n.Resource.Meshes.Length > 0),
                    string.Join("、", plan.Nodes.Where(n => n.Resource.Meshes.Length > 0)
                        .Select(n => Material(n.Matter) + " " + Form(n.Resource.Description.Id)).Distinct()))
                : T("BodyOnly")
        };
        string[] distributions = Nodes(definition.Root).Where(node => node.Distribution != null)
            .Select(node => L("Cultiway.Construction.DistributionSource." + node.Distribution.Source)).Distinct().ToArray();
        if (distributions.Length > 0) lines.Add(F("Distributions", string.Join("、", distributions)));
        if (plan.Body != null) lines.Add(F("Body", plan.Body.ActiveFrom, plan.Body.ActiveUntil));
        if (plan.ContactDamageScale > 0f)
            lines.Add(F("Damage", string.Join("、",
                plan.Nodes.Where(n => n.AffectsActors || n.Conduction != null ||
                                      n.EventOperations.Any(operation => operation.Conduction != null))
                    .Select(n => (n.Impact ?? plan.Impact).Targets).Concat(plan.Body == null
                        ? Array.Empty<SkillLibV3.Usage.SkillUseTargetRelation>()
                        : new[] { (plan.Body.Impact ?? plan.Impact).Targets })
                    .Distinct().Select(relation => T("Target." + relation))), plan.ContactDamageScale));
        lines.AddRange(plan.Nodes.Where(node => node.RadialMotion != null).Select(node =>
            F("RadialMotion", Form(node.Resource.Description.Id), node.RadialMotion.Radius,
                node.RadialMotion.Speed, node.RadialMotion.Interval, T("Target." + node.RadialMotion.Targets))).Distinct());
        if (plan.ContactForce > 0f) lines.Add(F("Force", plan.ContactForce));
        if (plan.ContactStagger > 0f) lines.Add(F("Stagger", plan.ContactStagger));
        if (plan.ContactBind > 0f) lines.Add(F("Bind", plan.ContactBind));
        if (plan.ContactLaunch > 0f) lines.Add(F("Launch", plan.ContactLaunch));
        if (plan.HasBarrier) lines.Add(T("Barrier"));
        if (definition.Binding != null)
            lines.Add(F("Placement", L("Cultiway.Construction.Anchor." + definition.Binding.Anchor),
                T(definition.Binding.Follow ? "Following" : "Fixed")));
        if (plan.ConcurrentLimit > 0) lines.Add(F("ConcurrentLimit", plan.ConcurrentLimit));
        if (definition.Binding?.Anchor == AbilityAnchor.Between)
            lines.Add(F("BetweenFraction", definition.Binding.Fraction ?? .5f));
        if (definition.Binding?.ContactAtDestination == true) lines.Add(T("DestinationOnly"));
        if (plan.Nodes.Any(node => node.SourceLoss == ConstructionSourceLoss.Continue)) lines.Add(T("IndependentSourceLoss"));
        if (definition.Binding?.Anchor == AbilityAnchor.Target && !definition.Binding.Follow &&
            definition.Root != null && !definition.Root.Operations.Any(operation => operation.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Ray))
            lines.Add(T("DirectPlacement"));
        var processes = new List<string>();
        foreach (ConstructionNode node in Nodes(definition.Root))
        {
            if (node.Activation != null) processes.Add(F("Appears", Form(node.Form), Condition(node.Activation)));
            foreach (ConstructionOperation operation in node.Operations)
            {
                string action = T("Operation." + operation.Kind);
                if (operation.Kind == ConstructionOperationKind.ConvertMatter)
                    action = F("Convert", Material(AbilityMatters.Resolve(operation.Matter, operation.State, "法术说明")));
                if (operation.Kind == ConstructionOperationKind.Fly && operation.TurnRate > 0f) action = T("Homing");
                if (operation.Kind == ConstructionOperationKind.Fly && operation.Path != null) action = T("Curve");
                if (operation.Kind == ConstructionOperationKind.Fly && operation.Ground == ConstructionGroundResponse.Follow) action = T("GroundFlight");
                if (operation.Kind == ConstructionOperationKind.Fly && operation.WorldVelocity && operation.Acceleration?[2] < 0f) action = T("Falling");
                if (operation.Ray != null) action = RayDescription(operation.Ray);
                if (operation.Kind == ConstructionOperationKind.Leave)
                    action = operation.Residue?.Lifetime > 0f
                        ? F("Residue", operation.Residue.Lifetime)
                        : T("ResiduePermanent");
                string when = operation.When != null
                    ? Condition(operation.When)
                    : operation.Trigger != ConstructionChangeTrigger.Time
                        ? T("Condition." + operation.Trigger)
                        : F("At", operation.At);
                if (operation.When != null && operation.At > 0f) when = F("Gated", operation.At, when);
                processes.Add(F("Process", when, Form(node.Form), action));
            }
        }

        foreach (ConstructionStep step in definition.Steps.Where(step => step.Operation.Ray != null))
        {
            var operation = step.Operation;
            string when = operation.When != null ? Condition(operation.When) : F("At", operation.At);
            processes.Add(F("Process", when, Form(plan.Nodes[plan.Steps.Single(value => value.Id == step.Id).Node].Resource.Description.Id), RayDescription(operation.Ray)));
        }

        string[] unique = processes.Distinct().ToArray();
        lines.AddRange(unique.Take(12));
        if (unique.Length > 12) lines.Add(F("More", unique.Length - 12));
        if (definition.Parent != null)
            lines.Add(F("Parent", ChangeName(definition.Parent.Change), definition.Parent.Revision));
        return string.Join("\n", lines);
    }

    /// <summary>条件说明与真实时钟、阈值和额外等待一致。</summary>
    /// <param name="condition">实际开始条件。</param>
    private static string Condition(ConstructionCondition condition)
    {
        string text = T("Condition." + condition.Trigger);
        if (condition.Threshold.HasValue) text += F("Threshold", condition.Threshold.Value);
        if (condition.Delay > 0f) text += F("Delay", condition.Delay);
        return text;
    }

    /// <summary>明确基础长度随主体尺寸缩放，转向和目标更新继续实际世界时钟。</summary>
    /// <param name="ray">同一份保存参数。</param>
    private static string RayDescription(ConstructionRay ray) => F("Ray", ray.Length,
        T(ray.FollowTarget ? "RayFollowing" : "RayFixed"), ray.TurnRate);

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
        "复生" => "Repeat",
        "显化" => "Project",
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
