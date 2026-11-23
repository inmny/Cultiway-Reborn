using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>已经完成的有限热量转移与材料烧损，不由显示回调触发。</summary>
public readonly struct ConstructedBurning
{
    /// <summary>接收材料的施放或世界编号，另一项为零。</summary>
    public readonly long CastId, WorldId;

    /// <summary>本次真正传热的活动施放或世界主体，过程性烧损为零。</summary>
    public readonly long SourceCastId, SourceWorldId;

    /// <summary>实际部位，世界主体为负一。</summary>
    public readonly int Node;

    /// <summary>真实加热来源。</summary>
    public readonly long Source;

    /// <summary>加热、冷却或实际烧损。</summary>
    public readonly string Kind;

    /// <summary>已经确认的位置。</summary>
    public readonly Vector3 Point;

    /// <summary>已经确认的共同世界时间。</summary>
    public readonly double WorldTime;

    /// <summary>本次转移量、当前热量、剩余材料和累计烧损。</summary>
    public readonly double Transferred, Heat, Volume, Burned;

    /// <summary>确认后的燃烧状态。</summary>
    public readonly bool Lit;

    /// <summary>从已经提交的实际状态建立结果。</summary>
    /// <param name="cast">施放编号。</param><param name="world">独立世界编号。</param><param name="node">部位。</param>
    /// <param name="kind">结果类别。</param><param name="point">真实接触点。</param><param name="time">世界时间。</param>
    /// <param name="amount">实际转移。</param><param name="volume">尚存材料。</param><param name="burning">实际燃烧状态。</param>
    /// <param name="sourceCast">本次实际传热施放。</param><param name="sourceWorld">本次实际世界热源。</param>
    internal ConstructedBurning(long cast, long world, int node, string kind, Vector3 point, double time,
        double amount, double volume, AbilityBurning burning, long sourceCast = 0, long sourceWorld = 0)
    {
        CastId = cast;
        WorldId = world;
        Node = node;
        Kind = kind;
        Point = point;
        WorldTime = time;
        Transferred = amount;
        Heat = burning.Heat;
        Volume = volume;
        Burned = burning.Burned;
        Lit = burning.Lit;
        Source = burning.Source;
        SourceCastId = sourceCast;
        SourceWorldId = sourceWorld;
    }
}

