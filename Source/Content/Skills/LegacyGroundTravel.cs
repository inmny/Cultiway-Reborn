using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原已学攻击在真实行进中使用的地面作用和本次剩余取样距离。</summary>
/// <param name="element">本次实际发射确定的元素。</param>
/// <param name="startsAt">原效果允许掠地的时点。</param>
internal sealed class LegacyGroundTravel(SkillVfxElementAsset element, float startsAt)
{
    /// <summary>沿用原发射的掠地作用，即使中途反射也不更换。</summary>
    internal readonly SkillVfxElementAsset Element = element;
    /// <summary>跳过成形期间的移动，从原作用时点开始累计。</summary>
    internal readonly float StartsAt = startsAt;
    /// <summary>上次采样以来已走过的剩余距离，按次存放。</summary>
    internal float Remainder;

    /// <summary>只有原轨迹和元素实际支持掠地时才加入共同推进。</summary>
    /// <param name="emission">本次原发射输入。</param>
    /// <param name="startsAt">构造实际保留的成形结束时点。</param>
    internal static LegacyGroundTravel From(in SkillEmission emission, float startsAt)
    {
        var skill = emission.SkillContainer;
        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        if (emission.RuntimeData.WorldScope != null || asset.WorldVisualProfile?.SuppressDefaultFlyOver == true ||
            !asset.PrefabEntity.HasComponent<ColliderSphere>()) return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        var manager = ModClass.I.SkillV3;
        if (manager.TrajLib.get(trajectory) is not { } path || (path.Domains & SkillGroundFx.FlyOverDomains) == 0)
            return null;
        SkillVfxElementAsset element = manager.ResolveGroundEffectElement(skill, emission.RuntimeData);
        return element?.HasGroundFlyOver == true ? new LegacyGroundTravel(element, startsAt) : null;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>预测完成以后才提交沿途格子，正常碰撞之后不再走原预测剩余路段。</summary>
    /// <param name="running">实际施放。</param>
    /// <param name="generation">本次提交所在世界的编号。</param>
    private static void ConfirmLegacyGroundTravel(RunningCast running, long generation)
    {
        LegacyGroundTravel travel = running.GroundTravel;
        if (travel == null || !running.Cast.TryGetRootTravel(travel.StartsAt, out Vector3 from, out Vector3 to)) return;
        float distance = Vector2.Distance(from, to);
        if (distance <= .000001f) return;
        const float spacing = SkillGroundFx.FlyOverDistanceThreshold;
        for (float sample = spacing - travel.Remainder; sample <= distance; sample += spacing)
        {
            Vector3 point = Vector3.LerpUnclamped(from, to, sample / distance);
            WorldTile tile = SkillGroundFx.GetTile(point);
            if (tile == null) continue;
            var floor = tile.main_type;
            SkillGroundFx.OnFlyOver(point, travel.Element);
            if (generation != worldGeneration) return;
            if (tile.main_type != floor) simulation.RefreshTerrainAt(tile);
        }

        travel.Remainder = (travel.Remainder + distance) % spacing;
    }
}
