using System;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Events;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Content.CreatureCompositions.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.Services;

/// <summary>
///     当前生效身体的唯一写入者。任何玩法都先形成完整的身体方案，再交给本服务一次性提交。
/// </summary>
public static class CreatureExpressionService
{
    /// <summary>尝试提交一份身体方案；方案不满足身体规则时返回假且不修改角色。</summary>
    /// <param name="actor">目标生物单位。</param>
    /// <param name="plan">已经确定的完整身体方案。</param>
    /// <param name="phenotype">提交成功后的当前身体。</param>
    /// <param name="failReason">失败时的稳定原因键，仅供日志使用。</param>
    public static bool TryExpress(
        ActorExtend actor,
        CreaturePhenotypePlan plan,
        out CreaturePhenotype phenotype,
        out string failReason)
    {
        phenotype = default;
        failReason = null;
        if (actor?.Base == null || actor.Base.isRekt() || plan == null)
        {
            failReason = "creature_phenotype.actor_invalid";
            return false;
        }

        // 按固定顺序执行：整理方案 → 切换固定形态 → 一次性替换当前身体 → 清理旧来源 → 刷新缓存 → 发布事件。
        if (!CreaturePhenotypeCompiler.TryGetOrCompile(plan, out CompiledCreaturePhenotype compiled))
        {
            failReason = "creature_phenotype.plan_rejected";
            return false;
        }

        Actor actorBase = actor.Base;
        bool hadPhenotype = actor.E.TryGetComponent(out CreaturePhenotype previous);
        bool morphChanged = !hadPhenotype ||
                            !string.Equals(previous.MorphId, compiled.Morph.id, StringComparison.Ordinal);
        if (morphChanged && !string.Equals(actorBase.asset.id, compiled.Morph.ActorAssetId, StringComparison.Ordinal))
        {
            ActorAsset targetAsset = AssetManager.actor_library.get(compiled.Morph.ActorAssetId);
            if (targetAsset == null)
            {
                failReason = "creature_phenotype.morph_actor_missing";
                return false;
            }

            // 形态切换不重放出生特性，也不重新检查可获得修炼体系；能力全部由身体本身提供。
            actorBase = ActorTransformationService.TransformInPlace(
                actorBase, targetAsset, ActorTransformationOptions.MorphSwitch);
            if (actorBase == null || actorBase.isRekt())
            {
                failReason = "creature_phenotype.morph_transform_failed";
                return false;
            }
        }

        int revision = hadPhenotype ? previous.Revision + 1 : 1;
        var expressed = new CreaturePhenotype(plan, compiled, revision);
        if (actor.E.HasComponent<CreaturePhenotype>())
            actor.E.GetComponent<CreaturePhenotype>() = expressed;
        else
            actor.E.AddComponent(expressed);

        RemoveOutdatedEffectStatuses(actor, expressed);
        actor.MarkCultiwayStatsDirty();

        CreaturePhenotypeEvents.PublishExpressed(new CreaturePhenotypeExpressedEvent(
            actor, expressed, hadPhenotype ? previous : default, !hadPhenotype, morphChanged));
        phenotype = expressed;
        return true;
    }

    /// <summary>移除由已经不存在的器官产生、且声明为跟随来源消失的持续状态。</summary>
    private static void RemoveOutdatedEffectStatuses(ActorExtend actor, CreaturePhenotype current)
    {
        long actorId = actor.Base.data.id;
        foreach (Entity status in actor.GetStatuses())
        {
            if (!status.TryGetComponent(out CreatureEffectStatusSource source)) continue;
            if (source.OwnerActorId != actorId) continue;
            if (source.PhenotypeRevision == current.Revision) continue;
            if (source.RemovalPolicy == CreatureStatusRemovalPolicy.PersistUntilExpiry) continue;
            if (HasOrgan(current, source.SlotId, source.OrganId))
            {
                // 器官仍在身体上，只把来源记录刷新到当前身体变更序号。
                status.GetComponent<CreatureEffectStatusSource>().PhenotypeRevision = current.Revision;
                continue;
            }

            actor.RemoveSharedStatus(status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
        }
    }

    /// <summary>判断当前身体是否仍然拥有指定位置上的指定器官。</summary>
    private static bool HasOrgan(CreaturePhenotype phenotype, string slotId, string organId)
    {
        foreach (CreatureOrganEntry organ in phenotype.Organs)
        {
            if (string.Equals(organ.SlotId, slotId, StringComparison.Ordinal) &&
                string.Equals(organ.OrganId, organId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
