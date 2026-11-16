using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Combat;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际介质分送的独立事实，不冒充本体直接接触。</summary>
public readonly struct ConstructedMediumSpread
{
    /// <summary>原施放、触发人物、当前控制者和实际借用介质施放。</summary>
    public readonly long CastId, ContactId, OwnerId, MediumCastId;
    /// <summary>真实攻击部位及介质部位。</summary>
    public readonly int Node, MediumNode;
    /// <summary>真实通道及共同世界时点。</summary>
    public readonly string Channel;
    /// <summary>完成结算的共同时间。</summary>
    public readonly double WorldTime;
    /// <summary>按实际接收顺序记录的稳定编号。</summary>
    public readonly long[] Targets;
    /// <summary>每个接收者是否为建筑。</summary>
    public readonly bool[] Buildings;
    /// <summary>真实已传递路径，首项为入口，后续与接收者顺序一致。</summary>
    public readonly Vector3[] Points;

    /// <summary>只发布实际已发生的传递，不预报尚未结算的目标。</summary>
    /// <param name="cast">原施放。</param><param name="contact">真实入口人物。</param><param name="owner">实际归属。</param>
    /// <param name="mediumCast">介质施放。</param><param name="node">攻击部位。</param><param name="mediumNode">介质部位。</param>
    /// <param name="channel">真实通道。</param><param name="time">共同时间。</param><param name="targets">实际接收顺序。</param>
    /// <param name="buildings">是否为建筑。</param><param name="points">真实端点。</param>
    internal ConstructedMediumSpread(long cast, long contact, long owner, long mediumCast, int node, int mediumNode,
        string channel, double time, long[] targets, bool[] buildings, Vector3[] points)
    {
        CastId = cast; ContactId = contact; OwnerId = owner; MediumCastId = mediumCast; Node = node; MediumNode = mediumNode;
        Channel = channel; WorldTime = time; Targets = targets; Buildings = buildings; Points = points;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>介质完成有限分送后发布，不递归触发新的本体命中。</summary>
    public static event Action<ConstructedMediumSpread> MediumSpreadConfirmed;
    /// <summary>实际已发生的短暂模型连接，结束后按世界秒淡出。</summary>
    private static readonly List<MediumLink> mediumLinks = new();

    /// <summary>先确认一个同阵营介质的连通部分，再按距离传递有限结果，回调后重新核对真实介质。</summary>
    /// <param name="running">实际施放。</param><param name="node">攻击部位。</param><param name="contactId">真实入口人物。</param>
    /// <param name="impact">原接触作用。</param><param name="spread">普通分送规则。</param><param name="center">真实入口脚下。</param>
    /// <param name="time">共同世界时点。</param><param name="strength">本次接触余量比例。</param>
    private static void ApplyMediumSpread(RunningCast running, int node, long contactId, CompiledConstructionImpact impact,
        CompiledConstructionContactSpread spread, Vector3 center, double time, float strength)
    {
        CompiledSpreadMedium settings = spread.Medium;
        if (settings.OncePerCast && running.UsedMediumChannels.Contains(settings.Channel) ||
            !simulation.actorIndices.TryGetValue(contactId, out int entryIndex)) return;
        ActorMotion entry = simulation.actors[entryIndex];
        simulation.Sample(entry, time, out _, out float height);
        var entryBody = ActorContact(center, height, entry.Body?.Cast.BodyPose?.Torso ?? Vector3.zero);
        CastAuthority owner = running.Owner(node);
        RunningCast medium = null; int mediumNode = -1; AbilityMediumRegion region = null;
        foreach (var candidate in casts.OrderBy(c => c.Id))
        {
            if (candidate.LastWorldTime > time) continue;
            ValidateSource(candidate);
            if (candidate.Cast.Finished) continue;
            for (int part = 0; part < candidate.Cast.Plan.Nodes.Length; part++)
            {
                if (!owner.SameKingdom(candidate.Owner(part))) continue;
                region = MediumRegion(candidate, part, settings, entryBody);
                if (region?.IsConnected != true) continue;
                medium = candidate; mediumNode = part; break;
            }
            if (medium != null) break;
        }
        if (medium == null) return;
        // 在任何伤害回调之前消耗一次机会；即使随后世界改变，也不能再次使用同通道。
        if (settings.OncePerCast) running.UsedMediumChannels.Add(settings.Channel);
        long generation = worldGeneration;
        var candidates = new List<MediumRecipient>(); var nearby = new List<int>();
        simulation.actorIndex.Query(region.Bounds, nearby);
        foreach (int id in nearby)
        {
            var actor = simulation.actors[id];
            if (actor.Id != contactId && actor.Actor.data?.id == actor.Id && actor.Actor.isAlive() && actor.Actor.hasHealth() && owner.Matches(impact.Targets, actor.Actor))
                candidates.Add(new MediumRecipient(actor.Actor, null, actor.Id));
        }
        if (spread.BuildingDamagePerForce > 0f)
            foreach (Building building in World.world.buildings.getSimpleList())
                if (building.isAlive() && building.hasHealth() && owner.IsHostileTo(building.kingdom, 0))
                {
                    Vector3 point = BuildingMediumPoint(building);
                    if (region.Bounds.Contains(point)) candidates.Add(new MediumRecipient(null, building, building.data.id));
                }
        var targets = new List<long>(); var buildings = new List<bool>();
        var points = new List<Vector3> { center + Vector3.forward * height * .55f };
        while (targets.Count < settings.MaximumTargets && candidates.Count > 0)
        {
            ValidateSource(running); ValidateSource(medium);
            owner = running.Owner(node);
            if (running.Cast.Finished || medium.Cast.Finished || !owner.SameKingdom(medium.Owner(mediumNode))) break;
            region = MediumRegion(medium, mediumNode, settings, entryBody);
            if (region?.IsConnected != true) break;
            int chosen = -1; float nearest = float.PositiveInfinity; Vector3 nextPoint = default;
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                if (!SampleMediumRecipient(candidate, owner, impact, time, out var body, out Vector3 point) || !region.Contains(body))
                { candidates.RemoveAt(i); if (chosen > i) chosen--; continue; }
                float distance = (point - points[points.Count - 1]).sqrMagnitude;
                if (distance < nearest || distance == nearest && (chosen < 0 || candidate.Id < candidates[chosen].Id))
                { chosen = i; nearest = distance; nextPoint = point; }
            }
            if (chosen < 0) break;
            var recipient = candidates[chosen]; candidates.RemoveAt(chosen);
            if (recipient.Actor != null)
            {
                ApplyContactOutcome(running, node, recipient.Actor, recipient.Id, spread.Outcome, center, strength, out _);
                if (generation != worldGeneration) return;
                if (settings.InheritContactEffects)
                {
                    ApplyRestoration(running, node, recipient.Actor, impact.Restoration, strength);
                    if (generation != worldGeneration) return;
                    ApplyTargetResponse(running, node, recipient.Actor, recipient.Id, impact, ApplyAttackState.Hit, center, strength: strength);
                    if (generation != worldGeneration) return;
                    ApplyContactSpreads(running, node, recipient.Id, impact, nextPoint, time, strength, allowMedium: false);
                }
            }
            else
            {
                float damage = (node < 0 ? running.Power : running.Cast.RemainingForce(node)) * strength * spread.BuildingDamagePerForce;
                using (DamageResolutionContext.Enter(owner.Damage.DamageOrigin, owner.Damage.SourceScopeId))
                    recipient.Building.getHit(damage, pAttacker: owner.IsLive ? owner.Actor : null);
                if (generation != worldGeneration) return;
                if (settings.InheritContactEffects)
                    ApplyContactSpreads(running, node, recipient.Id, impact, nextPoint, time, strength, allowMedium: false);
            }
            if (generation != worldGeneration) return;
            if (settings.Link != null && spread.VisualSeconds > 0f && nearest > settings.LinkRadius * settings.LinkRadius * 4f)
                mediumLinks.Add(new MediumLink(settings.Link, points[points.Count - 1], nextPoint, settings.LinkRadius, time, spread.VisualSeconds));
            targets.Add(recipient.Id); buildings.Add(recipient.Building != null); points.Add(nextPoint);
        }
        MediumSpreadConfirmed?.Invoke(new ConstructedMediumSpread(running.Id, contactId, owner.Id, medium.Id, node, mediumNode,
            settings.Channel, time, targets.ToArray(), buildings.ToArray(), points.ToArray()));
    }

    /// <summary>只读取当前仍在场且材料吻合的介质部位，不使用包围球冒充实际形状。</summary>
    /// <param name="running">介质施放。</param><param name="node">实际部位。</param><param name="settings">明确选择。</param>
    /// <param name="entry">原真实入口。</param>
    private static AbilityMediumRegion MediumRegion(RunningCast running, int node, CompiledSpreadMedium settings, in AbilityCapsule entry)
    {
        var cast = running.Cast;
        if (cast.Finished || !cast.IsNodePresent(node) || cast.Plan.Nodes[node].MediumChannel != settings.Channel) return null;
        var matter = cast.Matter(node);
        if (settings.Matter != null && (matter.Id != settings.Matter.Id || matter.State != settings.Matter.State)) return null;
        var shapes = new List<AbilityCapsule>(); cast.AppendContacts(shapes, includeMaterials: true);
        shapes.RemoveAll(shape => shape.Node != node);
        return new AbilityMediumRegion(shapes, entry);
    }

    /// <summary>同刻采样真实人物或建筑，去重身份失效后不借用对象池新对象。</summary>
    /// <param name="recipient">已选候选。</param><param name="owner">当前归属。</param><param name="impact">原接触关系。</param>
    /// <param name="time">共同时间。</param><param name="body">真实接收形状。</param><param name="point">实际连接端点。</param>
    private static bool SampleMediumRecipient(MediumRecipient recipient, CastAuthority owner, CompiledConstructionImpact impact,
        double time, out AbilityCapsule body, out Vector3 point)
    {
        body = default; point = default;
        if (recipient.Actor != null)
        {
            Actor actor = recipient.Actor;
            if (actor.data?.id != recipient.Id || !actor.isAlive() || !actor.hasHealth() || !owner.Matches(impact.Targets, actor) ||
                !simulation.actorIndices.TryGetValue(recipient.Id, out int index)) return false;
            var motion = simulation.actors[index]; simulation.Sample(motion, time, out var foot, out float height);
            body = ActorContact(foot, height, motion.Body?.Cast.BodyPose?.Torso ?? Vector3.zero);
            point = foot + Vector3.forward * height * .55f; return true;
        }
        Building building = recipient.Building;
        if (building.data?.id != recipient.Id || !building.isAlive() || !building.hasHealth() || !owner.IsHostileTo(building.kingdom, 0)) return false;
        point = BuildingMediumPoint(building); body = new AbilityCapsule(-1, point, point, 0f, "physical"); return true;
    }

    /// <summary>建筑沿同一世界高度与其实际位置接触介质。</summary>
    /// <param name="building">仍有效的建筑。</param>
    private static Vector3 BuildingMediumPoint(Building building)
    {
        Vector3 point = building.current_position;
        point.z = building.getHeight() + (simulation.terrain?.Sample(point.x, point.y) ?? 0f); return point;
    }

    /// <summary>固定接收者的真实身份，不在结果中混用人物和建筑编号。</summary>
    private readonly struct MediumRecipient
    {
        /// <summary>实际人物或建筑，仅其中之一非空。</summary>
        internal readonly Actor Actor;
        /// <summary>实际建筑，人物候选为空。</summary>
        internal readonly Building Building;
        /// <summary>对象池不变的真实编号。</summary>
        internal readonly long Id;
        /// <summary>记录当前对象的身份。</summary>
        /// <param name="actor">实际人物。</param><param name="building">实际建筑。</param><param name="id">稳定编号。</param>
        internal MediumRecipient(Actor actor, Building building, long id) { Actor = actor; Building = building; Id = id; }
    }

    /// <summary>显示端只淡出已经发生的共同模型连接，不推进规则或扩大范围。</summary>
    /// <param name="now">共同完成的世界时间。</param>
    private void AppendMediumLinks(double now)
    {
        for (int i = mediumLinks.Count - 1; i >= 0; i--)
        {
            var link = mediumLinks[i]; float age = (float)(now - link.At);
            if (age >= link.Seconds) { mediumLinks.RemoveAt(i); continue; }
            visuals.Add(new FormVisualInstance(link.Resource, link.Transform, skin: link.Skin, poseBounds: link.Bounds,
                tint: new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(age / link.Seconds)), terrain: terrain));
        }
    }

    /// <summary>独占已确认蒙皮，横截面厚度固定，端点随实际路径伸展，持有原资源版本。</summary>
    private sealed class MediumLink
    {
        /// <summary>发生时所属版本的模型。</summary>
        internal readonly FormResource Resource;
        /// <summary>固定实际起点、朝向及横截面尺寸。</summary>
        internal readonly Matrix4x4 Transform;
        /// <summary>发布后不再改变的伸展蒙皮。</summary>
        internal readonly Matrix4x4[] Skin;
        /// <summary>实际伸展后的模型空间范围。</summary>
        internal readonly Bounds Bounds;
        /// <summary>共同世界时点和世界秒寿命。</summary>
        internal readonly double At;
        /// <summary>有限世界秒寿命。</summary>
        internal readonly float Seconds;
        /// <summary>只在已确认传递后创建一次，不产生新的作用实体。</summary>
        /// <param name="resource">真实反馈模型。</param><param name="from">实际起点。</param><param name="to">实际终点。</param>
        /// <param name="radius">固定世界厚度。</param><param name="at">共同时间。</param><param name="seconds">有限世界秒。</param>
        internal MediumLink(FormResource resource, Vector3 from, Vector3 to, float radius, double at, float seconds)
        {
            Resource = resource; At = at; Seconds = seconds;
            FormSpan span = resource.Span; float scale = radius / span.Radius;
            var pose = resource.Rig.CreatePose(); resource.Rig.Sample(null, 0f, pose);
            Matrix4x4 shift = Matrix4x4.Translate(span.Axis * ((to - from).magnitude / scale - span.Length));
            pose.World[span.Control] = shift * pose.World[span.Control]; pose.Skin[span.Control] = shift * pose.Skin[span.Control];
            resource.Rig.RebuildBounds(pose); Skin = pose.Skin; Bounds = pose.Bounds;
            Transform = Matrix4x4.TRS(from, Quaternion.FromToRotation(span.Axis, (to - from).normalized), Vector3.one * scale);
        }
    }
}
