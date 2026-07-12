using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 负责将法术的实际使用积累转化为个人改进，并把改进结果上传魔网。
/// </summary>
public static class MagicSpellProgressionRules
{
    /// <summary>
    /// 保存一个已经完成构建和校验的改进候选及其随机抽取权重。
    /// 候选在选中前不归施法者或魔网所有，因此未选中时必须回收。
    /// </summary>
    private sealed class ImprovementCandidate
    {
        /// <summary>改进后的临时法术容器。</summary>
        public Entity Container;

        /// <summary>综合词条稀有度、配置权重和法术资产偏好后的抽取权重。</summary>
        public float Weight;
    }

    /// <summary>
    /// 记录一次已完成的施法序列；齐射、连射和散射产生的多发弹体仍只计作一次使用。
    /// </summary>
    public static void RecordCompletedCast(ActorExtend actor, Entity castContainer, int emittedCount)
    {
        // 只处理活着的魔法师实际完成的施法序列；空序列和非 mana 技能不产生使用积累。
        if (actor == null || actor.Base.isRekt() || emittedCount <= 0 || !actor.HasCultisys<Magic>()) return;
        if (!MagicLearningRules.IsManaSkill(castContainer)) return;

        // 先同步技能所有权和知识关系。若旧版本刚被替换，则按法术族找到施法者当前掌握的版本。
        MagicLearningRules.EnsureKnowledgeRelations(actor);
        var trackedContainer = ResolveTrackedContainer(actor, castContainer);
        if (trackedContainer.IsNull || !MagicLearningRules.EnsureKnowledge(actor, trackedContainer)) return;

        // 档案提供环位和法术族信息；无有效档案的容器不能进入改进流程。
        var profile = ResolveProfile(trackedContainer);
        if (profile == null) return;

        // 每个完成的施法序列固定增加一点进度，多发弹体只通过 emittedCount 判断施法是否有效。
        var now = GetWorldTime();
        ref var knowledge = ref actor.E.GetRelation<MagicSpellKnowledgeRelation, Entity>(trackedContainer);
        knowledge.TotalCastCount++;
        knowledge.ImprovementProgress += 1f;
        knowledge.LastUsedWorldTime = now;

        // 未到重试时间或使用积累不足时，仅保存本次使用记录，不生成临时候选。
        if (now < knowledge.NextImprovementAttemptWorldTime) return;
        var threshold = ResolveImprovementThreshold(actor, profile, knowledge.ImprovementCount);
        if (knowledge.ImprovementProgress < threshold) return;

        // 改进会替换知识关系，因此先复制旧状态；失败时保留全部进度并设置退避时间。
        var snapshot = knowledge;
        if (TryImproveAndPublish(actor, trackedContainer, profile, snapshot, threshold, now)) return;

        ref var current = ref actor.E.GetRelation<MagicSpellKnowledgeRelation, Entity>(trackedContainer);
        current.NextImprovementAttemptWorldTime = now +
                                                  MagicSetting.MagicSpellImprovementRetryYears *
                                                  TimeScales.SecPerYear;
    }

    /// <summary>
    /// 根据法术环位、既有改进次数和施法者智力计算下一次改进所需的使用积累。
    /// </summary>
    public static float ResolveImprovementThreshold(ActorExtend actor, MagicSpellProfile profile,
        int improvementCount)
    {
        var ringFactor = 1f + Mathf.Max(0, profile?.Ring ?? 0) *
            MagicSetting.MagicSpellImprovementRingUseFactor;
        var improvementFactor = 1f + Mathf.Max(0, improvementCount) *
            MagicSetting.MagicSpellImprovementCountUseFactor;
        var intelligence = actor == null ? 0f : Mathf.Max(0f, actor.GetStat(S.intelligence));
        var intelligenceFactor = Mathf.Sqrt(1f + intelligence /
            MagicSetting.MagicSpellImprovementIntelligenceScale);
        return Mathf.Max(1f, MagicSetting.MagicSpellImprovementBaseUses * ringFactor * improvementFactor /
                             intelligenceFactor);
    }

