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
        Slow.AddSimilarityTags("control", "slow");
        Slow.OnAddOrUpgrade = AddOrUpgradeSlow;
        Slow.OnEffectObj = ApplySlowEffect;
        Setup<BurnModifier>(Burn, SkillModifierRarity.Common);
        Burn.AddSimilarityTags("dot", "burn", "fire");
        Burn.OnAddOrUpgrade = AddOrUpgradeBurn;
        Burn.OnEffectObj = ApplyBurnEffect;
        Setup<FreezeModifier>(Freeze, SkillModifierRarity.Common);
        Freeze.AddSimilarityTags("control", "freeze");
        Freeze.OnAddOrUpgrade = AddOrUpgradeFreeze;
        Freeze.OnEffectObj = ApplyFreezeEffect;
        Setup<PoisonModifier>(Poison, SkillModifierRarity.Common);
        Poison.AddSimilarityTags("dot", "poison");
        Poison.OnAddOrUpgrade = AddOrUpgradePoison;
        Poison.OnEffectObj = ApplyPoisonEffect;
        Setup<ExplosionModifier>(Explosion, SkillModifierRarity.Common);
        Explosion.AddSimilarityTags("aoe", "blast");
        Explosion.OnAddOrUpgrade = AddOrUpgradeExplosion;
        Explosion.OnEffectObj = ApplyExplosionEffect;
        Setup<HasteModifier>(Haste, SkillModifierRarity.Common);
        Haste.AddSimilarityTags("speed", "projectile");
        Haste.OnAddOrUpgrade = AddOrUpgradeHaste;
        Haste.OnSetup = ApplyHasteOnSetup;
        Setup<ProficiencyModifier>(Proficiency, SkillModifierRarity.Common);
        Proficiency.AddSimilarityTags("growth");
        Setup<EmpowerModifier>(Empower, SkillModifierRarity.Common);
        Empower.AddSimilarityTags("power", "damage");
        Empower.OnAddOrUpgrade = AddOrUpgradeEmpower;
        Empower.OnSetup = ApplyEmpowerSetup;
        Setup<KnockbackModifier>(Knockback, SkillModifierRarity.Common);
        Knockback.AddSimilarityTags("control", "displace");
        Knockback.OnAddOrUpgrade = AddOrUpgradeKnockback;
        Knockback.OnEffectObj = ApplyKnockbackEffect;

        Setup<HugeModifier>(Huge, SkillModifierRarity.Rare);
        Huge.AddSimilarityTags("size", "aoe");
        Huge.OnAddOrUpgrade = AddOrUpgradeHuge;
        Huge.OnSetup = ApplyHugeOnSetup;
        Setup<WeakenModifier>(Weaken, SkillModifierRarity.Rare);
        Weaken.AddSimilarityTags("debuff", "attack_down");
        Weaken.OnAddOrUpgrade = AddOrUpgradeWeaken;
        Weaken.OnEffectObj = ApplyWeakenEffect;
        Setup<ArmorBreakModifier>(ArmorBreak, SkillModifierRarity.Rare);
        ArmorBreak.AddSimilarityTags("debuff", "armor_down");
        ArmorBreak.OnAddOrUpgrade = AddOrUpgradeArmorBreak;
        ArmorBreak.OnEffectObj = ApplyArmorBreakEffect;
        Setup<GravityModifier>(Gravity, SkillModifierRarity.Rare);
        Gravity.AddSimilarityTags("control", "pull", "aoe");
        Gravity.OnAddOrUpgrade = AddOrUpgradeGravity;
        Gravity.OnTravel = ApplyGravityTravel;
        Setup<DazeModifier>(Daze, SkillModifierRarity.Rare);
        Daze.AddSimilarityTags("control", "stun");

        Setup<MercyModifier>(Mercy, SkillModifierRarity.Epic, KillOverrideTag);
        Mercy.AddSimilarityTags("special");
        Mercy.IsDisabled = true;
        Setup<ChaosModifier>(Chaos, SkillModifierRarity.Epic);
        Chaos.AddSimilarityTags("special", "random");
        Chaos.IsDisabled = true;
        Setup<SwapModifier>(Swap, SkillModifierRarity.Epic);
        Swap.AddSimilarityTags("control", "swap");
        Swap.IsDisabled = true;
        Setup<RandomAffixModifier>(RandomAffix, SkillModifierRarity.Epic);
        RandomAffix.AddSimilarityTags("special", "random");
        RandomAffix.IsDisabled = true;
        Setup<BurnoutModifier>(Burnout, SkillModifierRarity.Epic);
        Burnout.AddSimilarityTags("dot", "burn");
        Burnout.IsDisabled = true;
        Setup<ComboModifier>(Combo, SkillModifierRarity.Epic);
        Combo.AddSimilarityTags("combo");
        Combo.IsDisabled = true;

        Setup<SilenceModifier>(Silence, SkillModifierRarity.Legendary);
        Silence.AddSimilarityTags("control", "silence");
        Setup<DeathSentenceModifier>(DeathSentence, SkillModifierRarity.Legendary, KillOverrideTag);
        DeathSentence.AddSimilarityTags("execute");
        Setup<ReincarnationTrialModifier>(ReincarnationTrial, SkillModifierRarity.Legendary);
        ReincarnationTrial.AddSimilarityTags("special");
        Setup<EternalCurseModifier>(EternalCurse, SkillModifierRarity.Legendary);
        EternalCurse.AddSimilarityTags("curse", "dot");
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
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        ModClass.I.CommandBuffer.AddComponent(status.Id, new StatusStatsMultiplier
        {
            Value = strength
        });
        // 设置施加方信息，用于计算powerlevel差距
        ref var statusComp = ref status.GetComponent<StatusComponent>();
        statusComp.Source = context.SourceObj;
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
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        // 设置施加方信息，用于计算powerlevel差距
        ref var statusComp = ref status.GetComponent<StatusComponent>();
        statusComp.Source = context.SourceObj;
        target.a.GetExtend().AddSharedStatus(status);
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
        
        // 生成爆炸动画
        ModClass.I.SkillV3.SpawnAnim("cultiway/effect/explosion_fireball", explosionPos, Vector3.right);

        // 获取技能元素和攻击者
        var attacker = context.SourceObj;
        ref var element = ref skill.Asset.Element;
        var damage = context.Strength * damageRatio;

        // 对范围内的敌人造成伤害
        foreach (var obj in SkillUtils.IterEnemyInSphere(explosionPos, radius, attacker))
        {
            if (obj.isActor())
            {
                EventSystemHub.Publish(new GetHitEvent()
                {
                    TargetID = obj.a.data.id,
                    Damage = damage,
                    Element = element,
                    Attacker = attacker
                });
            }
            else
            {
                obj.b.getHit(damage, pAttacker: attacker);
            }
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
            ref var timeLimit = ref burnStatus.GetComponent<AliveTimeLimit>();
            timeLimit.value = duration;
            // 先设置Source信息，用于AddSharedStatus时计算powerlevel差距
            ref var statusComp = ref burnStatus.GetComponent<StatusComponent>();
            statusComp.Source = context.SourceObj;
            ApplyDamageTickState(ref burnStatus, dps, element, context.SourceObj);
            actorExtend.AddSharedStatus(burnStatus);
        }
        else
        {
            ref var timeLimit = ref burnStatus.GetComponent<AliveTimeLimit>();
            timeLimit.value = duration;
            ApplyDamageTickState(ref burnStatus, dps, element, context.SourceObj);
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
            RefreshExistingPoison(duration, damagePerSecond, element, context.SourceObj, poisonStatuses);
            return;
        }

        var status = StatusEffects.Poison.NewEntity();
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        // 先设置Source信息，用于AddSharedStatus时计算powerlevel差距
        ref var statusComp = ref status.GetComponent<StatusComponent>();
        statusComp.Source = context.SourceObj;
        ApplyDamageTickState(ref status, damagePerSecond, element, context.SourceObj);
        actorExtend.AddSharedStatus(status);
    }

    private static void ApplyDamageTickState(ref Entity status, float dps, ElementComposition element, BaseSimObject source)
    {
        ref var tickState = ref status.GetComponent<StatusTickState>();
        tickState.Value = dps;
        tickState.Element = element;
        // Source已移到StatusComponent中，这里不再设置
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

    // 击中单位时施加击飞效果
    private static void ApplyKnockbackEffect(Entity skillEntity, BaseSimObject target)
    {
        if (!target.isActor()) return;

        var actor = target.a;
        if (!actor.asset.can_be_moved_by_powers) return;
        if (actor.position_height > 0f) return;

        if (!skillEntity.TryGetComponent(out SkillEntity skill)) return;
        var container = skill.SkillContainer;
        if (container.IsNull || !container.TryGetComponent(out KnockbackModifier knockback)) return;
        if (!skillEntity.TryGetComponent(out SkillContext context)) return;
        if (!skillEntity.TryGetComponent(out Position skillPos)) return;

        var distance = Mathf.Clamp(knockback.Distance, 0f, 10f);
        var height = Mathf.Clamp(knockback.Height, 0f, 5f);
        if (distance <= 0f && height <= 0f) return;

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
        var status = GetOrCreateStatus(actorExtend, StatusEffects.Weaken);
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        // 设置施加方信息，用于计算powerlevel差距
        ref var statusComp = ref status.GetComponent<StatusComponent>();
        if (statusComp.Source == null)
        {
            statusComp.Source = context.SourceObj;
        }

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
        var status = GetOrCreateStatus(actorExtend, StatusEffects.ArmorBreak);
        ref var timeLimit = ref status.GetComponent<AliveTimeLimit>();
        timeLimit.value = duration;
        // 设置施加方信息，用于计算powerlevel差距
        ref var statusComp = ref status.GetComponent<StatusComponent>();
        if (statusComp.Source == null)
        {
            statusComp.Source = context.SourceObj;
        }

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
        foreach (var obj in SkillUtils.IterEnemyInSphere(skillPos.v2, radius, attacker))
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

    private static Entity GetOrCreateStatus(ActorExtend actorExtend, StatusEffectAsset effect)
    {
        foreach (var status in actorExtend.GetStatuses())
        {
            if (status.IsNull || !status.TryGetComponent(out StatusComponent statusComponent)) continue;
            if (statusComponent.Type == effect)
            {
                return status;
            }
        }

        var newStatus = effect.NewEntity();
        actorExtend.AddSharedStatus(newStatus);
        return newStatus;
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
            stats = status.GetComponent<StatusOverwriteStats>().stats;
            if (stats == null)
            {
                stats = new BaseStats();
            }
            stats.clear();
        }
        return stats;
    }
}
