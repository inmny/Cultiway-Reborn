using System;
using System.Collections.Generic;
using Cultiway.Core.Combat;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实命中后已完成的范围分送；附近人物不冒充本体直接命中。</summary>
public readonly struct ConstructedSpread
{
    /// <summary>原施放、触发人物与当前控制人物的稳定编号。</summary>
    public readonly long CastId, ContactId, OwnerId;
    /// <summary>实际部位及本部位的分送资料序号。</summary>
    public readonly int Node, Index;
    /// <summary>接触时的真实中心。</summary>
    public readonly Vector3 Center;
    /// <summary>实际范围的世界半径。</summary>
    public readonly float Radius;
    /// <summary>按地面平面半径取人，忽略高度和身体边缘。</summary>
    public readonly bool GroundPlane;
    /// <summary>共同确认的世界时间。</summary>
    public readonly double WorldTime;
    /// <summary>实际分送过结果的人物和实际提交伤害的建筑。</summary>
    public readonly long[] Actors, Buildings;

    /// <summary>只记录已经完成的真实分送。</summary>
    /// <param name="cast">原施放。</param><param name="contact">触发人物。</param><param name="owner">控制人物。</param>
    /// <param name="node">实际部位。</param><param name="index">范围序号。</param><param name="center">真实中心。</param>
    /// <param name="radius">真实半径。</param><param name="time">共同时间。</param>
    /// <param name="actors">人物结果。</param><param name="buildings">建筑结果。</param>
    /// <param name="groundPlane">是否沿用原地面平面范围。</param>
    internal ConstructedSpread(long cast, long contact, long owner, int node, int index, Vector3 center, float radius, double time, long[] actors, long[] buildings, bool groundPlane)
    {
        CastId = cast; ContactId = contact; OwnerId = owner; Node = node; Index = index; Center = center; Radius = radius;
        WorldTime = time; Actors = actors; Buildings = buildings; GroundPlane = groundPlane;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>范围完成后发布独立事实，不再次触发本体的接触次数或附加词条。</summary>
    public static event Action<ConstructedSpread> SpreadConfirmed;

    /// <summary>已发生的短暂作用球，只由共同世界时间移除。</summary>
    private static readonly List<ContactVolume> contactVolumes = new();

    /// <summary>先在同刻真实身体上分送普通结果，再向去重建筑结算，最后发布同尺寸显示与事实。</summary>
    /// <param name="running">真实施放。</param><param name="node">真实部位。</param><param name="contactId">触发人物身份。</param>
    /// <param name="impact">本体实际接触规则。</param><param name="center">主伤害及换位前固定的真实中心。</param>
    /// <param name="time">接触共同世界时点。</param><param name="strength">本次接触的实际余力倍率。</param>
    /// <param name="allowMedium">是否允许后续介质分送。</param>
    /// <param name="groundPlane">是否按原派生的平面距离确认人物，并用贴地圆面显示。</param>
    private static void ApplyContactSpreads(RunningCast running, int node, long contactId, CompiledConstructionImpact impact,
        Vector3 center, double time, float strength = 1f, bool allowMedium = true, bool groundPlane = false)
    {
        if (impact.Spreads.Count == 0 || strength <= 0f || running.Cast.Finished) return;
        long generation = worldGeneration;
        for (int index = 0; index < impact.Spreads.Count; index++)
        {
            var spread = impact.Spreads[index];
            if (!spread.HasEffect) continue;
            if (spread.Medium != null)
            {
                if (allowMedium) ApplyMediumSpread(running, node, contactId, impact, spread, center, time, strength);
                if (generation != worldGeneration) return;
                continue;
            }
            CastAuthority owner = running.Owner(node);
            if (groundPlane && !owner.IsLive) return;
            var shape = new AbilityCapsule(node, center, center, spread.Radius, "physical");
            var recipients = new List<long>();
            var buildings = new List<long>();
            // 索引只筛选候选，按接触时点的完整身体胶囊确认，包含实际离地高度。
            if (spread.Outcome?.HasEffect == true)
            {
                var nearbyActors = new List<int>();
                if (groundPlane) simulation.actorIndex.QueryPlane(center, spread.Radius, nearbyActors);
                else simulation.actorIndex.Query(new Bounds(center, Vector3.one * spread.Radius * 2f), nearbyActors);
                foreach (int id in nearbyActors)
                {
                    var candidate = simulation.actors[id];
                    if (candidate.Actor.data?.id != candidate.Id || !candidate.Actor.isAlive() || !candidate.Actor.hasHealth() || !owner.Matches(impact.Targets, candidate.Actor)) continue;
                    simulation.Sample(candidate, time, out Vector3 foot, out float height);
                    if (groundPlane)
                    {
                        if (((Vector2)(foot - center)).sqrMagnitude > spread.Radius * spread.Radius) continue;
                    }
                    else
                    {
                        var body = ActorHitBoundary.Create(foot, height, candidate.Body?.Cast.BodyPose?.Torso ?? Vector3.zero);
                        if (!AbilityPairSweep.Find(shape, shape, body, body, out _, out _, out _)) continue;
                    }
                    ApplyContactOutcome(running, node, candidate.Actor, candidate.Id, spread.Outcome, center, strength, out _);
                    if (generation != worldGeneration) return;
                    recipients.Add(candidate.Id);
                }
            }
            // 建筑沿原版所在位置判断，一座建筑占多个地块也只受击一次。
            float damage = (node < 0 ? running.Power : running.Cast.RemainingForce(node)) * strength * spread.BuildingDamagePerForce;
            if (damage > 0f)
            {
                var seen = new HashSet<long>();
                int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - spread.Radius)), maxX = Mathf.Min(MapBox.width - 1, Mathf.CeilToInt(center.x + spread.Radius));
                int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - spread.Radius)), maxY = Mathf.Min(MapBox.height - 1, Mathf.CeilToInt(center.y + spread.Radius));
                for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++)
                {
                    Building building = World.world.GetTileSimple(x, y).building;
                    if (building == null || !building.isAlive() || !building.hasHealth() || !seen.Add(building.data.id) || !owner.IsHostileTo(building.kingdom, 0)) continue;
                    Vector3 point = building.current_position;
                    point.z = building.getHeight() + (simulation.terrain?.Sample(point.x, point.y) ?? 0f);
                    if ((point - center).sqrMagnitude > spread.Radius * spread.Radius) continue;
                    long buildingId = building.data.id;
                    using (DamageResolutionContext.Enter(DamageOrigin.Reaction, owner.Damage.SourceScopeId))
                        building.getHit(damage, pAttacker: owner.IsLive ? owner.Actor : null);
                    if (generation != worldGeneration) return;
                    buildings.Add(buildingId);
                }
            }
            if (spread.VisualSeconds > 0f)
            {
                FormResource resource = node < 0 ? FormResources.ContactVolume : running.Cast.Plan.Nodes[node].Resource.ContactVolume;
                string surface = null;
                if (node >= 0)
                {
                    var form = running.Cast.Plan.Nodes[node];
                    surface = running.Cast.Matter(node)?.Surface ?? form.Surface;
                    if (surface == null && form.Resource.Description.Parts.Length > 0)
                        surface = form.Resource.Description.Parts[0].Surface;
                }
                Vector3 position = center;
                Vector3 scale = Vector3.one * spread.Radius;
                if (groundPlane)
                {
                    const float thickness = .14f;
                    scale.z = thickness;
                    position.z = (simulation.terrain?.Sample(center.x, center.y) ?? 0f) + thickness;
                }
                contactVolumes.Add(new ContactVolume(resource, surface, position, scale, time, spread.VisualSeconds));
            }
            SpreadConfirmed?.Invoke(new ConstructedSpread(running.Id, contactId, owner.Id, node, index, center, spread.Radius, time, recipients.ToArray(), buildings.ToArray(), groundPlane));
            if (generation != worldGeneration) return;
        }
    }

    /// <summary>作用球尺寸保持实际半径，结束只淡出，绘制端不推进或扩大作用。</summary>
    /// <param name="now">共同完成的世界时间。</param>
    private void AppendContactVolumes(double now)
    {
        for (int i = contactVolumes.Count - 1; i >= 0; i--)
        {
            var volume = contactVolumes[i];
            float age = (float)(now - volume.At);
            if (age >= volume.Seconds) { contactVolumes.RemoveAt(i); continue; }
            visuals.Add(new FormVisualInstance(volume.Resource, Matrix4x4.TRS(volume.Center, Quaternion.identity, volume.Scale),
                surface: volume.Surface, tint: new Color(1f, 1f, 1f, .45f * (1f - Mathf.Clamp01(age / volume.Seconds))), terrain: terrain));
        }
    }

    /// <summary>持有发生时资源版本的短暂球体，换世界直接清空。</summary>
    private readonly struct ContactVolume
    {
        /// <summary>发生时的资源与表面，同版本重载不篡改旧结果。</summary>
        internal readonly FormResource Resource;
        /// <summary>实际来源已经加载的表面。</summary>
        internal readonly string Surface;
        /// <summary>固定显示中心与按实际半径换算的外观尺寸。</summary>
        internal readonly Vector3 Center, Scale;
        /// <summary>有限世界秒数。</summary>
        internal readonly float Seconds;
        /// <summary>发生的共同世界时点。</summary>
        internal readonly double At;
        /// <summary>记录已完成范围的显示资料。</summary>
        /// <param name="resource">所属资源。</param><param name="surface">原来源表面。</param><param name="center">真实中心。</param>
        /// <param name="scale">从真实范围得到的显示矩阵尺寸。</param><param name="at">发生时点。</param><param name="seconds">有限秒数。</param>
        internal ContactVolume(FormResource resource, string surface, Vector3 center, Vector3 scale, double at, float seconds)
        { Resource = resource; Surface = surface; Center = center; Scale = scale; At = at; Seconds = seconds; }
    }
}
