using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只保存本次计算的未取整数值，实际支付始终读取正式Cost，退款始终读取已付本金。</summary>
public sealed class ConstructionCalculatedValues
{
    /// <summary>当时计算的实际资源通道。</summary>
    public string Resource;

    /// <summary>本次费用在整数资源取整前的需求。</summary>
    public double Demand;

    /// <summary>本次恢复时间在转换为单精度前的秒数。</summary>
    public double Recovery;
}

/// <summary>从实际展开计划取得的施放规模；不是命中承诺，也不依赖网格三角形数量。</summary>
public sealed class ConstructionMeasure
{
    /// <summary>初次生成的独立材料主体数，不把碎片和余量后段重复计为材料出生。</summary>
    public int Materials { get; }

    /// <summary>以定义体高为一计算的实际材料体积预算。</summary>
    public double Volume { get; }

    /// <summary>材料体积与存在世界秒数的乘积。</summary>
    public double VolumeSeconds { get; }

    /// <summary>没有生成材料的功能区域，在参考姿态中的平面作用预算。</summary>
    public double Area { get; }

    /// <summary>独立接触组数，身体与显化的共享组只计一次。</summary>
    public int ContactGroups { get; }

    /// <summary>预算中的重复作用单位，条件重入不冒充固定命中次数。</summary>
    public double ContactWork { get; }

    /// <summary>身体、材料、运动、作用和世界过程合成的资源需求权重。</summary>
    public double Work { get; }

    /// <summary>按当前规模给出的默认恢复世界秒数；不替代真实身体收势。</summary>
    public float DefaultCooldown { get; }

