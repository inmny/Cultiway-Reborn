using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际水火相消后的结果，显示和开发验证均读取确认结果。</summary>
public readonly struct ConstructedQuenching
{
    /// <summary>火与活动水体的实际施放编号；独立水体不冒充施放。</summary>
    public readonly long FireCast, WaterCast;

    /// <summary>火与水的真实部位，独立水体的水部位为负一。</summary>
    public readonly int FireNode, WaterNode;

    /// <summary>真正参与失水的世界材料编号。</summary>
    public readonly long[] WorldWater;

    /// <summary>共同确认的真实位置。</summary>
    public readonly Vector3 Point;

    /// <summary>共同确认的世界时间。</summary>
    public readonly double WorldTime;

    /// <summary>本次真实蒸散体积。</summary>
    public readonly double Evaporated;

    /// <summary>本次消耗的火作用量和剩余作用量。</summary>
    public readonly float Spent, Remaining;

    /// <summary>记录已经完成的反应。</summary>
    /// <param name="fire">火施放。</param><param name="water">活动水施放。</param>
    /// <param name="fireNode">火部位。</param><param name="waterNode">水部位。</param>
    /// <param name="worldWater">真实世界水体。</param><param name="point">接触位置。</param>
    /// <param name="time">世界时间。</param><param name="evaporated">蒸散体积。</param>
    /// <param name="spent">消耗。</param><param name="remaining">剩余。</param>
    internal ConstructedQuenching(long fire, long water, int fireNode, int waterNode, long[] worldWater, Vector3 point,
        double time, double evaporated, float spent, float remaining)
    {
        FireCast = fire;
        WaterCast = water;
        FireNode = fireNode;
        WaterNode = waterNode;
        WorldWater = worldWater;
        Point = point;
        WorldTime = time;
        Evaporated = evaporated;
        Spent = spent;
        Remaining = remaining;
    }
}

