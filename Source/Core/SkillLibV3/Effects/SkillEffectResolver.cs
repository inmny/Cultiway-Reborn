using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Effects;

/// <summary>读取已编译的作用资料，供目标选择及预览使用。</summary>
public static class SkillEffectResolver
{
    /// <summary>在扣费前检查落点范围内是否至少存在一个可应用的地块效果。</summary>
    public static bool HasApplicableTile(
        ActorExtend caster,
        Entity skillContainer,
        Vector3 position,
        float radius)
    {
        if (caster == null || caster.Base.isRekt() || skillContainer.IsNull ||
            !skillContainer.HasComponent<SkillContainer>()) return false;
        SkillEffectPipeline pipeline = skillContainer.GetComponent<SkillContainer>().EffectPipeline;
        if (pipeline == null || !pipeline.HasTileEffects) return false;
        var evaluation = new SkillEffectEvaluationContext(caster, skillContainer, position, radius);
        foreach (SkillEffectDescriptor effect in pipeline.Effects)
        {
            if (!effect.IsTileEffect) continue;
            foreach (WorldTile tile in EnumerateTiles(position, radius))
            {
                if (effect.CanApplyTile?.Invoke(in evaluation, tile) ?? true) return true;
            }
        }
        return false;
    }

    /// <summary>统计落点预览中的有效和跳过地块数。</summary>
    public static SkillTilePreviewResult EvaluateTiles(
        ActorExtend caster,
        Entity skillContainer,
        Vector3 position,
        float radius)
    {
        if (caster == null || caster.Base.isRekt() || skillContainer.IsNull ||
            !skillContainer.HasComponent<SkillContainer>()) return default;
        SkillEffectPipeline pipeline = skillContainer.GetComponent<SkillContainer>().EffectPipeline;
        if (pipeline == null || !pipeline.HasTileEffects) return default;
        var evaluation = new SkillEffectEvaluationContext(caster, skillContainer, position, radius);
        int valid = 0;
        int skipped = 0;
        foreach (WorldTile tile in EnumerateTiles(position, radius))
        {
            bool applicable = false;
            foreach (SkillEffectDescriptor effect in pipeline.Effects)
            {
                if (!effect.IsTileEffect) continue;
                if (effect.CanApplyTile?.Invoke(in evaluation, tile) ?? true)
                {
                    applicable = true;
                    break;
                }
            }
            if (applicable) valid++;
            else skipped++;
        }
        return new SkillTilePreviewResult(valid, skipped);
    }

    /// <summary>
    /// 按技能真实的地块预检与效用委托，累计指定落点范围内的边际收益。
    /// 可选过滤器用于把组织领地、禁区等外部空间约束叠加到技能自身规则上。
    /// </summary>
    public static float EvaluateTileUtility(
        ActorExtend caster,
        Entity skillContainer,
        Vector3 position,
        float radius,
        Func<WorldTile, bool> filter = null)
    {
        if (caster == null || caster.Base.isRekt() || skillContainer.IsNull ||
            !skillContainer.HasComponent<SkillContainer>()) return 0f;
        SkillEffectPipeline pipeline = skillContainer.GetComponent<SkillContainer>().EffectPipeline;
        if (pipeline == null || !pipeline.HasTileEffects) return 0f;

        var evaluation = new SkillEffectEvaluationContext(caster, skillContainer, position, radius);
        float utility = 0f;
        foreach (WorldTile tile in EnumerateTiles(position, radius))
        {
            if (filter != null && !filter(tile)) continue;
            foreach (SkillEffectDescriptor effect in pipeline.Effects)
            {
                if (!effect.IsTileEffect || !(effect.CanApplyTile?.Invoke(in evaluation, tile) ?? true)) continue;
                utility += Mathf.Max(0f, effect.EvaluateTileUtility?.Invoke(in evaluation, tile) ?? 1f);
            }
        }
        return utility;
    }

    /// <summary>按技能结算采用的圆形离散规则收集影响范围内的世界地块。</summary>
    public static void CollectAreaTiles(
        Vector3 center,
        float radius,
        ICollection<WorldTile> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        foreach (WorldTile tile in EnumerateTiles(center, radius)) output.Add(tile);
    }

    /// <summary>收集落点范围内每个地块的具体可应用状态，供玩家选点预览复用真实预检规则。</summary>
    public static void CollectTilePreview(
        ActorExtend caster,
        Entity skillContainer,
        Vector3 position,
        float radius,
        ICollection<SkillTilePreviewEntry> output)
    {
        output.Clear();
        if (caster == null || caster.Base.isRekt() || skillContainer.IsNull ||
            !skillContainer.HasComponent<SkillContainer>()) return;
        SkillEffectPipeline pipeline = skillContainer.GetComponent<SkillContainer>().EffectPipeline;
        if (pipeline == null || !pipeline.HasTileEffects) return;
        var evaluation = new SkillEffectEvaluationContext(caster, skillContainer, position, radius);
        foreach (WorldTile tile in EnumerateTiles(position, radius))
        {
            bool applicable = false;
            foreach (SkillEffectDescriptor effect in pipeline.Effects)
            {
                if (!effect.IsTileEffect) continue;
                if (!(effect.CanApplyTile?.Invoke(in evaluation, tile) ?? true)) continue;
                applicable = true;
                break;
            }
            output.Add(new SkillTilePreviewEntry(tile, applicable));
        }
    }

