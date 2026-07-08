using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Content.Components.Skill;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;
using Friflo.Engine.ECS;
using ConflictTag = Cultiway.Core.SkillLibV3.SkillTags.Conflict;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;
using FormTag = Cultiway.Core.SkillLibV3.SkillTags.Form;
using SimilarityTag = Cultiway.Core.SkillLibV3.SkillTags.Similarity;

namespace Cultiway.Content;

public class SkillModifiers : ExtendLibrary<SkillModifierAsset, SkillModifiers>
{
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

    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        Setup<PlaceholderModifier>(Placeholder, SkillModifierRarity.Common);

        Setup<SlowModifier>(Slow, SkillModifierRarity.Common);
        Slow.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Slow);
        Slow.OnAddOrUpgrade = AddOrUpgradeSlow;
        Slow.OnEffectObj = ApplySlowEffect;
        Setup<BurnModifier>(Burn, SkillModifierRarity.Common);
        Burn.AddSimilarityTags(SimilarityTag.Dot, SimilarityTag.Burn, ElementTag.Fire);
        Burn.OnAddOrUpgrade = AddOrUpgradeBurn;
        Burn.OnEffectObj = ApplyBurnEffect;
        Setup<FreezeModifier>(Freeze, SkillModifierRarity.Common);
        Freeze.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Freeze);
        Freeze.OnAddOrUpgrade = AddOrUpgradeFreeze;
        Freeze.OnEffectObj = ApplyFreezeEffect;
        Setup<PoisonModifier>(Poison, SkillModifierRarity.Common);
        Poison.AddSimilarityTags(SimilarityTag.Dot, SimilarityTag.Poison);
        Poison.OnAddOrUpgrade = AddOrUpgradePoison;
        Poison.OnEffectObj = ApplyPoisonEffect;
        Setup<ExplosionModifier>(Explosion, SkillModifierRarity.Common);
        Explosion.AddSimilarityTags(FormTag.Aoe, SimilarityTag.Blast);
        Explosion.OnAddOrUpgrade = AddOrUpgradeExplosion;
        Explosion.OnEffectObj = ApplyExplosionEffect;
        Setup<HasteModifier>(Haste, SkillModifierRarity.Common);
        Haste.AddSimilarityTags(SimilarityTag.Speed, SimilarityTag.Projectile);
        Haste.OnAddOrUpgrade = AddOrUpgradeHaste;
        Haste.OnSetup = ApplyHasteOnSetup;
        Setup<ProficiencyModifier>(Proficiency, SkillModifierRarity.Common);
        Proficiency.AddSimilarityTags(SimilarityTag.Growth);
        Proficiency.OnAddOrUpgrade = AddOrUpgradeProficiency;
        Setup<EmpowerModifier>(Empower, SkillModifierRarity.Common);
        Empower.AddSimilarityTags(SimilarityTag.Power, SimilarityTag.Damage);
        Empower.OnAddOrUpgrade = AddOrUpgradeEmpower;
        Empower.OnSetup = ApplyEmpowerSetup;
        Setup<KnockbackModifier>(Knockback, SkillModifierRarity.Common);
        Knockback.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Displace);
        Knockback.OnAddOrUpgrade = AddOrUpgradeKnockback;
        Knockback.OnEffectObj = ApplyKnockbackEffect;
        Setup<VolleyModifier>(Volley, SkillModifierRarity.Common);
        Volley.AddSimilarityTags(SimilarityTag.Projectile, SimilarityTag.Burst);
        Volley.OnAddOrUpgrade = AddOrUpgradeVolley;
        Volley.OnSetup = ApplyVolleyOnSetup;

        Setup<HugeModifier>(Huge, SkillModifierRarity.Rare);
        Huge.AddSimilarityTags(SimilarityTag.Size, FormTag.Aoe);
        Huge.OnAddOrUpgrade = AddOrUpgradeHuge;
        Huge.OnSetup = ApplyHugeOnSetup;
        Setup<WeakenModifier>(Weaken, SkillModifierRarity.Rare);
        Weaken.AddSimilarityTags(SimilarityTag.Debuff, SimilarityTag.AttackDown);
        Weaken.OnAddOrUpgrade = AddOrUpgradeWeaken;
        Weaken.OnEffectObj = ApplyWeakenEffect;
        Setup<ArmorBreakModifier>(ArmorBreak, SkillModifierRarity.Rare);
        ArmorBreak.AddSimilarityTags(SimilarityTag.Debuff, SimilarityTag.ArmorDown);
        ArmorBreak.OnAddOrUpgrade = AddOrUpgradeArmorBreak;
        ArmorBreak.OnEffectObj = ApplyArmorBreakEffect;
        Setup<GravityModifier>(Gravity, SkillModifierRarity.Rare);
        Gravity.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Pull, FormTag.Aoe);
        Gravity.OnAddOrUpgrade = AddOrUpgradeGravity;
        Gravity.OnTravel = ApplyGravityTravel;
        Setup<DazeModifier>(Daze, SkillModifierRarity.Rare);
        Daze.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Stun);
        Daze.OnAddOrUpgrade = AddOrUpgradeDaze;
        Daze.OnEffectObj = ApplyDazeEffect;

        Setup<MercyModifier>(Mercy, SkillModifierRarity.Epic, ConflictTag.KillOverride);
        Mercy.AddSimilarityTags(SimilarityTag.Special);
        Mercy.OnAddOrUpgrade = AddOrUpgradeMercy;
        Mercy.OnSetup = ApplyMercyOnSetup;
        Mercy.OnEffectObj = ApplyMercyEffect;
        Setup<ChaosModifier>(Chaos, SkillModifierRarity.Epic);
        Chaos.AddSimilarityTags(SimilarityTag.Special, SimilarityTag.Random);
        Chaos.OnAddOrUpgrade = AddOrUpgradeChaos;
        Chaos.OnSetup = ApplyChaosOnSetup;
        Setup<SwapModifier>(Swap, SkillModifierRarity.Epic);
        Swap.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Swap);
        Swap.OnAddOrUpgrade = AddOrUpgradeSwap;
        Swap.OnEffectObj = ApplySwapEffect;
        Setup<RandomAffixModifier>(RandomAffix, SkillModifierRarity.Epic);
        RandomAffix.AddSimilarityTags(SimilarityTag.Special, SimilarityTag.Random);
        RandomAffix.OnAddOrUpgrade = AddOrUpgradeRandomAffix;
        RandomAffix.OnEffectObj = ApplyRandomAffixEffect;
        Setup<BurnoutModifier>(Burnout, SkillModifierRarity.Epic);
        Burnout.AddSimilarityTags(SimilarityTag.Dot, SimilarityTag.Burn);
        Burnout.OnAddOrUpgrade = AddOrUpgradeBurnout;
        Burnout.OnEffectObj = ApplyBurnoutEffect;
        Setup<ComboModifier>(Combo, SkillModifierRarity.Epic);
        Combo.AddSimilarityTags(SimilarityTag.Combo);
        Combo.OnAddOrUpgrade = AddOrUpgradeCombo;
        Combo.OnSetup = ApplyComboOnSetup;

        Setup<SilenceModifier>(Silence, SkillModifierRarity.Legendary);
        Silence.AddSimilarityTags(SimilarityTag.Control, SimilarityTag.Silence);
        Silence.OnAddOrUpgrade = AddOrUpgradeSilence;
        Silence.OnEffectObj = ApplySilenceEffect;
        Setup<DeathSentenceModifier>(DeathSentence, SkillModifierRarity.Legendary, ConflictTag.KillOverride);
        DeathSentence.AddSimilarityTags(SimilarityTag.Execute);
        DeathSentence.OnAddOrUpgrade = AddOrUpgradeDeathSentence;
        DeathSentence.OnEffectObj = ApplyDeathSentenceEffect;
        Setup<ReincarnationTrialModifier>(ReincarnationTrial, SkillModifierRarity.Legendary);
        ReincarnationTrial.AddSimilarityTags(SimilarityTag.Special);
        ReincarnationTrial.OnAddOrUpgrade = AddOrUpgradeReincarnationTrial;
        ReincarnationTrial.OnEffectObj = ApplyReincarnationTrialEffect;
        Setup<EternalCurseModifier>(EternalCurse, SkillModifierRarity.Legendary);
        EternalCurse.AddSimilarityTags(SimilarityTag.Curse, SimilarityTag.Dot);
        EternalCurse.OnAddOrUpgrade = AddOrUpgradeEternalCurse;
        EternalCurse.OnEffectObj = ApplyEternalCurseEffect;
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
        const float minStrength = 0.3f;
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
        if (upgradeDuration)
        {
            modifier.Duration += durationStep;
        }
        else
        {
            modifier.Strength += strengthStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeExplosion(SkillContainerBuilder builder)
    {
        const float minRadius = 1.5f;
        const float minDamageRatio = 0.5f;
        const float radiusStep = 0.3f;
        const float damageRatioStep = 0.1f;

        if (!builder.HasModifier<ExplosionModifier>())
        {
            builder.AddModifier(new ExplosionModifier
            {
                Radius = minRadius,
                DamageRatio = minDamageRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<ExplosionModifier>();
        var upgradeRadius = Random.value < 0.5f;
        if (upgradeRadius)
        {
            modifier.Radius += radiusStep;
        }
        else
        {
            modifier.DamageRatio += damageRatioStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeGravity(SkillContainerBuilder builder)
    {
        const float minRadius = 2f;
        const float minStrength = 0.5f;
        const float radiusStep = 0.5f;
        const float strengthStep = 0.2f;

        if (!builder.HasModifier<GravityModifier>())
        {
            builder.AddModifier(new GravityModifier
            {
                Radius = minRadius,
                Strength = minStrength
            });
            return true;
        }

        var modifier = builder.GetModifier<GravityModifier>();
        var upgradeRadius = Random.value < 0.5f;
        if (upgradeRadius)
        {
            modifier.Radius += radiusStep;
        }
        else
        {
            modifier.Strength += strengthStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeWeaken(SkillContainerBuilder builder)
    {
        const float minDuration = 5f;
        const float durationStep = 1f;
        const float minReduction = 0.2f;
        const float reductionStep = 0.05f;

        if (!builder.HasModifier<WeakenModifier>())
        {
            builder.AddModifier(new WeakenModifier
            {
                Duration = minDuration,
                AttackReduction = minReduction
            });
            return true;
        }

        var modifier = builder.GetModifier<WeakenModifier>();
        if (Random.value < 0.5f)
        {
            modifier.Duration += durationStep;
        }
        else
        {
            modifier.AttackReduction += reductionStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeArmorBreak(SkillContainerBuilder builder)
    {
        const float minDuration = 3f;
        const float durationStep = 0.5f;
        const float minReduction = 0.5f;
        const float reductionStep = 0.05f;

        if (!builder.HasModifier<ArmorBreakModifier>())
        {
            builder.AddModifier(new ArmorBreakModifier
            {
                Duration = minDuration,
                ArmorReduction = minReduction
            });
            return true;
        }

        var modifier = builder.GetModifier<ArmorBreakModifier>();
        if (Random.value < 0.5f)
        {
            modifier.Duration += durationStep;
        }
        else
        {
            modifier.ArmorReduction += reductionStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeHaste(SkillContainerBuilder builder)
    {
        const float minMultiplier = 0.5f;
        const float step = 0.1f;

        if (!builder.HasModifier<HasteModifier>())
        {
            builder.AddModifier(new HasteModifier
            {
                SpeedMultiplier = minMultiplier
            });
            return true;
        }

        var modifier = builder.GetModifier<HasteModifier>();
        modifier.SpeedMultiplier += step;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeEmpower(SkillContainerBuilder builder)
    {
        const float minSetup = 0.2f;
        const float step = 0.05f;

        if (!builder.HasModifier<EmpowerModifier>())
        {
            builder.AddModifier(new EmpowerModifier
            {
                SetupBonus = minSetup,
            });
            return true;
        }

        var modifier = builder.GetModifier<EmpowerModifier>();
        modifier.SetupBonus += step;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeProficiency(SkillContainerBuilder builder)
    {
        const float minCostReduction = 0.08f;
        const float minSalvoIntervalReduction = 0.08f;

        if (!builder.HasModifier<ProficiencyModifier>())
        {
            builder.AddModifier(new ProficiencyModifier
            {
                CostReduction = minCostReduction,
                SalvoIntervalReduction = minSalvoIntervalReduction
            });
            return true;
        }

        var modifier = builder.GetModifier<ProficiencyModifier>();
        if (Random.value < 0.6f)
        {
            modifier.CostReduction += 0.03f;
        }
        else
        {
            modifier.SalvoIntervalReduction += 0.03f;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeVolley(SkillContainerBuilder builder)
    {
        const int minBurstBonus = 2;
        const float minDamageMultiplier = 0.88f;

        VolleyModifier modifier;
        if (!builder.HasModifier<VolleyModifier>())
        {
            modifier = new VolleyModifier
            {
                BurstBonus = minBurstBonus,
                DamageMultiplier = minDamageMultiplier
            };
            builder.AddModifier(modifier);
        }
        else
        {
            modifier = builder.GetModifier<VolleyModifier>();
            modifier.BurstBonus += Random.value < 0.5f ? 1 : 2;
            modifier.DamageMultiplier = Mathf.Max(0.6f, modifier.DamageMultiplier - 0.02f);
            builder.SetModifier(modifier);
        }

        var burstValue = Mathf.Max(1, modifier.BurstBonus + 1);
        if (builder.HasModifier<BurstCount>())
        {
            var burst = builder.GetModifier<BurstCount>();
            burst.Value = Mathf.Max(burst.Value, burstValue);
            builder.SetModifier(burst);
        }
        else
        {
            builder.AddModifier(new BurstCount
            {
                Value = burstValue
            });
        }
        return true;
    }

    private static bool AddOrUpgradeBurn(SkillContainerBuilder builder)
    {
        const float minDuration = 4f;
        const float durationStep = 0.5f;
        const float minDamageRatio = 0.15f;
        const float damageRatioStep = 0.02f;

        if (!builder.HasModifier<BurnModifier>())
        {
            builder.AddModifier(new BurnModifier
            {
                Duration = minDuration,
                DamageRatio = minDamageRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<BurnModifier>();
        var roll = Random.value;
        if (roll < 0.5f)
        {
            modifier.DamageRatio += damageRatioStep;
        }
        else
        {
            modifier.Duration += durationStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeHuge(SkillContainerBuilder builder)
    {
        const float minScale = 1.2f;
        const float scaleStep = 0.1f;

        if (!builder.HasModifier<HugeModifier>())
        {
            builder.AddModifier(new HugeModifier
            {
                Value = minScale,
            });
            return true;
        }

        var modifier = builder.GetModifier<HugeModifier>();
        modifier.Value += scaleStep;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradePoison(SkillContainerBuilder builder)
    {
        const float minDuration = 5f;
        const float durationStep = 1f;
        const float minDamageRatio = 0.1f;
        const float damageRatioStep = 0.02f;
        const int minStacks = 3;

        if (!builder.HasModifier<PoisonModifier>())
        {
            builder.AddModifier(new PoisonModifier
            {
                Duration = minDuration,
                DamageRatio = minDamageRatio,
                MaxStacks = minStacks
            });
            return true;
        }

        var modifier = builder.GetModifier<PoisonModifier>();
        var roll = Random.value;
        if (roll < 0.4f)
        {
            modifier.DamageRatio += damageRatioStep;
        }
        else if (roll < 0.75f)
        {
            modifier.Duration += durationStep;
        }
        else
        {
            modifier.MaxStacks += 1;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeKnockback(SkillContainerBuilder builder)
    {
        const float minDistance = 2f;
        const float distanceStep = 0.5f;
        const float minHeight = 1f;
        const float heightStep = 0.3f;

        if (!builder.HasModifier<KnockbackModifier>())
        {
            builder.AddModifier(new KnockbackModifier
            {
                Distance = minDistance,
                Height = minHeight
            });
            return true;
        }

        var modifier = builder.GetModifier<KnockbackModifier>();
        var roll = Random.value;
        if (roll < 0.5f)
        {
            modifier.Distance += distanceStep;
        }
        else
        {
            modifier.Height += heightStep;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeFreeze(SkillContainerBuilder builder)
    {
        const float minDuration = 2f;
        const float durationStep = 0.5f;

        if (!builder.HasModifier<FreezeModifier>())
        {
            builder.AddModifier(new FreezeModifier
            {
                Duration = minDuration
            });
            return true;
        }

        var modifier = builder.GetModifier<FreezeModifier>();
        modifier.Duration += durationStep;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeDaze(SkillContainerBuilder builder)
    {
        const float minDuration = 0.6f;
        const float durationStep = 0.2f;

        if (!builder.HasModifier<DazeModifier>())
        {
            builder.AddModifier(new DazeModifier
            {
                Duration = minDuration
            });
            return true;
        }

        var modifier = builder.GetModifier<DazeModifier>();
        modifier.Duration += durationStep;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeMercy(SkillContainerBuilder builder)
    {
        const float minDamageMultiplier = 0.7f;
        const float minHealRatio = 0.2f;
        const float healStep = 0.05f;
        const float damageStep = 0.05f;

        if (!builder.HasModifier<MercyModifier>())
        {
            builder.AddModifier(new MercyModifier
            {
                DamageMultiplier = minDamageMultiplier,
                HealRatio = minHealRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<MercyModifier>();
        if (Random.value < 0.65f)
        {
            modifier.HealRatio += healStep;
        }
        else
        {
            modifier.DamageMultiplier = Mathf.Max(0.35f, modifier.DamageMultiplier - damageStep);
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeChaos(SkillContainerBuilder builder)
    {
        const float minDamageVariance = 0.25f;
        const float minAngleVariance = 12f;
        const float minSpeedVariance = 0.25f;

        if (!builder.HasModifier<ChaosModifier>())
        {
            builder.AddModifier(new ChaosModifier
            {
                DamageVariance = minDamageVariance,
                AngleVariance = minAngleVariance,
                SpeedVariance = minSpeedVariance
            });
            return true;
        }

        var modifier = builder.GetModifier<ChaosModifier>();
        var roll = Random.value;
        if (roll < 0.34f)
        {
            modifier.DamageVariance += 0.05f;
        }
        else if (roll < 0.67f)
        {
            modifier.AngleVariance += 3f;
        }
        else
        {
            modifier.SpeedVariance += 0.05f;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeSwap(SkillContainerBuilder builder)
    {
        const float minChance = 0.2f;
        const float chanceStep = 0.05f;

        if (!builder.HasModifier<SwapModifier>())
        {
            builder.AddModifier(new SwapModifier
            {
                Chance = minChance
            });
            return true;
        }

        var modifier = builder.GetModifier<SwapModifier>();
        modifier.Chance += chanceStep;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeRandomAffix(SkillContainerBuilder builder)
    {
        const float minChance = 0.35f;
        const float minPower = 1f;

        if (!builder.HasModifier<RandomAffixModifier>())
        {
            builder.AddModifier(new RandomAffixModifier
            {
                Chance = minChance,
                EffectPower = minPower
            });
            return true;
        }

        var modifier = builder.GetModifier<RandomAffixModifier>();
        if (Random.value < 0.55f)
        {
            modifier.Chance += 0.05f;
        }
        else
        {
            modifier.EffectPower += 0.2f;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeBurnout(SkillContainerBuilder builder)
    {
        const float minDamageRatio = 0.35f;
        const float minBurnDuration = 4f;
        const float minBurnDamageRatio = 0.2f;

        if (!builder.HasModifier<BurnoutModifier>())
        {
            builder.AddModifier(new BurnoutModifier
            {
                DamageRatio = minDamageRatio,
                BurnDuration = minBurnDuration,
                BurnDamageRatio = minBurnDamageRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<BurnoutModifier>();
        var roll = Random.value;
        if (roll < 0.4f)
        {
            modifier.DamageRatio += 0.08f;
        }
        else if (roll < 0.7f)
        {
            modifier.BurnDamageRatio += 0.05f;
        }
        else
        {
            modifier.BurnDuration += 0.75f;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeCombo(SkillContainerBuilder builder)
    {
        const int minSalvoBonus = 2;
        const float minDamageMultiplier = 0.85f;

        ComboModifier modifier;
        if (!builder.HasModifier<ComboModifier>())
        {
            modifier = new ComboModifier
            {
                SalvoBonus = minSalvoBonus,
                DamageMultiplier = minDamageMultiplier
            };
            builder.AddModifier(modifier);
        }
        else
        {
            modifier = builder.GetModifier<ComboModifier>();
            modifier.SalvoBonus += Random.value < 0.5f ? 1 : 2;
            modifier.DamageMultiplier = Mathf.Max(0.55f, modifier.DamageMultiplier - 0.03f);
            builder.SetModifier(modifier);
        }

        var salvoValue = Mathf.Max(1, modifier.SalvoBonus + 1);
        if (builder.HasModifier<SalvoCount>())
        {
            var salvo = builder.GetModifier<SalvoCount>();
            salvo.Value = Mathf.Max(salvo.Value, salvoValue);
            builder.SetModifier(salvo);
        }
        else
        {
            builder.AddModifier(new SalvoCount
            {
                Value = salvoValue
            });
        }
        return true;
    }

    private static bool AddOrUpgradeSilence(SkillContainerBuilder builder)
    {
        const float minDuration = 4f;
        const float minDamageReduction = 0.25f;

        if (!builder.HasModifier<SilenceModifier>())
        {
            builder.AddModifier(new SilenceModifier
            {
                Duration = minDuration,
                DamageReduction = minDamageReduction
            });
            return true;
        }

        var modifier = builder.GetModifier<SilenceModifier>();
        if (Random.value < 0.5f)
        {
            modifier.Duration += 0.75f;
        }
        else
        {
            modifier.DamageReduction += 0.05f;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeDeathSentence(SkillContainerBuilder builder)
    {
        const float minExecuteHealthRatio = 0.12f;
        const float minBonusDamageRatio = 0.5f;

        if (!builder.HasModifier<DeathSentenceModifier>())
        {
            builder.AddModifier(new DeathSentenceModifier
            {
                ExecuteHealthRatio = minExecuteHealthRatio,
                BonusDamageRatio = minBonusDamageRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<DeathSentenceModifier>();
        if (Random.value < 0.45f)
        {
            modifier.ExecuteHealthRatio += 0.03f;
        }
        else
        {
            modifier.BonusDamageRatio += 0.1f;
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeReincarnationTrial(SkillContainerBuilder builder)
    {
        const float minDamageRatio = 0.8f;
        const float minBacklashRatio = 0.15f;
        const float minHealRatio = 0.3f;

        if (!builder.HasModifier<ReincarnationTrialModifier>())
        {
            builder.AddModifier(new ReincarnationTrialModifier
            {
                DamageRatio = minDamageRatio,
                BacklashRatio = minBacklashRatio,
                HealRatio = minHealRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<ReincarnationTrialModifier>();
        var roll = Random.value;
        if (roll < 0.45f)
        {
            modifier.DamageRatio += 0.15f;
        }
        else if (roll < 0.75f)
        {
            modifier.HealRatio += 0.08f;
        }
        else
        {
            modifier.BacklashRatio = Mathf.Max(0.03f, modifier.BacklashRatio - 0.03f);
        }
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeEternalCurse(SkillContainerBuilder builder)
    {
        const float minDuration = 8f;
        const float minDamageRatio = 0.45f;
        const float minDebuffRatio = 0.15f;

        if (!builder.HasModifier<EternalCurseModifier>())
        {
            builder.AddModifier(new EternalCurseModifier
            {
                Duration = minDuration,
                DamageRatio = minDamageRatio,
                DebuffRatio = minDebuffRatio
            });
            return true;
        }

        var modifier = builder.GetModifier<EternalCurseModifier>();
        var roll = Random.value;
        if (roll < 0.35f)
        {
            modifier.Duration += 1.5f;
        }
        else if (roll < 0.7f)
        {
            modifier.DamageRatio += 0.08f;
        }
        else
        {
            modifier.DebuffRatio += 0.04f;
        }
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
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var duration = Mathf.Clamp(slow.Duration, 0f, 999f);
        var strength = Mathf.Clamp(slow.Strength, 0f, 1f);
        if (duration <= 0f || strength <= 0f) return;

        var status = StatusEffects.Slow.NewEntity();
        SetStatusDuration(status, duration);
        ModClass.I.CommandBuffer.AddComponent(status.Id, new StatusStatsMultiplier
        {
            Value = strength
        });
        SetStatusSource(status, context.SourceObj, context.PowerLevel);
        target.a.GetExtend().AddSharedStatus(status);
    }

    // 击中单位时附加冰冻状态
    private static void ApplyFreezeEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out FreezeModifier freeze)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var duration = Mathf.Clamp(freeze.Duration, 0f, 999f);
        if (duration <= 0f) return;

        var status = StatusEffects.Freeze.NewEntity();
        SetStatusDuration(status, duration);
        SetStatusSource(status, context.SourceObj, context.PowerLevel);
        target.a.GetExtend().AddSharedStatus(status);
    }

    // 击中单位时施加短暂硬直
    private static void ApplyDazeEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;
        if (!TryGetModifierContext<DazeModifier>(skillEntity, out var daze, out var context, out _)) return;

        var duration = Mathf.Clamp(daze.Duration, 0f, 10f);
        if (duration <= 0f) return;

        ApplyTimedStatus(target.a.GetExtend(), StatusEffects.Daze, duration, context.SourceObj, context.PowerLevel);
        target.a.makeWait(duration);
    }

    // 构建阶段降低伤害，命中时把部分伤害转为施法者回复
    private static void ApplyMercyOnSetup(Entity skillEntity)
    {
        if (!skillEntity.HasComponent<SkillContext>()) return;
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out MercyModifier mercy)) return;

        ref var context = ref skillEntity.GetComponent<SkillContext>();
        context.Strength *= Mathf.Clamp(mercy.DamageMultiplier, 0.05f, 1f);
    }

    private static void ApplyMercyEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!TryGetModifierContext<MercyModifier>(skillEntity, out var mercy, out var context, out _)) return;
        if (context.SourceObj.isRekt() || !context.SourceObj.isActor()) return;

        var heal = Mathf.RoundToInt(Mathf.Max(0f, context.Strength * Mathf.Clamp(mercy.HealRatio, 0f, 5f)));
        if (heal <= 0) return;

        context.SourceObj.a.restoreHealth(heal);
    }

    // 构建阶段让法术的伤害、初始角度和速度产生随机波动
    private static void ApplyChaosOnSetup(Entity skillEntity)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out ChaosModifier chaos)) return;
        if (!skillEntity.HasComponent<SkillContext>()) return;

        var damageVariance = Mathf.Clamp(chaos.DamageVariance, 0f, 2f);
        var angleVariance = Mathf.Clamp(chaos.AngleVariance, 0f, 90f);
        var speedVariance = Mathf.Clamp(chaos.SpeedVariance, 0f, 2f);

        ref var context = ref skillEntity.GetComponent<SkillContext>();
        context.Strength *= Mathf.Max(0.05f, 1f + Randy.randomFloat(-damageVariance, damageVariance));

        if (skillEntity.HasComponent<Rotation>() && angleVariance > 0f)
        {
            ref var rotation = ref skillEntity.GetComponent<Rotation>();
            var baseDir = rotation.value.sqrMagnitude > 0.0001f ? rotation.value.normalized : context.TargetDir;
            rotation.value = RotateDirection(baseDir, Randy.randomFloat(-angleVariance, angleVariance));
        }

        if (skillEntity.HasComponent<Velocity>() && speedVariance > 0f)
        {
            ref var velocity = ref skillEntity.GetComponent<Velocity>();
            velocity.Value *= Mathf.Max(0.1f, 1f + Randy.randomFloat(-speedVariance, speedVariance));
        }
    }

    // 命中时概率交换施法者和目标位置
    private static void ApplySwapEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;
        if (!TryGetModifierContext<SwapModifier>(skillEntity, out var swap, out var context, out _)) return;
        if (!Randy.randomChance(Mathf.Clamp01(swap.Chance))) return;
        if (context.SourceObj.isRekt() || !context.SourceObj.isActor()) return;

        var sourceActor = context.SourceObj.a;
        var targetActor = target.a;
        if (sourceActor.current_tile == null || targetActor.current_tile == null) return;
        if (sourceActor.current_tile == targetActor.current_tile) return;
        if (sourceActor.position_height > 0f || targetActor.position_height > 0f) return;

        var sourceTile = sourceActor.current_tile;
        var targetTile = targetActor.current_tile;
        ActionLibrary.teleportEffect(sourceActor, targetTile);
        ActionLibrary.teleportEffect(targetActor, sourceTile);
        sourceActor.cancelAllBeh();
        targetActor.cancelAllBeh();
        sourceActor.spawnOn(targetTile, 0f);
        targetActor.spawnOn(sourceTile, 0f);
        sourceActor.makeWait(0.2f);
        targetActor.makeWait(0.2f);
    }

    // 命中时随机触发一个轻量附加效果
    private static void ApplyRandomAffixEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;
        if (!TryGetModifierContext<RandomAffixModifier>(skillEntity, out var randomAffix, out var context,
                out var skill)) return;
        if (!Randy.randomChance(Mathf.Clamp01(randomAffix.Chance))) return;

        var power = Mathf.Clamp(randomAffix.EffectPower, 0.1f, 20f);
        switch (Randy.randomInt(0, 6))
        {
            case 0:
                ApplyTimedStatus(target.a.GetExtend(), StatusEffects.Daze, 0.25f + power * 0.15f,
                    context.SourceObj, context.PowerLevel);
                target.a.makeWait(0.25f + power * 0.15f);
                break;
            case 1:
                ApplyDamageOverTime(target, StatusEffects.Burn, 2f + power, context.Strength * (0.08f + power * 0.03f),
                    ElementComposition.Static.Fire, context.SourceObj, context.PowerLevel);
                break;
            case 2:
                ApplyDamageOverTime(target, StatusEffects.Poison, 3f + power, context.Strength * (0.06f + power * 0.025f),
                    ElementComposition.Static.Poison, context.SourceObj, context.PowerLevel);
                break;
            case 3:
                DealDamage(target, context.Strength * (0.12f + power * 0.04f), skill.Asset.Element, context);
                break;
            case 4:
                ApplyStatDebuff(target, StatusEffects.ArmorBreak, 2f + power * 0.4f, 0f,
                    Mathf.Clamp01(0.08f + power * 0.03f), context.SourceObj, context.PowerLevel);
                break;
            case 5:
                ApplyKnockbackForce(skillEntity, target, context, 1f + power * 0.4f, 0.4f + power * 0.2f);
                break;
        }
    }

    // 命中时追加火焰爆发和燃烧
    private static void ApplyBurnoutEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!TryGetModifierContext<BurnoutModifier>(skillEntity, out var burnout, out var context, out _)) return;

        var damageRatio = Mathf.Clamp(burnout.DamageRatio, 0f, 10f);
        if (damageRatio > 0f)
        {
            DealDamage(target, context.Strength * damageRatio, ElementComposition.Static.Fire, context);
        }

        var duration = Mathf.Clamp(burnout.BurnDuration, 0f, 999f);
        var burnRatio = Mathf.Clamp(burnout.BurnDamageRatio, 0f, 10f);
        if (duration <= 0f || burnRatio <= 0f) return;

        ApplyDamageOverTime(target, StatusEffects.Burn, duration, context.Strength * burnRatio,
            ElementComposition.Static.Fire, context.SourceObj, context.PowerLevel);
    }

    // 构建阶段按合击词条降低单发伤害，避免连射词条直接线性放大总伤害
    private static void ApplyComboOnSetup(Entity skillEntity)
    {
        if (!skillEntity.HasComponent<SkillContext>()) return;
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out ComboModifier combo)) return;

        ref var context = ref skillEntity.GetComponent<SkillContext>();
        context.Strength *= Mathf.Clamp(combo.DamageMultiplier, 0.05f, 2f);
    }

    // 命中时施加封印，降低目标攻击
    private static void ApplySilenceEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!TryGetModifierContext<SilenceModifier>(skillEntity, out var silence, out var context, out _)) return;

        var duration = Mathf.Clamp(silence.Duration, 0f, 999f);
        var reduction = Mathf.Clamp01(silence.DamageReduction);
        if (duration <= 0f || reduction <= 0f) return;

        ApplyStatDebuff(target, StatusEffects.Silence, duration, reduction, 0f, context.SourceObj, context.PowerLevel);
    }

    // 命中时执行低血目标，否则追加终焉伤害并挂标记
    private static void ApplyDeathSentenceEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;
        if (!TryGetModifierContext<DeathSentenceModifier>(skillEntity, out var sentence, out var context, out _))
            return;

        var duration = 3f;
        ApplyTimedStatus(target.a.GetExtend(), StatusEffects.DeathSentence, duration, context.SourceObj,
            context.PowerLevel);

        var executeRatio = Mathf.Clamp01(sentence.ExecuteHealthRatio);
        var bonusRatio = Mathf.Clamp(sentence.BonusDamageRatio, 0f, 20f);
        if (target.a.getHealthRatio() <= executeRatio)
        {
            DealDamage(target, target.a.getHealth() + Mathf.Max(1f, target.a.getMaxHealth() * 0.02f),
                new ElementComposition(entropy: 1f, normalize: true), context, ignoreDamageReduction: true);
            return;
        }

        if (bonusRatio > 0f)
        {
            DealDamage(target, context.Strength * bonusRatio, new ElementComposition(entropy: 1f, normalize: true),
                context);
        }
    }

    // 命中时追加轮回试练伤害；若目标看起来会被击杀，则治疗施法者，否则施法者承受反噬
    private static void ApplyReincarnationTrialEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!TryGetModifierContext<ReincarnationTrialModifier>(skillEntity, out var trial, out var context, out _))
            return;

        var element = new ElementComposition(pos: 0.35f, neg: 0.35f, entropy: 0.3f, normalize: true);
        var trialDamage = Mathf.Max(0f, context.Strength * Mathf.Clamp(trial.DamageRatio, 0f, 20f));
        if (trialDamage <= 0f) return;

        var likelyKills = target.isActor() && target.a.getHealth() <= trialDamage;
        DealDamage(target, trialDamage, element, context);

        if (context.SourceObj.isRekt() || !context.SourceObj.isActor()) return;

        if (likelyKills)
        {
            var heal = Mathf.RoundToInt(trialDamage * Mathf.Clamp(trial.HealRatio, 0f, 10f));
            if (heal > 0) context.SourceObj.a.restoreHealth(heal);
            return;
        }

        var backlash = trialDamage * Mathf.Clamp(trial.BacklashRatio, 0f, 10f);
        if (backlash <= 0f) return;

        var backlashContext = context;
        backlashContext.SourceObj = target;
        backlashContext.TargetDir = -context.TargetDir;
        ModClass.I.SkillV3.Vfx.QueueElementImpact(backlashContext, element, backlash, context.SourceObj);
        EventSystemHub.Publish(new GetHitEvent
        {
            TargetID = context.SourceObj.a.data.id,
            Damage = backlash,
            Element = element,
            Attacker = target,
            AttackerPowerLevel = context.PowerLevel
        });
    }

    // 命中时施加长时诅咒，持续伤害并降低攻防
    private static void ApplyEternalCurseEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;
        if (!TryGetModifierContext<EternalCurseModifier>(skillEntity, out var curse, out var context, out _)) return;

        var duration = Mathf.Clamp(curse.Duration, 0f, 999f);
        if (duration <= 0f) return;

        var actorExtend = target.a.GetExtend();
        var status = GetOrCreateStatus(actorExtend, StatusEffects.EternalCurse, context.SourceObj, context.PowerLevel,
            out var isNewStatus);
        SetStatusDuration(status, duration);
        if (isNewStatus && !actorExtend.AddSharedStatus(status)) return;

        var totalDamage = context.Strength * Mathf.Clamp(curse.DamageRatio, 0f, 20f);
        if (totalDamage > 0f)
        {
            ApplyDamageTickState(ref status, totalDamage / duration,
                new ElementComposition(neg: 0.5f, entropy: 0.5f, normalize: true), context.SourceObj,
                context.PowerLevel);
        }

        var debuffRatio = Mathf.Clamp01(curse.DebuffRatio);
        if (debuffRatio <= 0f) return;

        var stats = PrepareOverwriteStats(status);
        stats[S.damage] = -Mathf.Max(0f, target.stats[S.damage]) * debuffRatio;
        stats[S.armor] = -Mathf.Max(0f, target.stats[S.armor]) * debuffRatio;
    }

    // 击中单位时触发爆炸
    private static void ApplyExplosionEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out ExplosionModifier explosion)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;
        if (!skillEntity.TryGetComponent(out Position skillPos)) return;

        var radius = Mathf.Clamp(explosion.Radius, 0.5f, 10f);
        var damageRatio = Mathf.Clamp(explosion.DamageRatio, 0f, 2f);
        if (radius <= 0f || damageRatio <= 0f) return;

        // 获取爆炸中心位置（目标位置）
        var explosionPos = target.GetSimPos();
        
        // 获取技能元素和攻击者
        var attacker = context.SourceObj;
        ref var element = ref skill.Asset.Element;
        var damage = context.Strength * damageRatio;

        // 生成爆炸动画
        ModClass.I.SkillV3.Vfx.QueueExplosion(explosionPos, element, context.PowerLevel, damage, radius, target);

        // 对范围内的敌人造成伤害
        foreach (var obj in SkillUtils.IterEnemyInSphere(explosionPos, radius, attacker, context.AttackKingdom))
        {
            DealDamage(obj, damage, element, context);
        }
    }

    // 构建阶段提升伤害
    private static void ApplyEmpowerSetup(Entity skillEntity)
    {
        if (!skillEntity.HasComponent<SkillContext>()) return;
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out EmpowerModifier empower)) return;

        var bonus = Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (bonus <= 0f) return;
        ref var context = ref skillEntity.GetComponent<SkillContext>();
        context.Strength *= 1f + bonus;
    }

    // 齐射会提高分散施法倾向，因此压低单发伤害避免总伤害线性膨胀
    private static void ApplyVolleyOnSetup(Entity skillEntity)
    {
        if (!skillEntity.HasComponent<SkillContext>()) return;
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out VolleyModifier volley)) return;

        ref var context = ref skillEntity.GetComponent<SkillContext>();
        context.Strength *= Mathf.Clamp(volley.DamageMultiplier, 0.05f, 2f);
    }

    // 放大贴图和碰撞体
    private static void ApplyHugeOnSetup(Entity skillEntity)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out HugeModifier huge)) return;

        var scaleMul = Mathf.Clamp(huge.Value, 0.1f, 10f);

        if (skillEntity.HasComponent<Scale>())
        {
            ref var scale = ref skillEntity.GetComponent<Scale>();
            scale.value *= scaleMul;
        }
        else
        {
            ModClass.I.CommandBuffer.AddComponent(skillEntity.Id, new Scale(scaleMul));
        }

        if (skillEntity.HasComponent<ColliderSphere>())
        {
            ref var collider = ref skillEntity.GetComponent<ColliderSphere>();
            collider.Radius *= scaleMul;
        }
    }

    // 提升弹道速度
    private static void ApplyHasteOnSetup(Entity skillEntity)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out HasteModifier haste)) return;

        var multiplier = Mathf.Clamp(1f + haste.SpeedMultiplier, 0.1f, 10f);
        if (multiplier <= 0f) return;

        if (skillEntity.HasComponent<Velocity>())
        {
            ref var velocity = ref skillEntity.GetComponent<Velocity>();
            velocity.Value *= multiplier;
        }
    }

    // 击中单位时附加灼烧
    private static void ApplyBurnEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out BurnModifier burn)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var duration = Mathf.Clamp(burn.Duration, 0f, 999f);
        var damageRatio = Mathf.Clamp(burn.DamageRatio, 0f, 1f);
        if (duration <= 0f || damageRatio <= 0f) return;

        var totalDamage = Mathf.Max(0f, context.Strength * damageRatio);
        if (totalDamage <= 0f) return;
        var element = ElementComposition.Static.Fire;
        var dps = duration > 0f ? totalDamage / duration : 0f;
        if (dps <= 0f) return;

        var actorExtend = target.a.GetExtend();
        Entity burnStatus = default;
        foreach (var statusEntity in actorExtend.GetStatuses())
        {
            if (statusEntity.IsNull || !statusEntity.TryGetComponent(out StatusComponent statusComponent)) continue;
            if (statusComponent.Type == StatusEffects.Burn)
            {
                burnStatus = statusEntity;
                break;
            }
        }

        if (burnStatus.IsNull)
        {
            burnStatus = StatusEffects.Burn.NewEntity();
            SetStatusDuration(burnStatus, duration);
            SetStatusSource(burnStatus, context.SourceObj, context.PowerLevel);
            ApplyDamageTickState(ref burnStatus, dps, element, context.SourceObj, context.PowerLevel);
            actorExtend.AddSharedStatus(burnStatus);
        }
        else
        {
            SetStatusDuration(burnStatus, duration);
            SetStatusSource(burnStatus, context.SourceObj, context.PowerLevel);
            ApplyDamageTickState(ref burnStatus, dps, element, context.SourceObj, context.PowerLevel);
        }
    }

    // 击中单位时附加中毒效果
    private static void ApplyPoisonEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out PoisonModifier poison)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var duration = Mathf.Clamp(poison.Duration, 0f, 999f);
        var damageRatio = Mathf.Clamp(poison.DamageRatio, 0f, 1f);
        var maxStacks = Mathf.Clamp(poison.MaxStacks, 1, 99);
        if (duration <= 0f || damageRatio <= 0f) return;

        var actorExtend = target.a.GetExtend();
        List<Entity> poisonStatuses = new();
        foreach (var statusEntity in actorExtend.GetStatuses())
        {
            if (statusEntity.IsNull || !statusEntity.TryGetComponent(out StatusComponent statusComponent)) continue;
            if (statusComponent.Type == StatusEffects.Poison)
            {
                if (statusComponent.Source != context.SourceObj) continue;

                poisonStatuses.Add(statusEntity);
            }
        }

        var totalDamage = Mathf.Max(0f, context.Strength * damageRatio);
        if (totalDamage <= 0f) return;
        var element = ElementComposition.Static.Poison;
        var damagePerSecond = duration > 0f ? totalDamage / duration : 0f;
        if (damagePerSecond <= 0f) return;
        if (poisonStatuses.Count >= maxStacks)
        {
            RefreshExistingPoison(duration, damagePerSecond, element, context.SourceObj, context.PowerLevel,
                poisonStatuses);
            return;
        }

        var status = StatusEffects.Poison.NewEntity();
        SetStatusDuration(status, duration);
        SetStatusSource(status, context.SourceObj, context.PowerLevel);
        ApplyDamageTickState(ref status, damagePerSecond, element, context.SourceObj, context.PowerLevel);
        actorExtend.AddSharedStatus(status);
    }

    private static void ApplyDamageTickState(ref Entity status, float dps, ElementComposition element,
        BaseSimObject source, float? sourcePowerLevel)
    {
        ref var tickState = ref status.GetComponent<StatusTickState>();
        tickState.Value = dps;
        tickState.Element = element;
        SetStatusSource(status, source, sourcePowerLevel);
    }

    private static void RefreshExistingPoison(float duration, float dps, ElementComposition element,
        BaseSimObject source, float? sourcePowerLevel, List<Entity> poisonStatuses)
    {
        Entity targetStatus = default;
        float minTimer = float.MaxValue;
        foreach (var status in poisonStatuses)
        {
            if (status.IsNull || !status.TryGetComponent(out AliveTimer timer)) continue;
            if (timer.value < minTimer)
            {
                minTimer = timer.value;
                targetStatus = status;
            }
        }

        if (targetStatus.IsNull)
        {
            return;
        }

        SetStatusDuration(targetStatus, duration);
        ApplyDamageTickState(ref targetStatus, dps, element, source, sourcePowerLevel);
    }

    // 击中单位时施加击飞效果
    private static void ApplyKnockbackEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out KnockbackModifier knockback)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var distance = Mathf.Clamp(knockback.Distance, 0f, 10f);
        var height = Mathf.Clamp(knockback.Height, 0f, 5f);
        if (distance <= 0f && height <= 0f) return;

        ApplyKnockbackForce(skillEntity, target, context, distance, height);
    }

    private static void ApplyKnockbackForce(Entity skillEntity, BaseSimObject target, SkillContext context,
        float distance, float height)
    {
        if (!target.isActor()) return;

        var actor = target.a;
        if (!actor.asset.can_be_moved_by_powers) return;
        if (actor.position_height > 0f) return;
        if (!skillEntity.TryGetComponent(out Position skillPos)) return;

        var targetPos = target.GetSimPos();
        var direction = (targetPos - skillPos.value).normalized;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = context.TargetDir;
        }
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector3.forward;
        }

        var mass = target.stats["mass"];
        var a = mass * SimGlobals.m.gravity;
        var tm = Mathf.Sqrt(height * 2 / (a * 0.3f));
        var vz = a + 0.3f * a * tm;
        var te = 2 * tm;
        var vx = direction.x * distance / te;
        var vy = direction.y * distance / te;

        actor.GetExtend().GetForce(context.SourceObj, vx, vy, vz);
        return;
    }

    // 击中单位时施加衰弱效果，降低攻防
    private static void ApplyWeakenEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out WeakenModifier weaken)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var duration = Mathf.Clamp(weaken.Duration, 0f, 999f);
        if (duration <= 0f) return;

        var attackReduction = Mathf.Clamp01(weaken.AttackReduction);

        var attackDelta = attackReduction > 0f ? Mathf.Max(0f, target.stats[S.damage]) * attackReduction : 0f;
        if (attackDelta <= 0f) return;

        var actorExtend = target.a.GetExtend();
        var status = GetOrCreateStatus(actorExtend, StatusEffects.Weaken, context.SourceObj, context.PowerLevel,
            out var isNewStatus);
        SetStatusDuration(status, duration);
        if (isNewStatus && !actorExtend.AddSharedStatus(status)) return;

        var stats = PrepareOverwriteStats(status);
        if (attackDelta > 0f)
        {
            stats[S.damage] = -attackDelta;
        }
    }

    // 击中单位时施加破甲效果，降低护甲
    private static void ApplyArmorBreakEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out ArmorBreakModifier armorBreak)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;

        var duration = Mathf.Clamp(armorBreak.Duration, 0f, 999f);
        if (duration <= 0f) return;

        var reductionRatio = Mathf.Clamp01(armorBreak.ArmorReduction);
        var armorDelta = reductionRatio > 0f ? Mathf.Max(0f, target.stats[S.armor]) * reductionRatio : 0f;
        if (armorDelta <= 0f) return;

        var actorExtend = target.a.GetExtend();
        var status = GetOrCreateStatus(actorExtend, StatusEffects.ArmorBreak, context.SourceObj, context.PowerLevel,
            out var isNewStatus);
        SetStatusDuration(status, duration);
        if (isNewStatus && !actorExtend.AddSharedStatus(status)) return;

        var stats = PrepareOverwriteStats(status);
        stats[S.armor] = -armorDelta;
    }

    // 技能移动时施加引力效果
    private static void ApplyGravityTravel(Entity skillEntity)
    {
        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out GravityModifier gravity)) return;

        var radius = Mathf.Clamp(gravity.Radius, 0.5f, 10f);
        var strength = Mathf.Clamp(gravity.Strength, 0f, 10f);
        if (radius <= 0f || strength <= 0f) return;

        var data = skillEntity.Data;
        ref var skillPos = ref data.Get<Position>();
        ref var context = ref data.Get<SkillContext>();
        var attacker = context.SourceObj;

        // 对范围内的敌人施加引力
        foreach (var obj in SkillUtils.IterEnemyInSphere(skillPos.v2, radius, attacker, context.AttackKingdom))
        {
            if (!obj.isActor()) continue;

            var actor = obj.a;
            if (!actor.asset.can_be_moved_by_powers) continue;
            if (actor.position_height > 0f) continue;

            // 计算从敌人到技能实体的方向
            var targetPos = obj.GetSimPos();
            var direction = (skillPos.value - targetPos).normalized;
            if (direction.sqrMagnitude < 0.01f) continue;

            // 计算距离，距离越近引力越强
            var distance = Vector3.Distance(skillPos.value, targetPos);
            var distanceFactor = Mathf.Clamp01(1f - distance / radius);
            var forceStrength = strength * distanceFactor;

            // 施加引力（只施加水平方向的力，不施加垂直力）
            var vx = direction.x * forceStrength;
            var vy = direction.y * forceStrength;
            var vz = forceStrength;

            actor.GetExtend().GetForce(attacker, vx, vy, vz);
        }
    }

    private static bool TryGetModifierContext<TModifier>(Entity skillEntity, out TModifier modifier,
        out SkillContext context, out SkillEntity skill)
        where TModifier : struct, IModifier
    {
        modifier = default;
        context = default;
        skill = default;

        if (!skillEntity.TryGetComponent(out skill)) return false;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out modifier)) return false;
        return skillEntity.TryGetComponent(out context);
    }

    private static Vector3 RotateDirection(Vector3 direction, float angleDegrees)
    {
        if (direction.sqrMagnitude < 0.0001f) return Vector3.right;

        var rotated = Quaternion.AngleAxis(angleDegrees, Vector3.forward) * direction.normalized;
        return rotated.sqrMagnitude < 0.0001f ? direction.normalized : rotated.normalized;
    }

    private static void DealDamage(BaseSimObject target, float damage, ElementComposition element,
        SkillContext context, bool ignoreDamageReduction = false)
    {
        if (target.isRekt() || damage <= 0f) return;

        ModClass.I.SkillV3.Vfx.QueueElementImpact(context, element, damage, target);
        if (target.isActor())
        {
            EventSystemHub.Publish(new GetHitEvent
            {
                TargetID = target.a.data.id,
                Damage = damage,
                Element = element,
                Attacker = context.SourceObj,
                AttackerPowerLevel = context.PowerLevel,
                IgnoreDamageReduction = ignoreDamageReduction
            });
        }
        else
        {
            target.b.getHit(damage, pAttacker: context.SourceObj);
        }
    }

    private static bool ApplyTimedStatus(ActorExtend actorExtend, StatusEffectAsset effect, float duration,
        BaseSimObject source, float? sourcePowerLevel)
    {
        if (actorExtend == null || duration <= 0f) return false;

        var status = GetOrCreateStatus(actorExtend, effect, source, sourcePowerLevel, out var isNewStatus);
        SetStatusDuration(status, duration);
        return !isNewStatus || actorExtend.AddSharedStatus(status);
    }

    private static void ApplyDamageOverTime(BaseSimObject target, StatusEffectAsset effect, float duration,
        float totalDamage, ElementComposition element, BaseSimObject source, float? sourcePowerLevel)
    {
        if (!target.isActor()) return;
        if (duration <= 0f || totalDamage <= 0f) return;

        var actorExtend = target.a.GetExtend();
        var status = GetOrCreateStatus(actorExtend, effect, source, sourcePowerLevel, out var isNewStatus);
        SetStatusDuration(status, duration);
        if (isNewStatus && !actorExtend.AddSharedStatus(status)) return;

        ApplyDamageTickState(ref status, totalDamage / duration, element, source, sourcePowerLevel);
    }

    private static void ApplyStatDebuff(BaseSimObject target, StatusEffectAsset effect, float duration,
        float attackReduction, float armorReduction, BaseSimObject source, float? sourcePowerLevel)
    {
        if (!target.isActor()) return;
        if (duration <= 0f) return;

        var actorExtend = target.a.GetExtend();
        var status = GetOrCreateStatus(actorExtend, effect, source, sourcePowerLevel, out var isNewStatus);
        SetStatusDuration(status, duration);
        if (isNewStatus && !actorExtend.AddSharedStatus(status)) return;

        var stats = PrepareOverwriteStats(status);
        if (attackReduction > 0f)
        {
            stats[S.damage] = -Mathf.Max(0f, target.stats[S.damage]) * Mathf.Clamp01(attackReduction);
        }
        if (armorReduction > 0f)
        {
            stats[S.armor] = -Mathf.Max(0f, target.stats[S.armor]) * Mathf.Clamp01(armorReduction);
        }
    }

    private static void SetStatusDuration(Entity status, float duration)
    {
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        if (!status.HasComponent<AliveTimer>()) return;

        ref var timer = ref status.GetComponent<AliveTimer>();
        timer.value = 0f;
    }

    private static Entity GetOrCreateStatus(ActorExtend actorExtend, StatusEffectAsset effect,
        BaseSimObject source, float? sourcePowerLevel, out bool isNewStatus)
    {
        foreach (var status in actorExtend.GetStatuses())
        {
            if (status.IsNull || !status.TryGetComponent(out StatusComponent statusComponent)) continue;
            if (statusComponent.Type == effect)
            {
                SetStatusSource(status, source, sourcePowerLevel);
                isNewStatus = false;
                return status;
            }
        }

        var newStatus = effect.NewEntity();
        SetStatusSource(newStatus, source, sourcePowerLevel);
        isNewStatus = true;
        return newStatus;
    }

    private static void SetStatusSource(Entity status, BaseSimObject source, float? sourcePowerLevel)
    {
        ref var statusComp = ref status.GetComponent<StatusComponent>();
        statusComp.Source = source;
        statusComp.SourcePowerLevel = sourcePowerLevel;
    }

    private static BaseStats PrepareOverwriteStats(Entity status)
    {
        BaseStats stats;
        if (!status.HasComponent<StatusOverwriteStats>())
        {
            stats = new BaseStats();
            ModClass.I.CommandBuffer.AddComponent(status.Id, new StatusOverwriteStats
            {
                stats = stats
            });
        }
        else
        {
            ref var overwrite = ref status.GetComponent<StatusOverwriteStats>();
            stats = overwrite.stats;
            if (stats == null)
            {
                stats = new BaseStats();
                overwrite.stats = stats;
            }
            stats.clear();
        }
        return stats;
    }
}