/// <summary>水火以真实接触和有限余量结算，不登记整招专用执行器。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>反应真正改变双方状态后通知，不由绘制触发。</summary>
    public static event Action<ConstructedQuenching> QuenchingConfirmed;

    /// <summary>世界水体查询缓冲，不参与工作线程。</summary>
    private readonly List<QuenchingWater> quenchingWater = new();

    /// <summary>确认时重新读取部位形状，避免使用预测末端替代接触时点。</summary>
    private static readonly List<AbilityCapsule> quenchingShapes = new();

    /// <summary>本次实际失水的世界来源。</summary>
    private static readonly List<long> evaporatedSources = new();

    /// <summary>有限蒸散反馈，只保存已经发生的真实反应。</summary>
    private static readonly List<SteamPuff> steam = new();

    /// <summary>单个反馈的世界秒数。</summary>
    private const float SteamSeconds = .9f;

    /// <summary>只有真实火质和液态水能建立反应，不受阵营影响。</summary>
    /// <param name="left">左形体。</param><param name="right">右形体。</param>
    private static bool IsQuenching(in ShapeMotion left, in ShapeMotion right) =>
        AbilityQuenching.Matches(left.To.Material, right.To.Material) &&
        left.Running.Cast.ContactSweepForce(left.To.Node) > 0f ||
        AbilityQuenching.Matches(right.To.Material, left.To.Material) &&
        right.Running.Cast.ContactSweepForce(right.To.Node) > 0f;

    /// <summary>只在确认时点扣双方数量，火势归零后不再向人物造成旧伤害。</summary>
    /// <param name="hit">共同时间最早的真实水火接触。</param>
    private static void ResolveQuenching(in ContactChoice hit)
    {
        ShapeMotion fire = hit.Left;
        ShapeMotion? water = hit.Right;
        if (fire.To.Material?.Id != "fire")
        {
            fire = hit.Right.Value;
            water = hit.Left;
        }

        int node = fire.To.Node;
        float before = fire.Running.Cast.RemainingForce(node);
        if (before <= 0 || fire.Running.Cast.Matter(node).Id != "fire" ||
            fire.Running.Cast.Matter(node).State != "energy") return;
        if (!ReadQuenchingShape(fire, out var actualFire)) return;
        double removed;
        bool contacted = true;
        evaporatedSources.Clear();
        if (hit.WorldRecord > 0)
            removed = WorldMaterialService.EvaporateWater(hit.WorldRecord, hit.WorldShape.Shape, actualFire,
                AbilityQuenching.Capacity(before), evaporatedSources, out contacted);
        else
        {
            var wet = water.Value;
            if (!ReadQuenchingShape(wet, out var actualWater) ||
                !AbilityQuenching.Matches(fire.Running.Cast.Matter(node), wet.Running.Cast.Matter(wet.To.Node)) ||
                !AbilityPairSweep.Find(actualFire, actualFire, actualWater, actualWater, out _, out _, out _)) return;
            removed = wet.Running.Cast.EvaporateWater(wet.To.Node, wet.To.Shape, AbilityQuenching.Capacity(before),
                hit.Point);
        }

        if (!contacted) return;
        // 低于水格浮点分辨率的微小火力只被吸收，不虚报减水，也不在零时刻反复碰撞。
        float spent = removed > 0 ? Mathf.Min(before, (float)(removed * AbilityQuenching.ForcePerVolume)) : before;
        fire.Running.Cast.ConsumeForce(node, spent);
        if (removed > 0) AddSteam(hit.Point, removed, simulation.contactTime);
        QuenchingConfirmed?.Invoke(new ConstructedQuenching(fire.Running.Id, water?.Running.Id ?? 0, node,
            water?.To.Node ?? -1,
            evaporatedSources.ToArray(), hit.Point, simulation.contactTime, removed, spent,
            fire.Running.Cast.RemainingForce(node)));
    }

    /// <summary>只有真实失水才建立有限蒸散，水火及燃烧木体共用。</summary>
    /// <param name="point">真实接触点。</param><param name="volume">实际失水量。</param><param name="time">确认时间。</param>
    internal static void AddSteam(Vector3 point, double volume, double time)
    {
        if (steam.Count == 128) steam.RemoveAt(0);
        steam.Add(new SteamPuff(point, (float)volume, time, FormResources.Steam));
    }

    /// <summary>重新取得同一部位及格子的已确认形状。</summary>
    /// <param name="motion">预测中识别的部位。</param><param name="shape">真实时点仍有效的形状。</param>
    private static bool ReadQuenchingShape(in ShapeMotion motion, out AbilityCapsule shape)
    {
        quenchingShapes.Clear();
        motion.Running.Cast.AppendContacts(quenchingShapes, includeMaterials: true);
        foreach (var candidate in quenchingShapes)
            if (candidate.Node == motion.To.Node && candidate.Shape == motion.To.Shape)
            {
                shape = candidate;
                return true;
            }

        shape = default;
        return false;
    }

    /// <summary>原版与三维视角共用有限上升曲面，数量不影响真实伤害或水量。</summary>
    /// <param name="now">本轮完成的世界时间。</param>
    private void AppendSteamVisuals(double now)
    {
        for (int i = steam.Count - 1; i >= 0; i--)
        {
            var puff = steam[i];
            float age = (float)(now - puff.At);
            if (age >= SteamSeconds)
            {
                steam.RemoveAt(i);
                continue;
            }

            float progress = Mathf.Clamp01(age / SteamSeconds),
                size = Mathf.Clamp(Mathf.Pow(puff.Volume, 1f / 3f), .12f, 3f);
            var point = puff.Point + Vector3.forward * (age * size * .65f);
            var scale = new Vector3(1 + progress, 1 + progress, 1.2f + progress) * size;
            visuals.Add(new FormVisualInstance(puff.Resource, Matrix4x4.TRS(point, Quaternion.identity, scale),
                tint: new Color(.75f, .86f, .89f, .27f * (1 - progress)), terrain: terrain));
        }
    }

    /// <summary>实际反应产生的短暂曲面，持有所属资源版本直到消失。</summary>
    private readonly struct SteamPuff
    {
        /// <summary>真实位置和实际蒸散量。</summary>
        internal readonly Vector3 Point;

        /// <summary>本次真实减水量。</summary>
        internal readonly float Volume;

        /// <summary>共同确认的世界时间。</summary>
        internal readonly double At;

        /// <summary>当时的共享资源版本。</summary>
        internal readonly FormResource Resource;

        /// <summary>记录本次有限反馈。</summary>
        /// <param name="point">实际位置。</param><param name="volume">蒸散量。</param>
        /// <param name="at">发生时间。</param><param name="resource">本版本资源。</param>
        internal SteamPuff(Vector3 point, float volume, double at, FormResource resource)
        {
            Point = point;
            Volume = volume;
            At = at;
            Resource = resource;
        }
    }
}