    /// <summary>计算指定对象从技能结构化效果中能够获得的边际收益。</summary>
    public static float EvaluateObjectUtility(
        ActorExtend caster,
        Entity skillContainer,
        BaseSimObject target,
        Vector3 position,
        float radius)
    {
        if (caster == null || caster.Base.isRekt() || target == null || target.isRekt() ||
            skillContainer.IsNull || !skillContainer.HasComponent<SkillContainer>()) return 0f;
        SkillEffectPipeline pipeline = skillContainer.GetComponent<SkillContainer>().EffectPipeline;
        if (pipeline == null || !pipeline.HasObjectEffects) return 0f;
        var evaluation = new SkillEffectEvaluationContext(caster, skillContainer, position, radius);
        float utility = 0f;
        foreach (SkillEffectDescriptor effect in pipeline.Effects)
        {
            if (!effect.IsObjectEffect ||
                !SkillTargetRelationResolver.Matches(
                    effect.TargetRelation,
                    caster.Base,
                    target,
                    caster.Base.kingdom) ||
                !(effect.CanApplyObject?.Invoke(in evaluation, target) ?? true)) continue;
            utility += Mathf.Max(0f, effect.EvaluateObjectUtility?.Invoke(in evaluation, target) ?? 1f);
        }
        return utility;
    }

    /// <summary>按结构化效果的边际收益选择最佳友军；范围法术会累计候选中心覆盖的全部友军收益。</summary>
    public static bool TryResolveBestFriendlyTarget(
        ActorExtend caster,
        Entity skillContainer,
        IReadOnlyList<Actor> nearbyAllies,
        float radius,
        bool aggregateArea,
        out BaseSimObject target)
    {
        target = null;
        if (caster == null || caster.Base.isRekt() || skillContainer.IsNull ||
            !skillContainer.HasComponent<SkillContainer>()) return false;

        float bestUtility = 0f;
        Actor best = null;
        EvaluateFriendlyCandidate(
            caster,
            skillContainer,
            caster.Base,
            nearbyAllies,
            radius,
            aggregateArea,
            ref bestUtility,
            ref best);
        if (nearbyAllies != null)
        {
            for (int i = 0; i < nearbyAllies.Count; i++)
            {
                Actor candidate = nearbyAllies[i];
                if (candidate == null || candidate == caster.Base || candidate.isRekt() ||
                    !SkillTargetRelationResolver.IsFriendly(caster.Base, candidate)) continue;
                EvaluateFriendlyCandidate(
                    caster,
                    skillContainer,
                    candidate,
                    nearbyAllies,
                    radius,
                    aggregateArea,
                    ref bestUtility,
                    ref best);
            }
        }
        target = best;
        return best != null && bestUtility > 0f;
    }

    /// <summary>计算一个友军候选中心的单体收益或范围累计收益。</summary>
    private static void EvaluateFriendlyCandidate(
        ActorExtend caster,
        Entity skillContainer,
        Actor candidate,
        IReadOnlyList<Actor> nearbyAllies,
        float radius,
        bool aggregateArea,
        ref float bestUtility,
        ref Actor best)
    {
        Vector3 position = candidate.GetSimPos();
        float utility = EvaluateObjectUtility(caster, skillContainer, candidate, position, radius);
        if (aggregateArea && nearbyAllies != null)
        {
            float radiusSquared = radius * radius;
            for (int i = 0; i < nearbyAllies.Count; i++)
            {
                Actor ally = nearbyAllies[i];
                if (ally == null || ally == candidate || ally == caster.Base || ally.isRekt() ||
                    Toolbox.SquaredDistVec2Float(candidate.current_position, ally.current_position) > radiusSquared)
                    continue;
                utility += EvaluateObjectUtility(caster, skillContainer, ally, position, radius);
            }
            if (candidate != caster.Base &&
                Toolbox.SquaredDistVec2Float(candidate.current_position, caster.Base.current_position) <= radiusSquared)
            {
                utility += EvaluateObjectUtility(caster, skillContainer, caster.Base, position, radius);
            }
        }
        if (utility <= bestUtility) return;
        bestUtility = utility;
        best = candidate;
    }

    private static IEnumerable<WorldTile> EnumerateTiles(Vector3 center, float radius)
    {
        int roundedRadius = Mathf.Max(0, Mathf.CeilToInt(radius));
        int centerX = Mathf.FloorToInt(center.x);
        int centerY = Mathf.FloorToInt(center.y);
        float radiusSquared = radius * radius;
        for (int x = centerX - roundedRadius; x <= centerX + roundedRadius; x++)
        for (int y = centerY - roundedRadius; y <= centerY + roundedRadius; y++)
        {
            float dx = x - center.x;
            float dy = y - center.y;
            if (radius > 0f && dx * dx + dy * dy > radiusSquared) continue;
            WorldTile tile = World.world.GetTile(x, y);
            if (tile != null) yield return tile;
        }
    }
}

public readonly struct SkillTilePreviewResult
{
    public readonly int Valid;
    public readonly int Skipped;

    public SkillTilePreviewResult(int valid, int skipped)
    {
        Valid = Math.Max(0, valid);
        Skipped = Math.Max(0, skipped);
    }
}

/// <summary>玩家选点预览中的一个世界地块及其真实可应用状态。</summary>
public readonly struct SkillTilePreviewEntry
{
    public readonly WorldTile Tile;
    public readonly bool Applicable;

    public SkillTilePreviewEntry(WorldTile tile, bool applicable)
    {
        Tile = tile;
        Applicable = applicable;
    }
}