/// <summary>有限燃料的接触、传播与熄灭共用普通形状和时间推进。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>世界热源及接收者的实际形状缓冲。</summary>
    private readonly List<WorldThermalContact> thermalWood = new();

    /// <summary>世界材料提交后共用同一结果入口。</summary>
    /// <param name="id">世界编号。</param><param name="kind">结果类别。</param><param name="point">真实位置。</param>
    /// <param name="clock">世界时间。</param><param name="amount">实际变化。</param><param name="material">确认后的材料。</param>
    /// <param name="sourceCast">本次传热施放。</param><param name="sourceWorld">本次世界热源。</param>
    internal static void ReportWorldBurning(long id, string kind, Vector3 point, double clock, double amount,
        AbilityMaterial material, long sourceCast = 0, long sourceWorld = 0) =>
        BurningConfirmed?.Invoke(new ConstructedBurning(0, id, -1, kind, point, clock, amount, material.Volume,
            material.Burning, sourceCast, sourceWorld));

    /// <summary>真实加热、冷却或烧损完成后通知。</summary>
    public static event Action<ConstructedBurning> BurningConfirmed;

    /// <summary>材料接触的明确类型，未定义的相遇仍使用原交锋规则。</summary>
    private enum ThermalReaction
    {
        /// <summary>没有额外材料反应。</summary>
        None,

        /// <summary>从有限来源加热尚未点燃的木体。</summary>
        Ignite,

        /// <summary>水吸收木体的真实热量。</summary>
        CoolWood
    }

    /// <summary>取得同一部位可向外传出的有限热量。</summary>
    /// <param name="shape">实际热源形状。</param>
    private static double AvailableHeat(in ShapeMotion shape) =>
        shape.To.Material?.Id == "fire" && shape.To.Material.State == "energy"
            ? shape.Running.Cast.ContactSweepForce(shape.To.Node)
            : shape.Running.Cast.Burning(shape.To.Node).Transferable(shape.Running.Cast.RemainingVolume(shape.To.Node));

    /// <summary>只从热源查询接收者，普通木格彼此不会枚举传热。</summary>
    /// <param name="source">候选热源。</param><param name="target">候选接收者。</param>
    private static ThermalReaction ThermalPair(in ShapeMotion source, in ShapeMotion target)
    {
        if (source.Running == target.Running && source.To.Node == target.To.Node) return ThermalReaction.None;
        AbilityBurning burning = source.Running.Cast.Burning(source.To.Node);
        if (burning.Heat > 0 && target.To.Material?.Id == "water" && target.To.Material.State == "liquid")
            return ThermalReaction.CoolWood;
        if (AvailableHeat(source) > 0 && target.To.Material != null && AbilityBurning.CanBurn(target.To.Material) &&
            target.Running.Cast.Burning(target.To.Node).Needed(target.Running.Cast.RemainingVolume(target.To.Node)) > 0)
            return ThermalReaction.Ignite;
        return ThermalReaction.None;
    }

    /// <summary>同一接触时点改变双方实际数量，再发布结果。</summary>
    /// <param name="hit">共同最早的材料接触。</param>
    private static void ResolveThermal(in ContactChoice hit)
    {
        if (hit.WorldRecord > 0)
        {
            ResolveWorldThermal(hit);
            return;
        }

        ShapeMotion source = hit.Left, target = hit.Right.Value;
        if (ThermalPair(source, target) != hit.Reaction)
        {
            source = hit.Right.Value;
            target = hit.Left;
        }

        if (ThermalPair(source, target) != hit.Reaction || !ReadQuenchingShape(source, out var sourceShape) ||
            !ReadQuenchingShape(target, out var targetShape) ||
            !AbilityPairSweep.Find(sourceShape, sourceShape, targetShape, targetShape, out _, out _, out _)) return;
        if (hit.Reaction == ThermalReaction.CoolWood)
        {
            double heat = source.Running.Cast.Burning(source.To.Node).Heat;
            double removed = target.Running.Cast.EvaporateWater(target.To.Node, target.To.Shape,
                heat / AbilityQuenching.ForcePerVolume, hit.Point);
            double spent = removed > 0 ? Math.Min(heat, removed * AbilityQuenching.ForcePerVolume) : heat;
            source.Running.Cast.CoolWood(source.To.Node, spent);
            if (removed > 0) AddSteam(hit.Point, removed, simulation.contactTime);
            EmitBurning(source.Running, source.To.Node, "cool", hit.Point, spent);
            return;
        }

        double available = AvailableHeat(source);
        var fromBurn = source.Running.Cast.Burning(source.To.Node);
        long owner = fromBurn.Source > 0 ? fromBurn.Source : source.Running.Owner(source.To.Node).Id;
        double amount = target.Running.Cast.HeatWood(target.To.Node, available, owner, hit.Point);
        if (amount <= 0) return;
        SpendHeat(source, amount);
        EmitBurning(target.Running, target.To.Node, "heat", hit.Point, amount, sourceCast: source.Running.Id);
    }

    /// <summary>火体消费原攻击余力，燃烧木体只消费自身额外热量。</summary>
    /// <param name="source">真实热源。</param><param name="amount">接收者实际取得的数量。</param>
    private static void SpendHeat(in ShapeMotion source, double amount)
    {
        if (source.To.Material?.Id == "fire") source.Running.Cast.ConsumeForce(source.To.Node, (float)amount);
        else source.Running.Cast.CoolWood(source.To.Node, amount);
    }

    /// <summary>活动热源只查询附近世界接收者；世界热源另从自身查询活动索引。</summary>
    /// <param name="shape">本次真实活动主体。</param>
    /// <param name="queue">提交已取得的世界形状和材料编号。</param>
    private void FindWorldThermal(in ShapeMotion shape, Action<ContactSweep, ContactSweepTarget> queue)
    {
        if (AvailableHeat(shape) > 0)
        {
            bool continuous = shape.Continuous && shape.Running.Cast.StartHeat(shape.To.Node) > 0;
            WorldMaterialService.QueryWood(shape.Bounds, false, thermalWood);
            foreach (var target in thermalWood)
                if (target.Burning.Needed(target.Volume) > 0)
                    queue(new ContactSweep(continuous ? shape.From : shape.To, shape.To, target.Shape, target.Shape,
                            continuous ? 0f : 1f), new ContactSweepTarget(shape, worldRecord: target.Id,
                            worldShape: target.Shape, reaction: ThermalReaction.Ignite));
        }

        if (shape.Running.Cast.Burning(shape.To.Node).Heat <= 0) return;
        WorldMaterialService.QueryQuenchingWater(shape.Bounds, quenchingWater);
        foreach (var target in quenchingWater)
            queue(new ContactSweep(shape.Continuous ? shape.From : shape.To, shape.To, target.Shape, target.Shape,
                    shape.Continuous ? 0f : 1f), new ContactSweepTarget(shape, worldRecord: target.Record,
                    worldShape: target.Shape, reaction: ThermalReaction.CoolWood));
    }

    /// <summary>世界热源主动查询附近的活动水与未燃木体，冷材料之间不枚举。</summary>
    /// <param name="queue">提交已取得的世界形状和材料编号。</param>
    private void FindWorldHeatSources(Action<ContactSweep, ContactSweepTarget> queue)
    {
        WorldMaterialService.HotWood(thermalWood);
        foreach (var source in thermalWood)
        {
            shapeIndex.Query(AbilityContactIndex.Swept(source.Shape, source.Shape), nearby);
            foreach (int index in nearby)
            {
                var target = shapes[index];
                if (target.To.Node < 0) continue;
                var matter = target.To.Material;
                ThermalReaction reaction = matter?.Id == "water" && matter.State == "liquid"
                    ? ThermalReaction.CoolWood
                    : matter != null && AbilityBurning.CanBurn(matter) &&
                      source.Burning.Transferable(source.Volume) > 0 &&
                      target.Running.Cast.Burning(target.To.Node)
                          .Needed(target.Running.Cast.RemainingVolume(target.To.Node)) > 0
                        ? ThermalReaction.Ignite
                        : ThermalReaction.None;
                if (reaction == ThermalReaction.None) continue;
                queue(new ContactSweep(target.Continuous ? target.From : target.To, target.To, source.Shape,
                        source.Shape, target.Continuous ? 0f : 1f), new ContactSweepTarget(target, worldRecord: source.Id,
                        worldShape: source.Shape, reaction: reaction, worldSource: true));
            }
        }
    }

    /// <summary>独立材料与活动主体在同一确认时点交换热量，回调发生在双方扣量后。</summary>
    /// <param name="hit">最早的真实接触。</param>
    private static void ResolveWorldThermal(in ContactChoice hit)
    {
        var active = hit.Left;
        if (!ReadQuenchingShape(active, out var actual)) return;
        double clock = simulation.contactTime;
        if (hit.WorldSource)
        {
            if (!WorldMaterialService.ReadWood(hit.WorldRecord, actual, out var source)) return;
            if (hit.Reaction == ThermalReaction.CoolWood)
            {
                if (active.Running.Cast.Matter(active.To.Node).Id != "water" ||
                    active.Running.Cast.Matter(active.To.Node).State != "liquid") return;
                double heat = source.Material.Burning.Heat;
                double removed = active.Running.Cast.EvaporateWater(active.To.Node, active.To.Shape,
                    heat / AbilityQuenching.ForcePerVolume, hit.Point);
                if (heat <= 0) return;
                double spent = removed > 0 ? Math.Min(heat, removed * AbilityQuenching.ForcePerVolume) : heat;
                source.Material.Burning.Cool(spent);
                if (removed > 0) AddSteam(hit.Point, removed, clock);
                WorldMaterialService.ReportBurning(source, "cool", hit.Point, clock, spent);
            }
            else
            {
                double available = source.Material.Burning.Transferable(source.Material.Volume);
                long owner = source.Material.Burning.Source > 0 ? source.Material.Burning.Source : source.Controller;
                double amount = active.Running.Cast.HeatWood(active.To.Node, available, owner, hit.Point);
                if (amount <= 0) return;
                source.Material.Burning.Cool(amount);
                EmitBurning(active.Running, active.To.Node, "heat", hit.Point, amount, sourceWorld: source.Id);
            }

            return;
        }

        if (hit.Reaction == ThermalReaction.CoolWood)
        {
            double heat = active.Running.Cast.Burning(active.To.Node).Heat;
            if (heat <= 0) return;
            double removed = WorldMaterialService.EvaporateWater(hit.WorldRecord, hit.WorldShape.Shape, actual,
                heat / AbilityQuenching.ForcePerVolume, evaporatedSources, out bool touched);
            if (!touched) return;
            double spent = removed > 0 ? Math.Min(heat, removed * AbilityQuenching.ForcePerVolume) : heat;
            active.Running.Cast.CoolWood(active.To.Node, spent);
            if (removed > 0) AddSteam(hit.Point, removed, clock);
            EmitBurning(active.Running, active.To.Node, "cool", hit.Point, spent);
            return;
        }

        var state = active.Running.Cast.Burning(active.To.Node);
        long from = state.Source > 0 ? state.Source : active.Running.Owner(active.To.Node).Id;
        double accepted =
            WorldMaterialService.HeatWood(hit.WorldRecord, actual, AvailableHeat(active), from, hit.Point);
        if (accepted <= 0) return;
        SpendHeat(active, accepted);
        if (WorldMaterialService.ReadWood(hit.WorldRecord, actual, out var target))
            WorldMaterialService.ReportBurning(target, "heat", hit.Point, clock, accepted,
                sourceCast: active.Running.Id);
    }

    /// <summary>完整世界轮次完成后报告实际累计烧损，结束的部位也有最后结果。</summary>
    /// <param name="running">真实施放。</param><param name="clock">完成时间。</param>
    private static void ConfirmBurnProgress(RunningCast running, double clock)
    {
        if (BurningConfirmed == null) return;
        running.ReportedBurned ??= new double[running.Cast.Plan.FormCount];
        long epoch = worldGeneration;
        for (int node = 0; node < running.ReportedBurned.Length; node++)
        {
            var burning = running.Cast.Burning(node);
            double delta = burning.Burned - running.ReportedBurned[node];
            running.ReportedBurned[node] = burning.Burned;
            if (delta <= 0) continue;
            BurningConfirmed?.Invoke(new ConstructedBurning(running.Id, 0, node, "burn",
                running.Cast.BurningPoint(node), clock, delta,
                running.Cast.RemainingVolume(node), burning));
            if (epoch != worldGeneration) return;
        }
    }

    /// <summary>发布已经完成的材料状态，显示不决定燃烧。</summary>
    /// <param name="running">真实施放。</param><param name="node">真实部位。</param>
    /// <param name="kind">结果类别。</param><param name="point">接触位置。</param><param name="amount">实际转移量。</param>
    /// <param name="sourceCast">本次实际传热施放。</param><param name="sourceWorld">本次实际世界热源。</param>
    private static void EmitBurning(RunningCast running, int node, string kind, Vector3 point, double amount,
        long sourceCast = 0, long sourceWorld = 0) =>
        BurningConfirmed?.Invoke(new ConstructedBurning(running.Id, 0, node, kind, point, simulation.contactTime,
            amount, running.Cast.RemainingVolume(node), running.Cast.Burning(node), sourceCast, sourceWorld));
}