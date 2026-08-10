using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Effects;

/// <summary>按目标关系执行技能容器中已编译的对象效果和地块效果。</summary>
public static class SkillEffectResolver
{
    /// <summary>结算一次直接对象命中的结构化效果。</summary>
    public static bool ResolveDirectImpact(Entity skillEntity, BaseSimObject target)
    {
        if (!TryCreateContext(skillEntity, target?.GetSimPos() ?? default, 0f, out var context, out var pipeline))
            return false;
        return ApplyObjectEffects(pipeline, SkillEffectTrigger.Impact, in context, target).Changed;
    }

    /// <summary>在技能落点范围内按关系分别结算对象和地块效果。</summary>
    public static bool ResolveAreaImpact(Entity skillEntity, Vector3 position, float radius)
    {
        if (!TryCreateContext(skillEntity, position, radius, out var context, out var pipeline)) return false;
        SkillEffectResult result = ApplyAreaObjectEffects(pipeline, SkillEffectTrigger.Impact, in context);
        result = result.Merge(ApplyTileEffects(pipeline, SkillEffectTrigger.Impact, in context));
        SkillWorldVisualService.ReportAreaResolution(skillEntity, position, radius, in result);
        return result.Changed;
    }

    /// <summary>结算两个调度时间边界之间到期的周期效果。</summary>
    public static bool ResolvePeriodic(Entity skillEntity, float previousTime, float currentTime)
    {
        if (skillEntity.IsNull || !skillEntity.HasComponent<Position>()) return false;
        SkillEntityAsset asset = skillEntity.GetComponent<SkillEntity>().Asset;
        float radius = SkillEffectRadius.Resolve(
            skillEntity,
            asset.ImpactProfile.EffectRadius * asset.ImpactTuning.EffectRadiusMultiplier);
        if (!TryCreateContext(skillEntity, skillEntity.GetComponent<Position>().value, radius,
                out var context, out var pipeline)) return false;
        SkillEffectResult result = ApplyAreaObjectEffects(
            pipeline,
            SkillEffectTrigger.Periodic,
            in context,
            previousTime,
            currentTime);
        result = result.Merge(ApplyTileEffects(
            pipeline,
            SkillEffectTrigger.Periodic,
            in context,
            previousTime,
            currentTime));
        return result.Changed;
    }

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

