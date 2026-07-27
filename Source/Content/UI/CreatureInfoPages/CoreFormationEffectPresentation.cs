using System;
using System.Collections.Generic;
using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>核心构成 Tooltip 中一条原版标签-数值行。</summary>
internal readonly struct CoreFormationEffectTooltipLine
{
    /// <summary>左侧标签。</summary>
    public readonly string Label;

    /// <summary>右侧数值。</summary>
    public readonly string Value;

    /// <summary>创建一条已经完成本地化的标签-数值行。</summary>
    public CoreFormationEffectTooltipLine(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

/// <summary>单个核心构成原子的完整 Tooltip 展示模型。</summary>
internal sealed class CoreFormationEffectTooltipModel
{
    /// <summary>Tooltip 标题。</summary>
    public string Title;

    /// <summary>来源、机制说明、主动能力和实时状态组成的正文。</summary>
    public string Description;

    /// <summary>交给原版 Tooltip 绘制的标签-数值行。</summary>
    public readonly List<CoreFormationEffectTooltipLine> Lines = new();
}

/// <summary>把单个核心形成原子提供的机制格式化为详情页提示文本。</summary>
internal static class CoreFormationEffectPresentation
{
    /// <summary>生成原子的来源、机制说明、数值行和实时状态展示模型。</summary>
    public static CoreFormationEffectTooltipModel BuildAtomTooltip(
        ActorExtend actor,
        CoreFormationAtomAsset atom,
        string origin,
        IList<CoreFormationResolvedEffect> resolvedEffects,
        bool includeRuntimeState)
    {
        var model = new CoreFormationEffectTooltipModel
        {
            Title = atom.GetName()
        };
        var text = new StringBuilder(640);
        text.Append(origin);
        CoreFormationEffectDefinition[] definitions = atom.effects ?? [];
        bool multipleEffects = CountDisplayableEffects(definitions) > 1;
        var displayedEffects = 0;
        for (var i = 0; i < definitions.Length; i++)
        {
            CoreFormationEffectDefinition definition = definitions[i];
            if (definition == null || string.IsNullOrEmpty(definition.family_id)) continue;
            if (displayedEffects == 0)
            {
                text.AppendLine();
                text.AppendLine();
                text.AppendLine("Cultiway.CoreFormation.Page.Effects.Title".Localize());
            }
            else
            {
                text.AppendLine();
            }
            displayedEffects++;

            text.AppendLine(definition.GetName());
            string description = definition.GetDescription();
            if (!string.IsNullOrEmpty(description)) text.AppendLine(description);

            if (TryFindDefinition(resolvedEffects, definition, out CoreFormationResolvedEffect effect))
            {
                AppendResolved(model.Lines, text, actor, effect, includeRuntimeState, multipleEffects);
                continue;
            }
            if (TryFindFamily(resolvedEffects, definition.family_id, out CoreFormationResolvedEffect replacement))
            {
                text.AppendLine(string.Format(
                    "Cultiway.CoreFormation.Effect.Superseded".Localize(),
                    replacement.Atom.GetName(),
                    replacement.Definition.GetName()));
            }
        }
        model.Description = text.ToString().TrimEnd();
        return model;
    }

    /// <summary>追加当前实际生效定义的倍率、概率、冷却、主动能力和实时状态。</summary>
    private static void AppendResolved(
        List<CoreFormationEffectTooltipLine> lines,
        StringBuilder text,
        ActorExtend actor,
        in CoreFormationResolvedEffect effect,
        bool includeRuntimeState,
        bool showEffectHeading)
    {
        if (showEffectHeading)
            lines.Add(new CoreFormationEffectTooltipLine(effect.Definition.GetName(), string.Empty));
        AddValueLine(lines,
            "Cultiway.CoreFormation.Effect.Potency",
            "Cultiway.CoreFormation.Effect.Value.Multiplier",
            effect.Potency);
        if (effect.Definition.base_chance < 1f || effect.Definition.max_chance < 1f)
            AddValueLine(lines,
                "Cultiway.CoreFormation.Effect.ProcChance",
                "Cultiway.CoreFormation.Effect.Value.Percent",
                effect.ProcChance);
        if (effect.Definition.cooldown > 0f)
            AddValueLine(lines,
                "Cultiway.CoreFormation.Effect.InternalCooldown",
                "Cultiway.CoreFormation.Effect.Value.Seconds",
                effect.Definition.cooldown);

        CoreFormationEffectState state = default;
        bool hasState = includeRuntimeState &&
                        CoreFormationStateService.TryGet(
                            actor,
                            effect,
                            out _,
                            out state);
        AppendActive(lines, text, actor, effect, includeRuntimeState, hasState);
        if (includeRuntimeState)
            AppendRuntimeState(lines, text, actor, effect, hasState, state);
    }

    /// <summary>追加主动能力的固定消耗、持续时间、范围和当前冷却。</summary>
    private static void AppendActive(
        List<CoreFormationEffectTooltipLine> lines,
        StringBuilder text,
        ActorExtend actor,
        in CoreFormationResolvedEffect effect,
        bool includeRuntimeState,
        bool hasState)
    {
        CoreFormationActiveProfile active = effect.Definition.active;
        if (active == null) return;
        text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.Active".Localize(), active.GetName()));
        text.AppendLine(string.Format(
            "Cultiway.CoreFormation.Effect.ActiveCost".Localize(),
            SkillCastCost.CalculateStepDemand(active.SkillContainer)));
        if (active.duration > 0f)
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ActiveDuration".Localize(),
                active.duration));
        if (active.range > 0f || active.radius > 0f)
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ActiveRange".Localize(),
                active.range, active.radius));
        AddValueLine(lines,
            "Cultiway.CoreFormation.Effect.ActiveCooldown",
            "Cultiway.CoreFormation.Effect.Value.Seconds",
            active.cooldown);
        if (includeRuntimeState)
        {
            float cooldownRemaining = SkillCooldownService.GetRemaining(actor, active.SkillContainer);
            if (cooldownRemaining > 0f)
                AddValueLine(lines,
                    "Cultiway.CoreFormation.Effect.ActiveCooldownRemaining",
                    "Cultiway.CoreFormation.Effect.Value.Seconds",
                    cooldownRemaining);
        }
        if (active.duration > 0f && hasState)
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ActiveRemaining".Localize(),
                CoreFormationStateService.GetRemaining(actor, effect)));
    }

    /// <summary>按效果族追加护盾、储备、蓄力、适应和层数等有意义的实时状态。</summary>
    private static void AppendRuntimeState(
        List<CoreFormationEffectTooltipLine> lines,
        StringBuilder text,
        ActorExtend actor,
        in CoreFormationResolvedEffect effect,
        bool hasState,
        CoreFormationEffectState state)
    {
        float cooldownRemaining = SkillCooldownService.GetRemaining(actor, effect.Definition.CooldownSkill);
        if (cooldownRemaining > 0f)
            AddValueLine(lines,
                "Cultiway.CoreFormation.Effect.ProcCooldownRemaining",
                "Cultiway.CoreFormation.Effect.Value.Seconds",
                cooldownRemaining);
        if (!hasState) return;
        switch (effect.Definition.family_id)
        {
            case CoreFormationEffectFamilies.Earth when state.value > 0f:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Ward".Localize(), state.value));
                break;
            case CoreFormationEffectFamilies.Balanced when state.value > 0f:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Adaptation".Localize(),
                    state.value));
                break;
            case CoreFormationEffectFamilies.Condensed when state.charges > 0:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Charges".Localize(),
                    state.charges));
                break;
            case CoreFormationEffectFamilies.Vital when state.value > 0f:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.StoredHealing".Localize(),
                    state.value));
                break;
            case CoreFormationEffectFamilies.Spiritual when state.charges > 0:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Echoes".Localize(),
                    state.charges));
                break;
            case CoreFormationEffectFamilies.Reservoir when state.value > 0f:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Reserve".Localize(),
                    state.value));
                break;
            case CoreFormationEffectFamilies.Dragon when state.counter > 0:
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Dragon".Localize(),
                    state.counter));
                break;
            case CoreFormationEffectFamilies.FivePhase:
                int phase = Math.Max(ElementIndex.Iron, Math.Min(ElementIndex.Earth, state.phase));
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.State.Phase".Localize(),
                    ElementIndex.ElementNames[phase].Localize()));
                break;
        }
    }

    /// <summary>把本地化标签与格式化数值追加为原版 Tooltip 的标签-数值行。</summary>
    private static void AddValueLine(
        List<CoreFormationEffectTooltipLine> lines,
        string labelKey,
        string valueFormatKey,
        float value)
    {
        lines.Add(new CoreFormationEffectTooltipLine(
            labelKey.Localize(),
            string.Format(valueFormatKey.Localize(), value)));
    }

    /// <summary>统计当前原子中具备稳定效果族的可展示定义数量。</summary>
    private static int CountDisplayableEffects(CoreFormationEffectDefinition[] definitions)
    {
        var count = 0;
        for (var i = 0; i < definitions.Length; i++)
        {
            CoreFormationEffectDefinition definition = definitions[i];
            if (definition != null && !string.IsNullOrEmpty(definition.family_id)) count++;
        }
        return count;
    }

    /// <summary>在合并后的实际效果中查找完全对应当前定义的结果。</summary>
    private static bool TryFindDefinition(
        IList<CoreFormationResolvedEffect> effects,
        CoreFormationEffectDefinition definition,
        out CoreFormationResolvedEffect resolved)
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if (!ReferenceEquals(effects[i].Definition, definition)) continue;
            resolved = effects[i];
            return true;
        }
        resolved = default;
        return false;
    }

    /// <summary>查找取代当前定义的同族实际效果。</summary>
    private static bool TryFindFamily(
        IList<CoreFormationResolvedEffect> effects,
        string familyId,
        out CoreFormationResolvedEffect resolved)
    {
        for (var i = 0; i < effects.Count; i++)
        {
            if (!string.Equals(effects[i].Definition.family_id, familyId, StringComparison.Ordinal)) continue;
            resolved = effects[i];
            return true;
        }
        resolved = default;
        return false;
    }
}
