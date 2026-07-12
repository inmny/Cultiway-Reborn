using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 从源法术构建、校验并选出一个可发布的改进候选。
/// </summary>
internal static class MagicSpellImprovementPlanner
{
    /// <summary>
    /// 为源法术的每个可升级词条各构建一个独立候选，过滤无效候选后按权重选出一个版本。
    /// </summary>
    internal static Entity SelectCandidate(Entity source, MagicSpellProfile sourceProfile, int maxRing)
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

    private static bool ValidateCandidate(Entity candidate)
    {
        if (!SkillCastResourceResolver.UsesResource(candidate, SkillCastResources.Mana)) return false;
        var export = new SkillBlueprintExporter().Export(candidate);
        if (!export.Success) return false;
        return new SkillBlueprintValidator().Validate(export.Blueprint).IsCompatible;
    }

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

    private static void RemoveRuntimeTags(Entity container)
    {
        if (container.Tags.Has<TagOccupied>()) container.RemoveTag<TagOccupied>();
        if (container.Tags.Has<TagRecycle>()) container.RemoveTag<TagRecycle>();
    }

    private static void UpdateBlueprintOrigin(Entity container, string signature)
    {
        if (!container.HasComponent<SkillBlueprintOrigin>()) return;
        ref var origin = ref container.GetComponent<SkillBlueprintOrigin>();
        origin.Revision = Math.Max(1, origin.Revision + 1);
        origin.Signature = signature;
    }

    /// <summary>
    /// 候选在选中前不归施法者或魔网所有，因此未选中时必须回收。
    /// </summary>
    private sealed class ImprovementCandidate
    {
        public Entity Container;
        public float Weight;
    }
}