    private static bool TryCreateContext(
        Entity skillEntity,
        Vector3 position,
        float radius,
        out SkillEffectContext context,
        out SkillEffectPipeline pipeline)
    {
        context = default;
        pipeline = null;
        if (skillEntity.IsNull || !skillEntity.HasComponent<SkillEntity>() ||
            !skillEntity.HasComponent<SkillContext>()) return false;
        SkillEntity runtime = skillEntity.GetComponent<SkillEntity>();
        if (runtime.SkillContainer.IsNull || !runtime.SkillContainer.HasComponent<SkillContainer>()) return false;
        pipeline = runtime.SkillContainer.GetComponent<SkillContainer>().EffectPipeline;
        if (pipeline == null || pipeline.Effects.Count == 0) return false;
        SkillContext cast = skillEntity.GetComponent<SkillContext>();
        context = new SkillEffectContext(runtime.SkillContainer, skillEntity, in cast, position, radius);
        return true;
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

    private static SkillEffectResult ApplyAreaObjectEffects(
        SkillEffectPipeline pipeline,
        SkillEffectTrigger trigger,
        in SkillEffectContext context,
        float previousTime = 0f,
        float currentTime = 0f)
    {
        SkillEffectResult result = default;
        foreach (SkillEffectDescriptor effect in pipeline.Effects)
        {
            if (!effect.IsObjectEffect || effect.Trigger != trigger ||
                trigger == SkillEffectTrigger.Periodic &&
                !IsPeriodicEffectDue(effect, previousTime, currentTime)) continue;
            foreach (BaseSimObject target in EnumerateTargets(context, effect.TargetRelation))
            {
                result = result.Merge(ApplyObjectEffect(effect, in context, target));
            }
        }
        return result;
    }

    private static SkillEffectResult ApplyObjectEffects(
        SkillEffectPipeline pipeline,
        SkillEffectTrigger trigger,
        in SkillEffectContext context,
        BaseSimObject target)
    {
        SkillEffectResult result = default;
        foreach (SkillEffectDescriptor effect in pipeline.Effects)
        {
            if (!effect.IsObjectEffect || effect.Trigger != trigger) continue;
            result = result.Merge(ApplyObjectEffect(effect, in context, target));
        }
        return result;
    }

    private static SkillEffectResult ApplyObjectEffect(
        SkillEffectDescriptor effect,
        in SkillEffectContext context,
        BaseSimObject target)
    {
        if (target == null || target.isRekt() ||
            !SkillTargetRelationResolver.Matches(
                effect.TargetRelation,
                context.Cast.SourceObj,
                target,
                context.Cast.ResolveAttackKingdom())) return default;
        var evaluation = new SkillEffectEvaluationContext(
            ResolveCaster(in context),
            context.SkillContainer,
            context.Position,
            context.Radius);
        if (!(effect.CanApplyObject?.Invoke(in evaluation, target) ?? true)) return default;
        SkillEffectResult result = effect.ApplyObject(in context, target);
        if (result.Changed)
        {
            SkillWorldVisualService.ReportEffectResolution(
                context.SkillEntity,
                effect.Id,
                context.Position,
                context.Radius,
                target.GetSimPos(),
                in result);
        }
        return result;
    }

    private static SkillEffectResult ApplyTileEffects(
        SkillEffectPipeline pipeline,
        SkillEffectTrigger trigger,
        in SkillEffectContext context,
        float previousTime = 0f,
        float currentTime = 0f)
    {
        SkillEffectResult result = default;
        var evaluation = new SkillEffectEvaluationContext(
            ResolveCaster(in context),
            context.SkillContainer,
            context.Position,
            context.Radius);
        foreach (SkillEffectDescriptor effect in pipeline.Effects)
        {
            if (!effect.IsTileEffect || effect.Trigger != trigger ||
                trigger == SkillEffectTrigger.Periodic &&
                !IsPeriodicEffectDue(effect, previousTime, currentTime)) continue;
            foreach (WorldTile tile in EnumerateTiles(context.Position, context.Radius))
            {
                if (!(effect.CanApplyTile?.Invoke(in evaluation, tile) ?? true)) continue;
                SkillEffectResult tileResult = effect.ApplyTile(in context, tile);
                if (!tileResult.Changed) continue;
                result = result.Merge(tileResult);
                SkillWorldVisualService.ReportEffectResolution(
                    context.SkillEntity,
                    effect.Id,
                    context.Position,
                    context.Radius,
                    tile.posV3,
                    in tileResult);
            }
        }
        return result;
    }

    private static ActorExtend ResolveCaster(in SkillEffectContext context)
    {
        Actor actor = context.Cast.SourceObj?.a;
        return actor.TryGetExtend(out ActorExtend caster) ? caster : null;
    }

    /// <summary>判断指定效果的独立周期是否在当前调度区间内跨过了触发边界。</summary>
    private static bool IsPeriodicEffectDue(
        SkillEffectDescriptor effect,
        float previousTime,
        float currentTime)
    {
        float interval = Mathf.Max(0.05f, effect.Interval);
        int previousTick = Mathf.FloorToInt((Mathf.Max(0f, previousTime) + 0.0001f) / interval);
        int currentTick = Mathf.FloorToInt((Mathf.Max(0f, currentTime) + 0.0001f) / interval);
        return currentTick > previousTick;
    }

    private static IEnumerable<BaseSimObject> EnumerateTargets(
        SkillEffectContext context,
        SkillEffectTargetRelation relation)
    {
        BaseSimObject source = context.Cast.SourceObj;
        if (source == null || source.isRekt()) yield break;
        if (relation == SkillEffectTargetRelation.Self)
        {
            yield return source;
            yield break;
        }
        WorldTile center = World.world.GetTile(
            Mathf.FloorToInt(context.Position.x),
            Mathf.FloorToInt(context.Position.y));
        if (center == null) yield break;
        float radiusSquared = context.Radius * context.Radius;
        int chunkRadius = Mathf.CeilToInt(context.Radius / 16f) + 1;
        foreach (Actor actor in Finder.getUnitsFromChunk(center, chunkRadius))
        {
            if (actor == null || actor.isRekt()) continue;
            if (Toolbox.SquaredDistVec2Float(context.Position, actor.current_position) > radiusSquared) continue;
            if (!SkillTargetRelationResolver.Matches(
                    relation,
                    source,
                    actor,
                    context.Cast.ResolveAttackKingdom())) continue;
            yield return actor;
        }
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