    /// <summary>只在编译边界统计已展开的真实资料，不读取人物或分配运行状态。</summary>
    /// <param name="plan">同一版本的完整计划。</param><param name="power">明确的整招作用倍率。</param>
    internal ConstructionMeasure(CompiledAbility plan, float power)
    {
        // 先统计身体与逐部位规模，再合并接触组，最后生成整体需求和恢复预算。
        var scales = new double[plan.Nodes.Length];
        var layouts = new Matrix4x4[plan.Nodes.Length];
        Matrix4x4 basis = plan.Binding?.FaceTarget == true ||
                          (plan.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector)
            ? Matrix4x4.Rotate(Quaternion.LookRotation(Vector3.right, Vector3.forward))
            : Matrix4x4.identity;
        var groups = new Dictionary<int, (double From, double Until, double Force)>();
        double work = plan.Body == null ? 0 : .5d + plan.Body.Duration * .25d;
        work += plan.Body?.MovementWork ?? 0d;
        if (plan.Body != null)
            foreach (var relation in plan.Body.Construction.Relations)
                if (relation.Kind is BodyRelationKind.Brace or BodyRelationKind.Grab)
                    work += (relation.UntilWorldTime - relation.FromWorldTime) * relation.ControlStrength;
        if (plan.Body?.ForceResponse != null)
            work += plan.Body.ForceResponse.Capacity * .1d + plan.Body.ForceResponse.Routing.Work;
        if (plan.Body?.Construction.MayContact == true)
        {
            AddGroup(plan.Nodes.Length, plan.Body.ActiveFrom, plan.Body.ActiveUntil, plan.Body.Impact ?? plan.Impact,
                1f);
            foreach (var effect in (plan.Body.Impact ?? plan.Impact).WorldEffects)
                work += effect.MaximumTiles * .001d * (1d + effect.Duration * .1d) *
                        Repeat(effect.Contact, plan.Body.ActiveUntil - plan.Body.ActiveFrom);
        }

        for (int i = 0; i < plan.Nodes.Length; i++)
        {
            CompiledForm node = plan.Nodes[i];
            double growth = Math.Max(1d,
                node.EventOperations.Where(operation => operation.Kind == ConstructionOperationKind.Grow)
                    .Select(operation => (double)operation.TargetScale).DefaultIfEmpty(1d).Max());
            scales[i] = Math.Pow(node.Scale * growth, 3) *
                        (node.Parent >= 0 && node.Activation?.Placement != ConstructionEventPlacement.Event
                            ? scales[node.Parent] * Math.Abs(node.Anchor.determinant)
                            : 1d);
            layouts[i] = (node.Parent >= 0 && node.Activation?.Placement != ConstructionEventPlacement.Event
                             ? layouts[node.Parent] * node.Anchor
                             : basis) *
                         Matrix4x4.TRS(node.Offset, node.Rotation, Vector3.one * (float)(node.Scale * growth));
            double span = Math.Max(0, node.Presence.y - node.Presence.x);
            bool material = node.FragmentSource < 0 && node.RemainderRoot < 0 && node.Resource.Meshes.Length > 0;
            double volume = material ? node.Volume * scales[i] : 0d;
            if (material) Materials++;
            Volume += volume;
            VolumeSeconds += volume * span;
            work += volume * (1d + span * .25d);
            if (node.Resource.Meshes.Length == 0 && node.Resource.Contacts.Length == 0) continue;
            double area = 0;
            if (node.Resource.Meshes.Length == 0)
            {
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                Vector3 scale = layouts[i].lossyScale;
                foreach (var shape in node.Resource.Contacts)
                {
                    Vector2 a = (Vector3)layouts[i].MultiplyPoint3x4(shape.A),
                        b = (Vector3)layouts[i].MultiplyPoint3x4(shape.B);
                    Vector2 radius = Vector2.one * shape.Radius *
                                     Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    minimum = Vector2.Min(minimum, Vector2.Min(a, b) - radius);
                    maximum = Vector2.Max(maximum, Vector2.Max(a, b) + radius);
                }

                area = Math.Max(0, maximum.x - minimum.x) * Math.Max(0d, maximum.y - minimum.y);
                Area += area;
                work += area * (.1d + span * .025d);
            }

            double carried = node.FragmentSource >= 0
                ? plan.Nodes[node.FragmentSource].Volume * scales[node.FragmentSource] /
                  plan.Nodes[node.FragmentSource].Fracture.Pieces.Length
                : volume;
            double control = Math.Max(Math.Max(carried, area * .1d), .05d);
            work += control * node.Spin.magnitude *
                Math.Max(0, node.Presence.y - Math.Max(node.Presence.x, node.SpinAt)) / 360d;
            Movement(node.Flight, node.FlyAt);
            Conduction(node.Conduction);
            foreach (CompiledEventOperation operation in node.EventOperations)
            {
                Movement(operation.Flight, operation.At);
                Conduction(operation.Conduction);
                if (operation.Kind == ConstructionOperationKind.Spin)
                    work += control * operation.Vector.magnitude *
                        Math.Max(0, node.Presence.y - Math.Max(node.Presence.x, operation.At)) / 360d;
                if (operation.WorldMatter != null) work += operation.WorldMatter.MaximumVolume * .1d;
            }

            foreach (var change in node.Changes)
                if (change.WorldMatter != null)
                    work += change.WorldMatter.MaximumVolume * .1d;
            if (node.ContactRole == ConstructionContactRole.Attack)
                AddGroup(node.ContactGroup, Math.Max(node.Presence.x, node.Active.x),
                    Math.Min(node.Presence.y, node.Active.y), node.Impact ?? plan.Impact, node.Strength);
            if (node.ContactRole == ConstructionContactRole.Barrier) work += power * node.Strength;
            if (node.ContactRole == ConstructionContactRole.Attack)
                foreach (var effect in (node.Impact ?? plan.Impact).WorldEffects)
                    work += Math.Min(effect.MaximumTiles, area > 0 ? Math.Ceiling(area) : effect.MaximumTiles) * .001d *
                            (1d + effect.Duration * .1d) * Repeat(effect.Contact, span);

            void Movement(CompiledFlight flight, float at)
            {
                if (flight == null) return;
                double seconds = Math.Max(0, node.Presence.y - Math.Max(node.Presence.x, at));
                work += control * (flight.Velocity.magnitude * seconds +
                                   flight.Acceleration.magnitude * seconds * seconds * .5d) * .1d;
                if (flight.Path != null)
                    work += control * flight.Path.TravelBudget(seconds, plan.TargetRange ?? 12f) * .1d;
            }

            void Conduction(CompiledConduction conduction)
            {
                if (conduction != null)
                    work += conduction.Pulses * (conduction.Range * .1d +
                                                 conduction.MaximumTargets * conduction.DamagePerTarget * .1d);
            }
        }

        foreach (var group in groups)
            ContactWork += group.Value.Force *
                           Repeat(plan.ContactPolicies[group.Key], group.Value.Until - group.Value.From);
        ContactGroups = groups.Count;
        // 选取距离与作用形状分别维护；更远投送保留相应控制预算，不扩大接触面。
        float range = plan.Use.Target == SkillLibV3.ActiveAbilities.ActiveAbilityTargetMode.Self
            ? 0f
            : plan.Reach.Selection();
        Work = (work + ContactWork) * (1d + range / 120d);
        if (double.IsNaN(Work) || double.IsInfinity(Work) || Work > float.MaxValue)
            throw new InvalidDataException("构造规模：材料、持续或作用预算超出可表示资源范围");
        DefaultCooldown = Math.Max(plan.Body?.Duration ?? 0f, (float)Math.Sqrt(Work) * .25f);

        void AddGroup(int id, double from, double until, CompiledConstructionImpact impact, float strength)
        {
            if (until < from) return;
            work += impact.ForceRouting?.Work ?? 0d;
            double force = power * strength * (impact.DamageScale +
                                               Math.Abs(impact.Force) * .1d *
                                               (impact.SlideSeconds > 0 ? impact.SlideSeconds : 1d) +
                                               (impact.Launch ? impact.Lift * .1d : 0d) + impact.StaggerSeconds +
                                               impact.BindSeconds + impact.StrainSeconds + impact.ExposureSeconds +
                                               (impact.Impulse?.magnitude ?? 0f) * .1d *
                                               (impact.SlideSeconds > 0 ? impact.SlideSeconds : 1d));
            if (groups.TryGetValue(id, out var old))
                groups[id] = (Math.Min(old.From, from), Math.Max(old.Until, until), Math.Max(old.Force, force));
            else groups.Add(id, (from, until, force));
        }
    }

