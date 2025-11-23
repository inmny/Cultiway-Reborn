using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Content.Components.Skill;
using Cultiway.Utils.Extension;
using UnityEngine;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

public class SkillModifiers : ExtendLibrary<SkillModifierAsset, SkillModifiers>
{
    private const string KillOverrideTag = "kill_override";

    [AssetId(PlaceholderModifier.PlaceholderAssetId)]
    public static SkillModifierAsset Placeholder { get; private set; }

    public static SkillModifierAsset Slow { get; private set; }
    public static SkillModifierAsset Burn { get; private set; }
    public static SkillModifierAsset Freeze { get; private set; }
    public static SkillModifierAsset Poison { get; private set; }
    public static SkillModifierAsset Explosion { get; private set; }
    public static SkillModifierAsset Haste { get; private set; }
    public static SkillModifierAsset Proficiency { get; private set; }
    public static SkillModifierAsset Empower { get; private set; }
    public static SkillModifierAsset Knockback { get; private set; }
    public static SkillModifierAsset Volley { get; private set; }

    public static SkillModifierAsset LockOn { get; private set; }
    public static SkillModifierAsset Huge { get; private set; }
    public static SkillModifierAsset Weaken { get; private set; }
    public static SkillModifierAsset ArmorBreak { get; private set; }
    public static SkillModifierAsset Gravity { get; private set; }
    public static SkillModifierAsset Daze { get; private set; }

    public static SkillModifierAsset Mercy { get; private set; }
    public static SkillModifierAsset Chaos { get; private set; }
    public static SkillModifierAsset Swap { get; private set; }
    public static SkillModifierAsset RandomAffix { get; private set; }
    public static SkillModifierAsset Burnout { get; private set; }
    public static SkillModifierAsset Combo { get; private set; }

    public static SkillModifierAsset Silence { get; private set; }
    public static SkillModifierAsset DeathSentence { get; private set; }
    public static SkillModifierAsset ReincarnationTrial { get; private set; }
    public static SkillModifierAsset EternalCurse { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        Setup<PlaceholderModifier>(Placeholder, SkillModifierRarity.Common);

        Setup<SlowModifier>(Slow, SkillModifierRarity.Common);
        Slow.OnAddOrUpgrade = AddOrUpgradeSlow;
        Slow.OnEffectObj = ApplySlowEffect;
        Setup<BurnModifier>(Burn, SkillModifierRarity.Common);
        Setup<FreezeModifier>(Freeze, SkillModifierRarity.Common);
        Setup<PoisonModifier>(Poison, SkillModifierRarity.Common);
        Setup<ExplosionModifier>(Explosion, SkillModifierRarity.Common);
        Setup<HasteModifier>(Haste, SkillModifierRarity.Common);
        Setup<ProficiencyModifier>(Proficiency, SkillModifierRarity.Common);
        Setup<EmpowerModifier>(Empower, SkillModifierRarity.Common);
        Setup<KnockbackModifier>(Knockback, SkillModifierRarity.Common);

        Setup<LockOnModifier>(LockOn, SkillModifierRarity.Rare);
        Setup<HugeModifier>(Huge, SkillModifierRarity.Rare);
        Setup<WeakenModifier>(Weaken, SkillModifierRarity.Rare);
        Setup<ArmorBreakModifier>(ArmorBreak, SkillModifierRarity.Rare);
        Setup<GravityModifier>(Gravity, SkillModifierRarity.Rare);
        Setup<DazeModifier>(Daze, SkillModifierRarity.Rare);

        Setup<MercyModifier>(Mercy, SkillModifierRarity.Epic, KillOverrideTag);
        Setup<ChaosModifier>(Chaos, SkillModifierRarity.Epic);
        Setup<SwapModifier>(Swap, SkillModifierRarity.Epic);
        Setup<RandomAffixModifier>(RandomAffix, SkillModifierRarity.Epic);
        Setup<BurnoutModifier>(Burnout, SkillModifierRarity.Epic);
        Setup<ComboModifier>(Combo, SkillModifierRarity.Epic);

        Setup<SilenceModifier>(Silence, SkillModifierRarity.Legendary);
        Setup<DeathSentenceModifier>(DeathSentence, SkillModifierRarity.Legendary, KillOverrideTag);
        Setup<ReincarnationTrialModifier>(ReincarnationTrial, SkillModifierRarity.Legendary);
        Setup<EternalCurseModifier>(EternalCurse, SkillModifierRarity.Legendary);
    }

    private void Setup<TModifier>(SkillModifierAsset asset, SkillModifierRarity rarity, params string[] conflictTags)
        where TModifier : struct, IModifier
    {
        foreach (var tag in conflictTags)
        {
            asset.ConflictTags.Add(tag);
        }

        asset.Rarity = rarity;
        asset.OnAddOrUpgrade = builder => AddModifier<TModifier>(builder);
        asset.GetDescription = entity =>
        {
            if (entity.HasComponent<TModifier>())
            {
                var modifier = entity.GetComponent<TModifier>();
                var value = modifier.GetValue();
                if (string.IsNullOrEmpty(value)) return modifier.GetKey();
                return $"{modifier.GetKey()}: {modifier.GetValue()}";
            }
            return null;
        };
    }

    private static bool AddModifier<TModifier>(SkillContainerBuilder builder) where TModifier : struct, IModifier
    {
        if (builder.HasModifier<TModifier>()) return false;
        builder.AddModifier(new TModifier());
        return true;
    }

    private static bool AddOrUpgradeSlow(SkillContainerBuilder builder)
    {
        const float minDuration = 3f;
        const float maxDuration = 5f;
        const float minStrength = 0.3f;
        const float maxStrength = 0.5f;
        const float durationStep = 0.5f;
        const float strengthStep = 0.05f;

        if (!builder.HasModifier<SlowModifier>())
        {
            builder.AddModifier(new SlowModifier
            {
                Duration = minDuration,
                Strength = minStrength
            });
            return true;
        }

        var modifier = builder.GetModifier<SlowModifier>();
        var upgradeDuration = Random.value < 0.5f;
        var changed = false;
        if (upgradeDuration && modifier.Duration < maxDuration)
        {
            modifier.Duration = Mathf.Min(maxDuration, modifier.Duration + durationStep);
            changed = true;
        }
        else if (!upgradeDuration && modifier.Strength < maxStrength)
        {
            modifier.Strength = Mathf.Min(maxStrength, modifier.Strength + strengthStep);
            changed = true;
        }
        else if (modifier.Duration < maxDuration)
        {
            modifier.Duration = Mathf.Min(maxDuration, modifier.Duration + durationStep);
            changed = true;
        }
        else if (modifier.Strength < maxStrength)
        {
            modifier.Strength = Mathf.Min(maxStrength, modifier.Strength + strengthStep);
            changed = true;
        }

        if (!changed) return false;
        builder.SetModifier(modifier);
        return true;
    }

    // 击中单位时附加减速状态
    private static void ApplySlowEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out SlowModifier slow)) return;

        var duration = Mathf.Clamp(slow.Duration, 0f, 999f);
        var strength = Mathf.Clamp(slow.Strength, 0f, 1f);
        if (duration <= 0f || strength <= 0f) return;

        var status = StatusEffects.Slow.NewEntity();
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        status.AddComponent(new StatusStatsMultiplier
        {
            Value = strength
        });
        target.a.GetExtend().AddSharedStatus(status);
    }
}