    /// <summary>
    /// 生成一个合法改进版本、上传魔网取得规范容器，并替换施法者当前掌握的源版本。
    /// </summary>
    private static bool TryImproveAndPublish(ActorExtend actor, Entity source, MagicSpellProfile sourceProfile,
        MagicSpellKnowledgeRelation knowledge, float threshold, double now)
    {
        // 当前魔法等级决定候选允许达到的最高环位，避免改进出施法者无法理解的版本。
        ref var magic = ref actor.GetCultisys<Magic>();
        var candidate = SelectImprovementCandidate(source, sourceProfile,
            Cultisyses.GetMaxSpellRing(magic.CurrLevel));
        if (candidate.IsNull) return false;

        // 若施法者已经掌握结构完全相同的其他技能，则该候选不能替换源技能。
        var candidateSignature = SkillContainerSignature.Build(candidate);
        if (HasOwnedDuplicate(actor, source, candidateSignature))
        {
            SkillBlueprintCompiler.Recycle(candidate);
            return false;
        }

        // 先交给魔网进行全世界结构去重；新增时候选成为规范容器，重复时改用已有规范容器。
        var manager = MagicWebManager.Instance;
        var publish = manager?.TryPublish(candidate) ??
                      new MagicWebPublishResult(MagicWebPublishStatus.Unavailable);
        if (!publish.Success)
        {
            SkillBlueprintCompiler.Recycle(candidate);
            return false;
        }

        var canonical = publish.Container;
        if (publish.Status == MagicWebPublishStatus.Duplicate)
        {
            // 重复候选未被魔网接管，后续替换直接使用魔网返回的规范容器。
            SkillBlueprintCompiler.Recycle(candidate);
        }

        // 上传成功后原子替换施法者的 SkillMasterRelation；失败时不修改旧知识关系。
        var replace = SkillOwnershipService.Replace(actor, source, canonical);
        if (replace != SkillOwnershipResult.Replaced) return false;

        // 将累计使用、剩余进度和改进代数迁移到新版本，并把来源标记为自行改进。
        actor.E.RemoveRelation<MagicSpellKnowledgeRelation>(source);
        actor.E.AddRelation(new MagicSpellKnowledgeRelation
        {
            SkillContainer = canonical,
            LearnedWorldTime = now,
            Source = MagicSpellKnowledgeSource.SelfCreated,
            TotalCastCount = knowledge.TotalCastCount,
            ImprovementProgress = Mathf.Max(0f, knowledge.ImprovementProgress - threshold),
            ImprovementCount = knowledge.ImprovementCount + 1,
            LastUsedWorldTime = knowledge.LastUsedWorldTime,
            LastImprovedWorldTime = now,
            NextImprovementAttemptWorldTime = now +
                                              MagicSetting.MagicSpellImprovementRetryYears *
                                              TimeScales.SecPerYear
        });
        // 本次上传和采用都属于明确访问，刷新动态魔网条目的存活时间。
        manager.Touch(canonical);
        ModClass.LogInfo($"[{actor}] 改进并上传法术: {source.Id} -> {canonical.Id} ({publish.Status})");
        return true;
    }

    /// <summary>
    /// 为源法术的每个可升级词条各构建一个独立候选，过滤无效候选后按权重选出一个版本。
    /// </summary>
    private static Entity SelectImprovementCandidate(Entity source, MagicSpellProfile sourceProfile, int maxRing)
    {
        var candidates = new List<ImprovementCandidate>();
        var sourceSignature = SkillContainerSignature.Build(source);
        foreach (var modifier in ModClass.I.SkillV3.ModifierLib.list)
        {
            // 普通候选必须允许编辑；轨迹改写由独立词条代理，因此作为显式例外参与改进。
            var canImproveTrajectory = modifier == SkillModifierLibrary.SetTrajectory;
            if (modifier == null || modifier.IsDisabled ||
                (!modifier.EditorSelectable && !canImproveTrajectory) ||
                modifier.OnAddOrUpgrade == null || modifier.WeightMod <= 0f) continue;

            Entity clone = default;
            try
            {
                // 魔网和施法者持有的容器都视为不可变对象，所有试改都在无主克隆上进行。
                clone = source.Store.CloneEntity(source);
                RemoveRuntimeTags(clone);
                var builder = new SkillContainerBuilder(clone);
                if (!modifier.OnAddOrUpgrade(builder))
                {
                    // 委托返回 false 表示该词条在当前版本上既不能添加也不能继续升级。
                    SkillBlueprintCompiler.Recycle(clone);
                    continue;
                }

                // RuleOnly 会刷新派生组件、ItemLevel 和规则名称，但不会触发运行时 AI 命名。
                builder.Build(SkillContainerBuildMode.RuleOnly);
                var signature = SkillContainerSignature.Build(clone);
                if (string.IsNullOrEmpty(signature) || signature == sourceSignature)
                {
                    // 结构未发生变化的候选不构成新版本，也不能上传魔网。
                    SkillBlueprintCompiler.Recycle(clone);
                    continue;
                }

                // 候选必须仍属同一法术族、没有超过境界环位上限，并通过完整蓝图兼容校验。
                var profile = MagicSpellProfile.Evaluate(clone);
                if (profile == null || profile.Ring > maxRing ||
                    profile.FamilySignature != sourceProfile.FamilySignature ||
                    !ValidateCandidate(clone))
                {
                    SkillBlueprintCompiler.Recycle(clone);
                    continue;
                }

                // 更新万法阁来源组件中的版本信息，避免克隆继续携带旧结构签名。
                UpdateBlueprintOrigin(clone, signature);

                // 已有词条升级和新增词条使用不同倍率，再叠加稀有度、词条及法术资产权重。
                var alreadyHas = HasModifier(source, modifier);
                var modeWeight = alreadyHas
                    ? MagicSetting.MagicSpellImprovementExistingModifierWeight
                    : MagicSetting.MagicSpellImprovementNewModifierWeight;
                var asset = clone.GetComponent<SkillContainer>().Asset;
                var weight = modifier.Rarity.Weight() * modifier.WeightMod * modeWeight *
                             asset.GetModifierWeightMultiplier(modifier);
                if (weight <= 0f)
                {
                    SkillBlueprintCompiler.Recycle(clone);
                    continue;
                }

                candidates.Add(new ImprovementCandidate { Container = clone, Weight = weight });
            }
            catch (Exception exception)
            {
                // 单个词条构建失败不应中断整轮改进，其临时实体仍必须进入回收流程。
                if (!clone.IsNull) SkillBlueprintCompiler.Recycle(clone);
                ModClass.LogError($"法术改进候选生成失败: {modifier.id}\n{exception}");
            }
        }

        if (candidates.Count == 0) return default;

        // 只保留抽中的临时容器，其余候选没有所有者，统一标记回收。
        var selected = WeightedSelect(candidates);
        foreach (var candidate in candidates)
        {
            if (candidate.Container != selected) SkillBlueprintCompiler.Recycle(candidate.Container);
        }
        return selected;
    }