    /// <summary>按明确间隔和次数上限定预算，重入仍由真实离开与进入决定。</summary>
    /// <param name="policy">同组实际规则。</param><param name="span">可能有效的世界秒数。</param>
    private static double Repeat(CompiledContactPolicy policy, double span)
    {
        double count = policy.Mode switch
        {
            ConstructionContactMode.Once => 1d,
            ConstructionContactMode.Interval => 1d + Math.Floor(Math.Max(0, span) / policy.Interval),
            _ => policy.MaximumPerTarget > 0 ? policy.MaximumPerTarget : 1d + Math.Max(0, span)
        };
        if (policy.MaximumPerTarget > 0) count = Math.Min(count, policy.MaximumPerTarget);
        if (policy.MaximumTotal > 0) count = Math.Min(count, policy.MaximumTotal);
        return count;
    }
}

/// <summary>创建和明确改造命令共用的需求计算；正式读取与登记不重写作者确定的费用。</summary>
public static class ConstructionPricing
{
    /// <summary>以当前实际构造计算新创作的默认费用与恢复时间。</summary>
    /// <param name="definition">本次独占的新创作。</param><param name="cost">是否补费用。</param><param name="cooldown">是否补恢复时间。</param>
    public static AbilityDefinition Price(AbilityDefinition definition, bool cost = true, bool cooldown = true)
    {
        ConstructionMeasure measure = AbilityCompiler.Compile(definition, false).Measure;
        if (cost) definition.Cost = definition.Resource == "none" ? 0f : Normalize(definition.Resource, measure.Work);
        if (cooldown) definition.Cooldown = measure.DefaultCooldown;
        definition.Calculation = new ConstructionCalculatedValues
        {
            Resource = definition.Resource,
            Demand = cost && definition.Resource != "none" ? measure.Work : definition.Cost,
            Recovery = definition.Cooldown
        };
        return definition;
    }

    /// <summary>改造命令依据前后实际权重调整需求；明确免费与零冷却保持为零。</summary>
    /// <param name="source">没有被修改的完整原构造。</param><param name="changed">本次独占的改造结果。</param>
    /// <param name="cost">是否计算费用，明确另行选择的费用可以保留。</param><param name="cooldown">是否计算恢复时间。</param>
    public static AbilityDefinition Reprice(AbilityDefinition source, AbilityDefinition changed, bool cost = true,
        bool cooldown = true)
    {
        double before = AbilityCompiler.Compile(source, false).Measure.Work;
        double after = AbilityCompiler.Compile(changed, false).Measure.Work;
        double ratio = before > 0 ? after / before : 1d;
        double demand = source.Calculation != null && source.Calculation.Resource == source.Resource &&
                        Normalize(source.Resource, source.Calculation.Demand) == source.Cost
            ? source.Calculation.Demand
            : source.Cost;
        double recovery = source.Calculation != null && Normalize(null, source.Calculation.Recovery) == source.Cooldown
            ? source.Calculation.Recovery
            : source.Cooldown;
        demand = cost ? demand * ratio : changed.Cost;
        recovery = cooldown ? recovery * Math.Sqrt(ratio) : changed.Cooldown;
        if (cost) changed.Cost = Normalize(changed.Resource, demand);
        if (cooldown) changed.Cooldown = Normalize(null, recovery);
        changed.Calculation = new ConstructionCalculatedValues
            { Resource = changed.Resource, Demand = demand, Recovery = recovery };
        return changed;
    }

    /// <summary>整数资源在需求边界向上取整；只消除整额附近的计算尾差，不抹掉微小正费用。</summary>
    /// <param name="resource">实际通道编号，空值表示普通秒数。</param><param name="value">计算出的有限需求。</param>
    private static float Normalize(string resource, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > float.MaxValue)
            throw new InvalidDataException("构造规模：改造后的费用或恢复时间超出可表示范围");
        bool integer = resource is "mana" or "stamina" || resource != null &&
            resource is not ("none" or "wakan" or "vigor" or "yao_power") &&
            ModClass.I.SkillV3.CastResourceLib.get(resource)?.IntegerAmount == true;
        if (!integer) return (float)value;
        double nearest = Math.Round(value);
        return (float)Math.Ceiling(nearest > 0 && Math.Abs(value - nearest) < .000001d ? nearest : value);
    }
}