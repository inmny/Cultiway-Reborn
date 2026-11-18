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

    /// <summary>状态及恢复的独立需求；旧请求仍使用原Work，新规则明确加上此项。</summary>
    public double ContactEffectsWork { get; }

    /// <summary>接触击退与换位的独立需求；第十三版起显式计入。</summary>
    public double ContactDisplacementWork { get; }

    /// <summary>附加伤害及有限随机、条件结果的需求，第十四版起计入。</summary>
    public double ContactResultsWork { get; }

    /// <summary>球形范围与介质接收者的分送需求，第十五版起计入。</summary>
    public double ContactSpreadWork { get; }

    /// <summary>范围推拉按世界半径面积、速度及存在时间计算的需求；第十九版起计入。</summary>
    public double RadialMotionWork { get; }

    /// <summary>主体、明确世界容量及留置后侵蚀的独立需求，第二十八版起计入。</summary>
    public double ErosionWork { get; }

    /// <summary>过程的局部变速需求；第三十一版起独立计入。</summary>
    public double ProcessTimeWork { get; }

    /// <summary>真实部位承载映像传递的额外需求，第二十九版起计入。</summary>
    public double CorrespondenceWork { get; }

    /// <summary>门的半径、门距和真实通行上限带来的需求，独立于生成材料费用。</summary>
    public double SpatialMappingWork { get; }

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
        var contactEffects = new Dictionary<int, double>();
        var contactDisplacement = new Dictionary<int, double>();
        var contactResults = new Dictionary<int, double>();
        var contactSpreads = new Dictionary<int, double>();
        var onceSpreadChannels = new Dictionary<string, double>(StringComparer.Ordinal);
        double work = plan.Body == null ? 0 : .5d + plan.Body.Duration * .25d;
        work += plan.Body?.MovementWork ?? 0d;
        if (plan.Body != null)
            foreach (var relation in plan.Body.Construction.Relations)
                if (relation.Kind is BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect or BodyRelationKind.Disarm)
                    work += (relation.UntilWorldTime - relation.FromWorldTime) * relation.ControlStrength *
                            (relation.Kind == BodyRelationKind.Disarm ? 2d : relation.Kind == BodyRelationKind.Deflect ? 1.5d : 1d) *
                            (relation.MoveTarget ? 2d * (1d + relation.CollisionDamageScale) : 1d) *
                            (relation.RotateTarget ? 1.5d : 1d);
        if (plan.Body?.ForceResponse != null)
            work += plan.Body.ForceResponse.Capacity * .1d + plan.Body.ForceResponse.Routing.Work;
        if (plan.Body?.Construction.MayContact == true)
        {
            for (int i = 0; i < plan.Body.StageCount; i++)
            {
                CompiledBodyStage stage = plan.Body.Stages[i];
                CompiledConstructionImpact impact = stage.Impact ?? plan.Impact;
                AddGroup(plan.Nodes.Length + i, stage.From, stage.Until, impact, 1f);
                foreach (var effect in impact.WorldEffects)
                    work += effect.MaximumTiles * .001d * (1d + effect.Duration * .1d) *
                            Repeat(effect.Contact, stage.Until - stage.From);
            }
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
            if (node.RadialMotion != null)
                RadialMotionWork += Math.PI * node.RadialMotion.Radius * node.RadialMotion.Radius *
                                    Math.Abs(node.RadialMotion.Speed) * span * .1d;
            bool material = node.FragmentSource < 0 && node.RemainderRoot < 0 && node.Resource.Meshes.Length > 0;
            double volume = material ? node.Volume * scales[i] : 0d;
            if (material) Materials++;
            Volume += volume;
            VolumeSeconds += volume * span;
            work += volume * (1d + span * .25d);
            foreach (var operation in node.EventOperations)
            {
                if (operation.Parameters is CompiledErosion erosion)
                {
                    ErosionWork += ErosionDemand(erosion, volume);
                    ProcessTimeWork += ErosionTimeDemand(erosion, volume,
                        erosion.World == null ? Math.Max(0d, node.Presence.y - Math.Max(node.Presence.x, operation.At)) : double.PositiveInfinity);
                }
                else if (operation.Parameters is CompiledErosionRetiming retiming)
                    ProcessTimeWork += retiming.World.MaximumVolume * (.08d + (retiming.Pace?.AdjustmentWork(double.PositiveInfinity) ?? 0d) * .25d);
            }
            if (node.Residue?.Erosion is { } residue)
            {
                ErosionWork += ErosionDemand(residue, volume);
                ProcessTimeWork += ErosionTimeDemand(residue, volume, node.Residue.Lifetime > 0 ? node.Residue.Lifetime : double.PositiveInfinity);
            }
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
            if (node.Correspondence is { } relation)
            {
                double window = Math.Max(0d, Math.Min(span, relation.ActiveUntil) - relation.ActiveFrom);
                double channels = relation.DamageFraction;
                foreach (var effect in new[] { ConstructionCorrespondenceEffects.Restoration, ConstructionCorrespondenceEffects.Status, ConstructionCorrespondenceEffects.Force })
                    if ((relation.Effects & effect) != 0) channels += .5d;
                CorrespondenceWork += control * node.Strength * window * channels * .25d;
            }
            foreach (CompiledEventOperation operation in node.EventOperations)
                if (operation.Ray != null)
                {
                    Vector3 rayScale = layouts[i].lossyScale;
                    double length = operation.Ray.Length * Mathf.Max(Mathf.Abs(rayScale.x), Mathf.Abs(rayScale.y), Mathf.Abs(rayScale.z));
                    double seconds = Math.Max(0, node.Presence.y - Math.Max(node.Presence.x, operation.At));
                    work += control * length * (.1d + seconds * .025d +
                                                operation.Ray.TurnRate * seconds / 3600d);
                }
            work += control * node.Spin.magnitude *
                Math.Max(0, node.Presence.y - Math.Max(node.Presence.x, node.SpinAt)) / 360d;
            Movement(node.Flight, node.FlyAt);
            Conduction(node.Conduction);
            foreach (CompiledEventOperation operation in node.EventOperations)
            {
                Movement(operation.Flight, operation.At);
                Conduction(operation.Conduction);
                if (operation.Propagation != null)
                    work += operation.Propagation.MaximumCells *
                        (operation.Propagation.CellCost + operation.Propagation.Height * .01d);
                if (operation.Kind == ConstructionOperationKind.Spin)
                    work += control * operation.Vector.magnitude *
                        Math.Max(0, node.Presence.y - Math.Max(node.Presence.x, operation.At)) / 360d;
                if (operation.Kind == ConstructionOperationKind.Deform)
                {
                    var motions = node.EventOperations.Select(action => action.Motion).Append(node.Motion)
                        .Where(motion => motion != null).ToArray();
                    var transition = node.Resource.Rig.TransitionRates(operation.Motion, motions, operation.BlendSeconds);
                    work += control * operation.BlendSeconds * (transition.Angular / 360d +
                        transition.Linear * Math.Pow(scales[i], 1d / 3d) * .1d);
                }
                if (operation.WorldMatter != null) work += operation.WorldMatter.MaximumVolume * .1d;
            }

            foreach (var change in node.Changes)
                if (change.WorldMatter != null)
                    work += change.WorldMatter.MaximumVolume * .1d;
            if (node.AffectsActors)
            {
                double from = Math.Max(node.Presence.x, node.Active.x), until = Math.Min(node.Presence.y, node.Active.y);
                if (node.ContactGroup == plan.Nodes.Length && plan.Body != null)
                    for (int stage = 0; stage < plan.Body.StageCount; stage++)
                        AddGroup(node.ContactGroup + stage,
                            Math.Max(from, node.BodyWindow || stage > 0 ? plan.Body.Stages[stage].From : 0f),
                            Math.Min(until, node.BodyWindow ? plan.Body.Stages[stage].Until :
                                stage + 1 == plan.Body.StageCount ? plan.Lifetime : plan.Body.Stages[stage + 1].From),
                            node.Impact ?? (node.BodyWindow ? plan.Body.Stages[stage].Impact : null) ?? plan.Impact,
                            node.Strength);
                else AddGroup(node.ContactGroup, from, until, node.Impact ?? plan.Impact, node.Strength);
            }
            if (node.ContactRole == ConstructionContactRole.Barrier) work += power * node.Strength;
            if (node.AffectsActors)
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

        foreach (var step in plan.Steps)
        {
            if (step.MappingEntrance < 0) continue;
            double meters = Vector3.Distance(layouts[step.MappingEntrance].GetColumn(3), layouts[step.MappingExit].GetColumn(3));
            if (step.Action.Kind == ConstructionOperationKind.MapActor)
            {
                var mapping = ((float Radius, int? Capacity, float Wait))step.Action.Parameters;
                SpatialMappingWork += mapping.Radius * mapping.Radius * .08d +
                                      (mapping.Capacity ?? 1) * (.3d + meters * .2d + mapping.Wait * .22d);
            }
            else
            {
                var radius = (float)step.Action.Parameters;
                SpatialMappingWork += radius * radius * .08d + .3d + meters * .2d;
            }
        }

        foreach (var group in groups)
        {
            double repeat = Repeat(plan.ContactPolicies[group.Key], group.Value.Until - group.Value.From);
            ContactWork += group.Value.Force * repeat;
            ContactEffectsWork += contactEffects[group.Key] * repeat;
            ContactDisplacementWork += contactDisplacement[group.Key] * repeat;
            ContactResultsWork += contactResults[group.Key] * repeat;
            ContactSpreadWork += contactSpreads[group.Key] * repeat;
        }
        ContactSpreadWork += onceSpreadChannels.Values.Sum();
        ContactGroups = groups.Keys.Select(id => id >= plan.Nodes.Length ? plan.Nodes.Length : id).Distinct().Count();
        // 选取距离与作用形状分别维护；更远投送保留相应控制预算，不扩大接触面。
        float range = plan.Use.Target == SkillLibV3.ActiveAbilities.ActiveAbilityTargetMode.Self
            ? 0f
            : plan.Reach.Selection();
        Work = (work + ContactWork) * (1d + range / 120d);
        ContactEffectsWork *= 1d + range / 120d;
        ContactDisplacementWork *= 1d + range / 120d;
        ContactResultsWork *= 1d + range / 120d;
        ContactSpreadWork *= 1d + range / 120d;
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
            double effects = impact.AgeYears * (double)strength;
            effects += impact.ReleaseGrab switch
            {
                ConstructionGrabRelease.Own    => .5d,
                ConstructionGrabRelease.Rescue => .5d + power * strength,
                _                              => 0d
            };
            if (impact.EquipmentErosion != null)
                effects += impact.EquipmentErosion.Slots.Count * impact.EquipmentErosion.Fraction * 100d * strength;
            if (impact.EquipmentRepair != null)
                effects += impact.EquipmentRepair.Durability * .1d * strength;
            if (impact.Signal != null)
                effects += .25d + impact.Signal.Duration * .25d * strength;
            if (impact.InventoryAging != null)
                effects += impact.InventoryAging.MaximumItems * (double)impact.InventoryAging.Seconds * strength /
                           Const.TimeScales.SecPerYear;
            foreach (CompiledConstructionStatus status in impact.Statuses)
                effects += status.Duration * (.25d +
                    (status.Stats?.Values.Sum(value => Math.Abs((double)value)) ?? 0d) * .1d +
                    (status.TargetStatRatios?.Values.Sum(value => Math.Abs((double)value)) ?? 0d) * power * strength +
                    (status.Tick?.ForcePerSecond ?? 0f) * power * strength);
            if (impact.Restoration != null)
            {
                CompiledConstructionRestoration restoration = impact.Restoration;
                effects += (restoration.Health + restoration.HealthPerForce * power * strength +
                            restoration.HealthPerSecond * restoration.Duration) * .1d +
                    (restoration.CleanseCount + restoration.RemovalMaximum) * .5d;
            }
            double displacement = impact.Knockback == null ? 0d :
                impact.Knockback.Distance * .25d + impact.Knockback.Height * .5d;
            if (impact.Exchange != null)
                displacement += impact.Exchange.Chance * (1d + impact.Exchange.WaitSeconds + plan.Reach.Selection() * .05d);
            double results = DamageWork(impact.AdditionalDamage, power, strength);
            if (impact.RandomContact != null) results += impact.RandomContact.Chance * impact.RandomContact.Outcomes.Max(outcome => OutcomeWork(outcome, power, strength));
            foreach (var rule in impact.ConditionalContacts)
                results += Math.Max(OutcomeWork(rule.Outcome, power, strength), OutcomeWork(rule.Otherwise, power, strength)) +
                    Math.Max(OutcomeWork(rule.OnKill, power, strength), OutcomeWork(rule.OnSurvive, power, strength));
            // 球形按平面覆盖预算，介质按明确接收者上限；同施放通道一次的需求单独合并。
            double sphereWork = impact.Spreads.Where(spread => spread.HasEffect && spread.Medium == null).Sum(spread =>
                Math.Max(1d, Math.PI * spread.Radius * spread.Radius / 4d) *
                (OutcomeWork(spread.Outcome, power, strength) + spread.BuildingDamagePerForce * power * strength * .1d));
            double spreading = sphereWork;
            foreach (var spread in impact.Spreads.Where(spread => spread.HasEffect && spread.Medium != null))
            {
                var medium = spread.Medium;
                double inherited = medium.InheritContactEffects ? effects + displacement + results +
                    Math.Max(0d, force - power * strength * impact.DamageScale) + sphereWork : 0d;
                double mediumWork = medium.MaximumTargets * Math.Max(OutcomeWork(spread.Outcome, power, strength) + inherited,
                    spread.BuildingDamagePerForce * power * strength * .1d + (medium.InheritContactEffects ? sphereWork : 0d));
                if (medium.OncePerCast)
                    onceSpreadChannels[medium.Channel] = Math.Max(onceSpreadChannels.TryGetValue(medium.Channel, out double oldMedium) ? oldMedium : 0d, mediumWork);
                else spreading += mediumWork;
            }
            contactSpreads[id] = Math.Max(contactSpreads.TryGetValue(id, out double previousSpreading) ? previousSpreading : 0d, spreading);
            contactResults[id] = Math.Max(contactResults.TryGetValue(id, out double previousResults) ? previousResults : 0d, results);
            contactDisplacement[id] = Math.Max(contactDisplacement.TryGetValue(id, out double previousDisplacement) ? previousDisplacement : 0d, displacement);
            contactEffects[id] = Math.Max(contactEffects.TryGetValue(id, out double previousEffects) ? previousEffects : 0d, effects);
            if (groups.TryGetValue(id, out var old))
                groups[id] = (Math.Min(old.From, from), Math.Max(old.Until, until), Math.Max(old.Force, force));
            else groups.Add(id, (from, until, force));
        }
    }

    /// <summary>按实际主体规模或明确世界容量计算投入，不承诺材料必然接受。</summary>
    /// <param name="erosion">已编译过程。</param><param name="volume">实际主体体积。</param>
    private static double ErosionDemand(CompiledErosion erosion, double volume) =>
        (erosion.World?.MaximumVolume ?? volume) * erosion.Fraction * (1d + erosion.Seconds * .1d);

    private static double ErosionTimeDemand(CompiledErosion erosion, double volume, double until) => erosion.Pace == null ? 0d :
        (erosion.World?.MaximumVolume ?? volume) * erosion.Pace.AdjustmentWork(Math.Min(until, erosion.Pace.CompletionFor(erosion.Seconds))) * .25d;

    /// <summary>人物生命比例以本次本体量作参考预算；实际伤害仍在命中时读取人物生命。</summary>
    /// <param name="damage">实际编译伤害。</param><param name="power">整招量。</param><param name="strength">部位倍率。</param>
    private static double DamageWork(IReadOnlyList<CompiledConstructionContactDamage> damage, float power, float strength) =>
        damage.Sum(item => (power * strength * (item.PerForce + item.PerHealth + item.PerMaxHealth) + item.MinimumBonus) * .1d *
            (item.IgnoreDamageReduction ? 1.5d : 1d));

    /// <summary>结果使用普通作用的预算；互斥分支取较大结果，不把候选数当作实际命中次数。</summary>
    /// <param name="outcome">可空的普通结果。</param><param name="power">整招量。</param><param name="strength">部位倍率。</param>
    private static double OutcomeWork(CompiledConstructionContactOutcome outcome, float power, float strength)
    {
        if (outcome == null) return 0d;
        double work = DamageWork(outcome.Damage, power, strength);
        foreach (var status in outcome.Statuses)
            work += status.Duration * (.25d + (status.Stats?.Values.Sum(value => Math.Abs((double)value)) ?? 0d) * .1d +
                (status.TargetStatRatios?.Values.Sum(value => Math.Abs((double)value)) ?? 0d) * power * strength +
                (status.Tick?.ForcePerSecond ?? 0f) * power * strength);
        if (outcome.Restoration != null)
            work += (outcome.Restoration.Health + outcome.Restoration.HealthPerForce * power * strength +
                outcome.Restoration.HealthPerSecond * outcome.Restoration.Duration) * .1d +
                (outcome.Restoration.CleanseCount + outcome.Restoration.RemovalMaximum) * .5d;
        if (outcome.Knockback != null) work += outcome.Knockback.Distance * .25d + outcome.Knockback.Height * .5d;
        return work;
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
    internal const int ProcessTimeRules = 31;

    /// <summary>自主创作接入指定解除、解救、修复和讯号条件。</summary>
    internal const int ApplicationRules = 32;

    /// <summary>按已冻结规则合并真实接触需求，不改变旧请求的计算。</summary>
    /// <param name="measure">已编译的实际规模。</param><param name="rules">本次创作版本。</param>
    private static double Demand(ConstructionMeasure measure, int rules) => measure.Work +
        (rules >= 12 ? measure.ContactEffectsWork : 0d) + (rules >= 13 ? measure.ContactDisplacementWork : 0d) +
        (rules >= 14 ? measure.ContactResultsWork : 0d) + (rules >= 15 ? measure.ContactSpreadWork : 0d) +
        (rules >= 19 ? measure.RadialMotionWork : 0d) + (rules >= 28 ? measure.ErosionWork : 0d) +
        (rules >= 29 ? measure.CorrespondenceWork : 0d) + (rules >= 30 ? measure.SpatialMappingWork : 0d) +
        (rules >= ProcessTimeRules ? measure.ProcessTimeWork : 0d);

    /// <summary>以当前实际构造计算新创作的默认费用与恢复时间。</summary>
    /// <param name="definition">本次独占的新创作。</param><param name="cost">是否补费用。</param><param name="cooldown">是否补恢复时间。</param>
    /// <param name="contactRules">创作规则版本；十二版计状态及恢复，十三版计位移，十四版计附加与分支结果，十五版计范围和介质分送，十九版计范围推拉，三十一版计过程时间，省略沿原计算。</param>
    public static AbilityDefinition Price(AbilityDefinition definition, bool cost = true, bool cooldown = true, int contactRules = 0)
    {
        CompiledAbility plan = AbilityCompiler.Compile(definition, false);
        ConstructionMeasure measure = plan.Measure;
        double work = Demand(measure, contactRules);
        if (cost) definition.Cost = definition.Resource == "none" ? 0f : Normalize(definition.Resource, work);
        if (cooldown) definition.Cooldown = contactRules >= 12
            ? Math.Max(plan.Body?.Duration ?? 0f, (float)Math.Sqrt(work) * .25f)
            : measure.DefaultCooldown;
        definition.Calculation = new ConstructionCalculatedValues
        {
            Resource = definition.Resource,
            Demand = cost && definition.Resource != "none" ? work : definition.Cost,
            Recovery = definition.Cooldown
        };
        return definition;
    }

    /// <summary>改造命令依据前后实际权重调整需求；明确免费与零冷却保持为零。</summary>
    /// <param name="source">没有被修改的完整原构造。</param><param name="changed">本次独占的改造结果。</param>
    /// <param name="cost">是否计算费用，明确另行选择的费用可以保留。</param><param name="cooldown">是否计算恢复时间。</param>
    /// <param name="contactRules">本次创作的规则版本，旧请求按已冻结版本计算。</param>
    public static AbilityDefinition Reprice(AbilityDefinition source, AbilityDefinition changed, bool cost = true,
        bool cooldown = true, int contactRules = 0)
    {
        ConstructionMeasure original = AbilityCompiler.Compile(source, false).Measure;
        ConstructionMeasure modified = AbilityCompiler.Compile(changed, false).Measure;
        double before = Demand(original, contactRules);
        double after = Demand(modified, contactRules);
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
