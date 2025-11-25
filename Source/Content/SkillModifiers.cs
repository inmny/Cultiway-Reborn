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

    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        Setup<PlaceholderModifier>(Placeholder, SkillModifierRarity.Common);

        Setup<SlowModifier>(Slow, SkillModifierRarity.Common);
        Slow.OnAddOrUpgrade = AddOrUpgradeSlow;
        Slow.OnEffectObj = ApplySlowEffect;
        Setup<BurnModifier>(Burn, SkillModifierRarity.Common);
        Burn.OnAddOrUpgrade = AddOrUpgradeBurn;
        Burn.OnEffectObj = ApplyBurnEffect;
        Setup<FreezeModifier>(Freeze, SkillModifierRarity.Common);
        Setup<PoisonModifier>(Poison, SkillModifierRarity.Common);
        Poison.OnAddOrUpgrade = AddOrUpgradePoison;
        Poison.OnEffectObj = ApplyPoisonEffect;
        Setup<ExplosionModifier>(Explosion, SkillModifierRarity.Common);
        Setup<HasteModifier>(Haste, SkillModifierRarity.Common);
        Setup<ProficiencyModifier>(Proficiency, SkillModifierRarity.Common);
        Setup<EmpowerModifier>(Empower, SkillModifierRarity.Common);
        Empower.OnAddOrUpgrade = AddOrUpgradeEmpower;
        Empower.OnSetup = ApplyEmpowerSetup;
        Setup<KnockbackModifier>(Knockback, SkillModifierRarity.Common);

        Setup<LockOnModifier>(LockOn, SkillModifierRarity.Rare);
        Setup<HugeModifier>(Huge, SkillModifierRarity.Rare);
        Huge.OnAddOrUpgrade = AddOrUpgradeHuge;
        Huge.OnSetup = ApplyHugeOnSetup;
        Setup<WeakenModifier>(Weaken, SkillModifierRarity.Rare);
        Setup<ArmorBreakModifier>(ArmorBreak, SkillModifierRarity.Rare);
        Setup<GravityModifier>(Gravity, SkillModifierRarity.Rare);
        Setup<DazeModifier>(Daze, SkillModifierRarity.Rare);

        Setup<MercyModifier>(Mercy, SkillModifierRarity.Epic, KillOverrideTag);
        Mercy.IsDisabled = true;
        Setup<ChaosModifier>(Chaos, SkillModifierRarity.Epic);
        Chaos.IsDisabled = true;
        Setup<SwapModifier>(Swap, SkillModifierRarity.Epic);
        Swap.IsDisabled = true;
        Setup<RandomAffixModifier>(RandomAffix, SkillModifierRarity.Epic);
        RandomAffix.IsDisabled = true;
        Setup<BurnoutModifier>(Burnout, SkillModifierRarity.Epic);
        Burnout.IsDisabled = true;
        Setup<ComboModifier>(Combo, SkillModifierRarity.Epic);
        Combo.IsDisabled = true;

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

    private static bool AddOrUpgradeEmpower(SkillContainerBuilder builder)
    {
        const float minSetup = 0.2f;
        const float maxSetup = 0.4f;
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
        var changed = false;
        var roll = Random.value;
        if (roll < 0.5f && modifier.SetupBonus < maxSetup)
        {
            modifier.SetupBonus = Mathf.Min(maxSetup, modifier.SetupBonus + step);
            changed = true;
        }
        else if (modifier.SetupBonus < maxSetup)
        {
            modifier.SetupBonus = Mathf.Min(maxSetup, modifier.SetupBonus + step);
            changed = true;
        }

        if (!changed) return false;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeBurn(SkillContainerBuilder builder)
    {
        const float minDuration = 4f;
        const float maxDuration = 8f;
        const float durationStep = 0.5f;
        const float minDamageRatio = 0.15f;
        const float maxDamageRatio = 0.25f;
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
        var changed = false;
        var roll = Random.value;
        if (roll < 0.5f && modifier.DamageRatio < maxDamageRatio)
        {
            modifier.DamageRatio = Mathf.Min(maxDamageRatio, modifier.DamageRatio + damageRatioStep);
            changed = true;
        }
        else if (modifier.Duration < maxDuration)
        {
            modifier.Duration = Mathf.Min(maxDuration, modifier.Duration + durationStep);
            changed = true;
        }
        else if (modifier.DamageRatio < maxDamageRatio)
        {
            modifier.DamageRatio = Mathf.Min(maxDamageRatio, modifier.DamageRatio + damageRatioStep);
            changed = true;
        }

        if (!changed) return false;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradeHuge(SkillContainerBuilder builder)
    {
        const float minScale = 1.2f;
        const float maxScale = 1.8f;
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
        var changed = false;
        var roll = Random.value;
        if (roll < 0.5f && modifier.Value < maxScale)
        {
            modifier.Value = Mathf.Min(maxScale, modifier.Value + scaleStep);
            changed = true;
        }
        else if (modifier.Value < maxScale)
        {
            modifier.Value = Mathf.Min(maxScale, modifier.Value + scaleStep);
            changed = true;
        }

        if (!changed) return false;
        builder.SetModifier(modifier);
        return true;
    }

    private static bool AddOrUpgradePoison(SkillContainerBuilder builder)
    {
        const float minDuration = 5f;
        const float maxDuration = 10f;
        const float durationStep = 1f;
        const float minDamageRatio = 0.1f;
        const float maxDamageRatio = 0.2f;
        const float damageRatioStep = 0.02f;
        const int minStacks = 3;
        const int maxStacks = 5;

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
        var changed = false;
        var roll = Random.value;
        if (roll < 0.4f && modifier.DamageRatio < maxDamageRatio)
        {
            modifier.DamageRatio = Mathf.Min(maxDamageRatio, modifier.DamageRatio + damageRatioStep);
            changed = true;
        }
        else if (roll < 0.75f && modifier.Duration < maxDuration)
        {
            modifier.Duration = Mathf.Min(maxDuration, modifier.Duration + durationStep);
            changed = true;
        }
        else if (modifier.MaxStacks < maxStacks)
        {
            modifier.MaxStacks = Mathf.Min(maxStacks, modifier.MaxStacks + 1);
            changed = true;
        }
        else if (modifier.DamageRatio < maxDamageRatio)
        {
            modifier.DamageRatio = Mathf.Min(maxDamageRatio, modifier.DamageRatio + damageRatioStep);
            changed = true;
        }
        else if (modifier.Duration < maxDuration)
        {
            modifier.Duration = Mathf.Min(maxDuration, modifier.Duration + durationStep);
            changed = true;
        }
        else if (modifier.MaxStacks < maxStacks)
        {
            modifier.MaxStacks = Mathf.Min(maxStacks, modifier.MaxStacks + 1);
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
            skillEntity.AddComponent(new Scale(scaleMul));
        }

        if (skillEntity.HasComponent<ColliderSphere>())
        {
            ref var collider = ref skillEntity.GetComponent<ColliderSphere>();
            collider.Radius *= scaleMul;
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
            actorExtend.AddSharedStatus(burnStatus);
        }

        ref var timeLimit = ref burnStatus.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        ApplyDamageTickState(ref burnStatus, dps, element, context.SourceObj);
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
                ref var tickState = ref statusEntity.GetComponent<StatusTickState>();
                if (tickState.Source != context.SourceObj) continue;

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
            RefreshExistingPoison(duration, damagePerSecond, element, context.SourceObj, poisonStatuses);
            return;
        }

        var status = StatusEffects.Poison.NewEntity();
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        ApplyDamageTickState(ref status, damagePerSecond, element, context.SourceObj);
        actorExtend.AddSharedStatus(status);
    }

    private static void ApplyDamageTickState(ref Entity status, float dps, ElementComposition element, BaseSimObject source)
    {
        ref var tickState = ref status.GetComponent<StatusTickState>();
        tickState.Value = dps;
        tickState.Element = element;
        tickState.Source = source;
    }

    private static void RefreshExistingPoison(float duration, float dps, ElementComposition element, BaseSimObject source, List<Entity> poisonStatuses)
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

        ref var aliveTimer = ref targetStatus.GetComponent<AliveTimer>();
        aliveTimer.value = 0f;
        ref var aliveLimit = ref targetStatus.GetComponent<AliveTimeLimit>();
        aliveLimit.value = duration;
        ApplyDamageTickState(ref targetStatus, dps, element, source);
    }
}
