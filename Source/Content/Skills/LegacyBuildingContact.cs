using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原攻击发出时确定的建筑关系、伤害和目标。</summary>
internal sealed class LegacyBuildingContact
{
    private readonly ColliderConfig collider;
    private readonly long selectedBuilding;

    /// <summary>范围攻击由已确认的爆发统一处理伤害。</summary>
    internal readonly bool Area;
    /// <summary>该攻击原先会对普通接触对象造成基础伤害。</summary>
    internal readonly bool DealsBaseDamage;
    /// <summary>有高度限制的原轨迹，按同一目标的高度保留允许接触的末段。</summary>
    internal readonly float HeightAllowance;
    /// <summary>原攻击实际使用的命中声音。</summary>
    internal readonly string ImpactSound;

    private LegacyBuildingContact(ColliderConfig collider, long selectedBuilding, bool area, bool dealsBaseDamage,
        float heightAllowance, string impactSound)
    {
        this.collider = collider;
        this.selectedBuilding = selectedBuilding;
        Area = area;
        DealsBaseDamage = dealsBaseDamage;
        HeightAllowance = heightAllowance;
        ImpactSound = impactSound;
    }

    /// <summary>沿用原发射是否允许扫描建筑的规则。</summary>
    /// <param name="emission">原入口已经接受的实际施放。</param>
    internal static LegacyBuildingContact From(in SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        if (emission.RuntimeData.WorldScope != null || !asset.PrefabEntity.TryGetComponent(out ColliderConfig collider) ||
            !collider.Enabled || !collider.Building) return null;
        long selected = emission.Target?.isBuilding() == true ? emission.Target.getID() : 0;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        float heightAllowance = trajectory == SkillTrajectories.ArcToPosition.id ? SkillTrajectories.ArcCollisionHeight :
            trajectory == SkillTrajectories.FallingStrike.id || trajectory == SkillTrajectories.RainFall.id
                ? SkillTrajectories.SkyfallCollisionHeight : float.PositiveInfinity;
        string sound = ModClass.I.SkillV3.ResolveGroundEffectElement(skill, emission.RuntimeData)?.ImpactSound;
        return new LegacyBuildingContact(collider, selected, asset.ImpactProfile.CanResolveAtPosition,
            asset.DealsBaseDamage, heightAllowance, sound);
    }