    /// <summary>
    /// 按候选累计权重随机选择一个改进版本。
    /// </summary>
    private static Entity WeightedSelect(IReadOnlyList<ImprovementCandidate> candidates)
    {
        var total = candidates.Sum(candidate => candidate.Weight);
        var roll = Randy.randomFloat(0f, total);
        foreach (var candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0f) return candidate.Container;
        }
        return candidates[candidates.Count - 1].Container;
    }

    /// <summary>
    /// 检查候选仍消耗 mana，并能完整导出为通过兼容性验证的技能蓝图。
    /// </summary>
    private static bool ValidateCandidate(Entity candidate)
    {
        if (!MagicLearningRules.IsManaSkill(candidate)) return false;
        var export = new SkillBlueprintExporter().Export(candidate);
        if (!export.Success) return false;
        return new SkillBlueprintValidator().Validate(export.Blueprint).IsCompatible;
    }

    /// <summary>
    /// 判断容器是否已经持有指定词条；轨迹改写等特殊词条同样通过 IModifier 识别。
    /// </summary>
    private static bool HasModifier(Entity container, SkillModifierAsset asset)
    {
        foreach (var componentType in container.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(componentType)) continue;
            var modifier = (IModifier)container.GetComponent(componentType);
            if (modifier.ModifierAsset?.id == asset.id) return true;
        }
        return false;
    }

    /// <summary>
    /// 判断施法者除待替换源技能外，是否已经掌握结构签名相同的技能。
    /// </summary>
    private static bool HasOwnedDuplicate(ActorExtend actor, Entity source, string signature)
    {
        foreach (var owned in actor.GetLearnedSkillsInOrder())
        {
            if (owned.IsNull || owned == source) continue;
            if (SkillContainerSignature.Build(owned) == signature) return true;
        }
        return false;
    }

    /// <summary>
    /// 取得本次使用应累计到的当前技能版本；旧版本刚被替换时按法术族映射到新版本。
    /// </summary>
    private static Entity ResolveTrackedContainer(ActorExtend actor, Entity castContainer)
    {
        if (actor.OwnsLearnedSkill(castContainer)) return castContainer;
        var castProfile = ResolveProfile(castContainer);
        if (castProfile == null) return default;

        foreach (var relation in actor.E.GetRelations<MagicSpellKnowledgeRelation>())
        {
            var known = relation.SkillContainer;
            if (known.IsNull || !actor.OwnsLearnedSkill(known)) continue;
            var knownProfile = ResolveProfile(known);
            if (knownProfile?.FamilySignature == castProfile.FamilySignature) return known;
        }
        return default;
    }

    /// <summary>
    /// 优先复用魔网收录时冻结的档案，非魔网容器则即时计算档案。
    /// </summary>
    private static MagicSpellProfile ResolveProfile(Entity container)
    {
        return MagicWebManager.Instance?.TryGetProfile(container, out var profile) == true
            ? profile
            : MagicSpellProfile.Evaluate(container);
    }

    /// <summary>
    /// 清除从源容器克隆来的占用和回收标签，使候选可以正常构建及上传。
    /// </summary>
    private static void RemoveRuntimeTags(Entity container)
    {
        if (container.Tags.Has<TagOccupied>()) container.RemoveTag<TagOccupied>();
        if (container.Tags.Has<TagRecycle>()) container.RemoveTag<TagRecycle>();
    }

    /// <summary>
    /// 若容器来自万法阁蓝图，则递增其运行时修订号并写入新的结构签名。
    /// </summary>
    private static void UpdateBlueprintOrigin(Entity container, string signature)
    {
        if (!container.HasComponent<SkillBlueprintOrigin>()) return;
        ref var origin = ref container.GetComponent<SkillBlueprintOrigin>();
        origin.Revision = Math.Max(1, origin.Revision + 1);
        origin.Signature = signature;
    }

    /// <summary>
    /// 返回当前世界时间；尚未进入世界时返回零。
    /// </summary>
    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }
}
