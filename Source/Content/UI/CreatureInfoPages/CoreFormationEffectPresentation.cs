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

/// <summary>把当前角色实际生效的核心形成机制格式化为详情页提示文本。</summary>
internal static class CoreFormationEffectPresentation
{
    /// <summary>解析当前形成效果，并生成包含机制说明、倍率、概率、冷却和实时状态的文本。</summary>
    public static string BuildTooltip(ActorExtend actor)
    {
        var effects = new List<CoreFormationResolvedEffect>(CoreFormationGrantRuntime.MaxEffects);
        CoreFormationEffectResolver.Resolve(actor, effects);
        if (effects.Count == 0) return "Cultiway.CoreFormation.Page.Effects.Empty".Localize();
        CoreFormationEffectResolver.Synchronize(actor, effects);
        var text = new StringBuilder(640);
        for (var i = 0; i < effects.Count; i++)
        {
            if (i > 0) text.AppendLine();
            CoreFormationResolvedEffect effect = effects[i];
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.Source".Localize(),
                effect.Atom.GetName(), effect.Definition.GetName()));
            string description = effect.Definition.GetDescription();
            if (!string.IsNullOrEmpty(description)) text.AppendLine(description);
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.Potency".Localize(), effect.Potency));
            if (effect.Definition.base_chance < 1f || effect.Definition.max_chance < 1f)
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ProcChance".Localize(),
                    effect.ProcChance));
            if (effect.Definition.cooldown > 0f)
                text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.InternalCooldown".Localize(),
                    effect.Definition.cooldown));

            bool hasState = CoreFormationStateService.TryGet(
                actor,
                effect,
                out _,
                out CoreFormationEffectState state);
            AppendActive(text, actor, effect, hasState);
            AppendRuntimeState(text, actor, effect, hasState, state);
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>追加主动能力的固定消耗、持续时间、范围和当前冷却。</summary>
    private static void AppendActive(
        StringBuilder text,
        ActorExtend actor,
        in CoreFormationResolvedEffect effect,
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
        text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ActiveCooldown".Localize(), active.cooldown));
        float cooldownRemaining = SkillCooldownService.GetRemaining(actor, active.SkillContainer);
        if (cooldownRemaining > 0f)
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ActiveCooldownRemaining".Localize(),
                cooldownRemaining));
        if (active.duration > 0f && hasState)
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ActiveRemaining".Localize(),
                CoreFormationStateService.GetRemaining(actor, effect)));
    }

    /// <summary>按效果族追加护盾、储备、蓄力、适应和层数等有意义的实时状态。</summary>
    private static void AppendRuntimeState(
        StringBuilder text,
        ActorExtend actor,
        in CoreFormationResolvedEffect effect,
        bool hasState,
        CoreFormationEffectState state)
    {
        float cooldownRemaining = SkillCooldownService.GetRemaining(actor, effect.Definition.CooldownSkill);
        if (cooldownRemaining > 0f)
            text.AppendLine(string.Format("Cultiway.CoreFormation.Effect.ProcCooldownRemaining".Localize(),
                cooldownRemaining));
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
}