    /// <summary>原来可点名的建筑能直接接触，其他建筑沿用当前控制者阵营。</summary>
    /// <param name="building">当前仍在地图中的原对象。</param>
    /// <param name="kingdom">本次实际控制者的阵营。</param>
    /// <param name="reflections">反射后放弃原来点名的建筑。</param>
    internal bool Allows(Building building, Kingdom kingdom, int reflections)
    {
        if (building == null || building.isRekt()) return false;
        if (reflections == 0 && selectedBuilding != 0 && building.getID() == selectedBuilding) return true;
        return !collider.ExplicitTargetOnly && ((kingdom?.isEnemy(building.kingdom) ?? true) ? collider.Enemy : collider.Alias);
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>一座建筑可占多格，同一作用面只查询一次其实际边界。</summary>
    private readonly HashSet<long> checkedLegacyBuildings = new();

    /// <summary>沿已预测的实际部位扫掠原建筑平面边界，共同选择最早接触。</summary>
    /// <param name="shape">实际作用面的前后态。</param>
    /// <param name="owner">当前部位控制者。</param>
    /// <param name="step">本次共同预测的世界时长。</param>
    /// <param name="include">加入共同接触候选。</param>
    private void FindLegacyBuildingContacts(ShapeMotion shape, CastAuthority owner, float step, Action<ContactChoice> include)
    {
        RunningCast running = shape.Running;
        int node = shape.To.Node, stage = running.Cast.BodyStage;
        if (!running.Cast.ContactInterval(node, stage, out float from, out float until)) return;
        if (!shape.Continuous) from = 1f;
        if (!LimitLegacyBuildingHeight(shape, running.LastTargetFoot.z + running.BuildingContact.HeightAllowance,
                ref from, ref until)) return;
        Bounds bounds = shape.Bounds;
        // 原建筑通过占地格登记，沿用原查询留给建筑中心的余量。
        const float padding = 3f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(bounds.min.x - padding));
        int maxX = Mathf.Min(MapBox.width - 1, Mathf.CeilToInt(bounds.max.x + padding));
        int minY = Mathf.Max(0, Mathf.FloorToInt(bounds.min.y - padding));
        int maxY = Mathf.Min(MapBox.height - 1, Mathf.CeilToInt(bounds.max.y + padding));
        Kingdom kingdom = owner.AttackKingdom();
        int reflections = running.Cast.Reflections(node);
        checkedLegacyBuildings.Clear();
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            Building building = World.world.GetTileSimple(x, y).building;
            if (!running.BuildingContact.Allows(building, kingdom, reflections) ||
                !checkedLegacyBuildings.Add(building.getID())) continue;
            // 建筑用负编号保留独立次数，仍消耗同组的总接触预算。
            var key = running.HitKeyAtStage(node, -building.getID(), stage);
            running.Hits.TryGetValue(key, out ContactMemory memory);
            CompiledContactPolicy policy = running.Cast.Plan.ContactPolicies[key.Item1];
            if (memory != null && (policy.Mode == ConstructionContactMode.Once ||
                policy.MaximumPerTarget > 0 && memory.Count >= policy.MaximumPerTarget)) continue;
            float earliest = from;
            if (memory != null && policy.Mode == ConstructionContactMode.Interval)
                earliest = Mathf.Max(from, (memory.Age + policy.Interval - (running.Cast.Age - step)) / step);
            if (earliest > 1f || earliest > until) continue;
            Vector2 center = building.current_position;
            float radius = Mathf.Max(1f, building.stats[strings.S.size]);
            // 原建筑边界是平面的，垂直轴覆盖本段扫掠，保留真实作用面而非扩大投射本体。
            var target = new AbilityCapsule(0, new Vector3(center.x, center.y, bounds.min.z),
                new Vector3(center.x, center.y, bounds.max.z), radius, null);
            AbilityCapsule start = Slice(shape.From, shape.To, earliest);
            if (!AbilityPairSweep.Find(start, shape.To, target, target, out float at, out Vector3 point, out Vector3 normal))
                continue;
            float fraction = Mathf.Lerp(earliest, 1f, at);
            if (fraction <= until)
                include(new ContactChoice(shape, default, null, fraction, point, normal, step,
                    worldShape: target, building: building));
        }
    }

    /// <summary>弧线和坠落沿用原来接近目标高度后才可碰到建筑的规则。</summary>
    /// <param name="shape">本段的实际形状和转动中心。</param>
    /// <param name="height">原高度上限，普通平飞不限。</param>
    /// <param name="from">允许接触的起始进度。</param><param name="until">允许接触的结束进度。</param>
    private static bool LimitLegacyBuildingHeight(in ShapeMotion shape, float height, ref float from, ref float until)
    {
        if (float.IsPositiveInfinity(height)) return true;
        float before = shape.From.SweepPose?.Pivot.z ?? (shape.From.A.z + shape.From.B.z) * .5f;
        float after = shape.To.SweepPose?.Pivot.z ?? (shape.To.A.z + shape.To.B.z) * .5f;
        if (!shape.Continuous) return after <= height;
        if (before > height && after > height) return false;
        if (before > height) from = Mathf.Max(from, (height - before) / (after - before));
        else if (after > height) until = Mathf.Min(until, (height - before) / (after - before));
        return from <= until;
    }

    /// <summary>已到最早接触点后执行原建筑伤害或开启共同范围爆发。</summary>
    /// <param name="hit">已经重新走到该时点的建筑接触。</param>
    private static void ResolveLegacyBuildingContact(in ContactChoice hit)
    {
        RunningCast running = hit.Left.Running;
        LegacyBuildingContact rule = running.BuildingContact;
        int node = hit.Left.To.Node;
        CastAuthority owner = running.Owner(node);
        Building building = hit.Building;
        if (!running.Cast.IsNodePresent(node) || building.getID() != hit.BuildingId ||
            !rule.Allows(building, owner.AttackKingdom(), running.Cast.Reflections(node))) return;
        RememberContact(running, node, -hit.BuildingId);
        long generation = worldGeneration;
        if (!rule.Area)
        {
            Vector3 foot = building.GetSimPos();
            if (running.GroundContact != null)
                ConfirmLegacyGroundContact(running.GroundContact, foot, owner, generation);
            if (generation != worldGeneration) return;
            float damage = running.Cast.DamageForce(node) * running.Cast.Impact(node).DamageScale;
            if (rule.DealsBaseDamage && damage > 0f && !building.isRekt())
            {
                int before = building.getHealth();
                building.getHit(damage, pAttacker: owner.IsLive ? owner.Actor : null);
                if (generation != worldGeneration) return;
                if (!building.isRekt() && building.getHealth() < before)
                    SpatialCombatFeedback.Sound(rule.ImpactSound, foot);
            }
        }

        running.Cast.ConfirmBuildingContact(node, hit.Point, hit.BuildingId,
            hit.LeftRayLength >= 0f ? hit.LeftRayLength : null);
        EndExhaustedContactGroup(running, node);
    }
}
