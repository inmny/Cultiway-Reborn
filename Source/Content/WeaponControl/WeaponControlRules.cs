using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using strings;

namespace Cultiway.Content.WeaponControl;

/// <summary>一次御器释放在进入通用施放序列前解析出的完整计划。</summary>
internal readonly struct WeaponControlPreparedCast
{
    /// <summary>施放开始时装备的真实武器。</summary>
    public readonly Item Weapon;

    /// <summary>真实武器对应的装备资产。</summary>
    public readonly EquipmentAsset WeaponAsset;

    /// <summary>武器按运行行为归一化后的器形。</summary>
    public readonly WeaponControlCategory Category;

    /// <summary>本次释放选择的整体招式。</summary>
    public readonly WeaponControlCastMode Mode;

    /// <summary>逐发目标与绝对延迟组成的通用技能计划。</summary>
    public readonly SkillCastPlan Plan;

    /// <summary>本次招式能够继续重定向目标的最大距离。</summary>
    public readonly float Range;

    /// <summary>从起手到最后一道攻击生成完成的预计秒数。</summary>
    public readonly float Duration;

    /// <summary>创建一份不可变的御器施放计划。</summary>
    public WeaponControlPreparedCast(
        Item weapon,
        EquipmentAsset weaponAsset,
        WeaponControlCategory category,
        WeaponControlCastMode mode,
        SkillCastPlan plan,
        float range,
        float duration)
    {
        Weapon = weapon;
        WeaponAsset = weaponAsset;
        Category = category;
        Mode = mode;
        Plan = plan;
        Range = range;
        Duration = duration;
    }
}

/// <summary>集中维护御器的境界资格、器形识别、数量决策和目标规划。</summary>
internal static class WeaponControlRules
{
    private const int MaxCandidateTargets = 18;

    /// <summary>突刺相对同境界普通近战控制距离的长度倍率。</summary>
    internal const float ThrustReachMultiplier = 2.5f;

    /// <summary>判断角色是否已经进入筑基期并具备使用御器的体系资格。</summary>
    public static bool IsEligibleCultivator(ActorExtend actor)
    {
        return GeneralSettings.EnableSkillSystems && actor != null && !actor.Base.isRekt() &&
               actor.HasCultisys<Xian>() &&
               actor.GetCultisys<Xian>().CurrLevel >= XianLevels.XianBase;
    }

    /// <summary>返回角色当前仙道大境界；不具备仙道体系时返回练气层级。</summary>
    public static int ResolveRealm(ActorExtend actor)
    {
        return actor != null && actor.HasCultisys<Xian>()
            ? actor.GetCultisys<Xian>().CurrLevel
            : XianLevels.QiRefinement;
    }

    /// <summary>解析当前真实武器及器形，默认攻击和已经损坏的装备不能作为御器载体。</summary>
    public static bool TryResolveWeapon(
        ActorExtend actor,
        out Item weapon,
        out EquipmentAsset weaponAsset,
        out WeaponControlCategory category)
    {
        weapon = null;
        weaponAsset = null;
        category = WeaponControlCategory.Other;
        if (actor == null || actor.Base.isRekt() || !actor.Base.hasWeapon()) return false;

        weapon = actor.Base.getWeapon();
        weaponAsset = actor.Base.getWeaponAsset();
        if (weapon == null || !weapon.isAlive() || weapon.isBroken() || weaponAsset == null ||
            weaponAsset.equipment_type != EquipmentType.Weapon) return false;

        category = Classify(weaponAsset);
        return category != WeaponControlCategory.Ranged ||
               !string.IsNullOrWhiteSpace(weaponAsset.projectile) &&
               AssetManager.projectiles.get(weaponAsset.projectile) != null;
    }

    /// <summary>返回当前器形在对应境界下的普通控制距离。</summary>
    public static float ResolveRange(ActorExtend actor, WeaponControlCategory category)
    {
        int realm = ResolveRealm(actor);
        if (category == WeaponControlCategory.Ranged)
        {
            return realm switch
            {
                XianLevels.XianBase => 28f,
                XianLevels.Jindan => 44f,
                _ => 60f,
            };
        }

        return realm switch
        {
            XianLevels.XianBase => 5.5f,
            XianLevels.Jindan => 6.25f,
            _ => 7f,
        };
    }

    /// <summary>返回 AI 和施放入口允许搜索目标的最远距离，可突刺器形包含完整突刺距离。</summary>
    public static float ResolveSelectionRange(ActorExtend actor, WeaponControlCategory category)
    {
        float range = ResolveRange(actor, category);
        return SupportsThrust(category) ? range * ThrustReachMultiplier : range;
    }

    /// <summary>按当前武器、目标压力和剩余灵气构造一次可执行的御器计划。</summary>
    public static bool TryPrepareCast(
        ActorExtend caster,
        Entity skillContainer,
        BaseSimObject primaryTarget,
        Kingdom attackKingdom,
        out WeaponControlPreparedCast prepared)
    {
        prepared = default;
        if (!IsEligibleCultivator(caster) || caster.Base.isFlying() || primaryTarget.isRekt() ||
            !caster.Base.canAttackTarget(primaryTarget) ||
            !TryResolveWeapon(caster, out Item weapon, out EquipmentAsset weaponAsset,
                out WeaponControlCategory category)) return false;

        float baseRange = ResolveRange(caster, category);
        float selectionRange = ResolveSelectionRange(caster, category);
        float targetSize = primaryTarget.stats[strings.S.size];
        float targetDistanceSquared =
            (primaryTarget.current_position - caster.Base.current_position).sqrMagnitude;
        float allowedSelectionRange = selectionRange + targetSize;
        if (targetDistanceSquared > allowedSelectionRange * allowedSelectionRange) return false;

        WeaponControlCastMode mode = WeaponControlCastMode.MeleeSweep;
        if (category != WeaponControlCategory.Ranged)
        {
            float allowedBaseRange = baseRange + targetSize;
            mode = SupportsThrust(category) && targetDistanceSquared > allowedBaseRange * allowedBaseRange
                ? WeaponControlCastMode.MeleeThrust
                : ResolveMode(category, 0);
        }

        float range = mode == WeaponControlCastMode.MeleeThrust ? selectionRange : baseRange;
        float allowedRange = range + targetSize;
        if (targetDistanceSquared > allowedRange * allowedRange) return false;

        var candidates = new List<BaseSimObject>(MaxCandidateTargets);
        CollectTargets(caster, primaryTarget, attackKingdom, range, category, candidates);
        int affordable = SkillCastCost.GetAffordableStepLimit(caster, skillContainer);
        int emissionCount = ResolveEmissionCount(caster, category, primaryTarget, candidates.Count, affordable);
        if (emissionCount <= 0) return false;

        if (category == WeaponControlCategory.Ranged) mode = ResolveMode(category, candidates.Count);
        float interval = ResolveEmissionInterval(caster, category);
        var plan = new SkillCastPlan();
        for (var i = 0; i < emissionCount; i++)
        {
            BaseSimObject target = SelectTarget(primaryTarget, candidates, i, mode);
            plan.Steps.Add(new SkillCastStep(target, i * interval));
        }

        float actionTail = category == WeaponControlCategory.Ranged ? 0.22f : 0.48f;
        float duration = (emissionCount - 1) * interval + actionTail;
        prepared = new WeaponControlPreparedCast(
            weapon,
            weaponAsset,
            category,
            mode,
            plan,
            range,
            duration);
        return true;
    }

    /// <summary>估计 AI 规划当前御器动作时会实际发出的攻击数量。</summary>
    public static int ResolveExpectedEmissionCount(
        ActorExtend caster,
        Entity skillContainer,
        BaseSimObject target,
        WeaponControlCategory category)
    {
        if (target.isRekt()) return 0;
        int nearbyEnemies = CountNearbyEnemies(caster, target, category);
        int affordable = SkillCastCost.GetAffordableStepLimit(caster, skillContainer);
        return ResolveEmissionCount(caster, category, target, nearbyEnemies, affordable);
    }

    /// <summary>按原版装备分组识别远程、剑、矛、斧、锤和杖；未知近战武器保留通用扫掠。</summary>
    private static WeaponControlCategory Classify(EquipmentAsset asset)
    {
        if (asset.attack_type == WeaponType.Range) return WeaponControlCategory.Ranged;
        string group = asset.group_id ?? string.Empty;
        return group switch
        {
            S_EquipmentGroup.sword => WeaponControlCategory.Sword,
            S_EquipmentGroup.spear => WeaponControlCategory.Spear,
            S_EquipmentGroup.axe => WeaponControlCategory.Axe,
            S_EquipmentGroup.hammer => WeaponControlCategory.Hammer,
            S_EquipmentGroup.staff => WeaponControlCategory.Staff,
            _ => WeaponControlCategory.Other,
        };
    }

    /// <summary>收集主目标、施法者近期攻击者与目标周围敌人，避免连续攻击只锁死一个对象。</summary>
    private static void CollectTargets(
        ActorExtend caster,
        BaseSimObject primaryTarget,
        Kingdom attackKingdom,
        float range,
        WeaponControlCategory category,
        ICollection<BaseSimObject> output)
    {
        AddTarget(caster.Base, primaryTarget, range, output);
        foreach (BaseSimObject attacker in caster.GetRecentAttackersSnapshot())
        {
            AddTarget(caster.Base, attacker, range, output);
            if (output.Count >= MaxCandidateTargets) return;
        }

        float clusterRadius = category == WeaponControlCategory.Ranged ? 10f : Mathf.Min(range, 6f);
        foreach (BaseSimObject target in SkillUtils.IterEnemyInSphere(
                     primaryTarget.current_position,
                     clusterRadius,
                     caster.Base,
                     attackKingdom))
        {
            AddTarget(caster.Base, target, range, output);
            if (output.Count >= MaxCandidateTargets) return;
        }
    }

    /// <summary>仅把仍可攻击、处于控制距离内且尚未出现的对象加入候选集。</summary>
    private static void AddTarget(
        Actor caster,
        BaseSimObject target,
        float range,
        ICollection<BaseSimObject> output)
    {
        if (target.isRekt() || target == caster || !caster.canAttackTarget(target) || output.Contains(target))
            return;
        float allowedRange = range + target.stats[strings.S.size];
        if ((target.current_position - caster.current_position).sqrMagnitude > allowedRange * allowedRange)
            return;
        output.Add(target);
    }

    /// <summary>在境界上限内按敌群密度、强敌压力、受伤程度和实际灵气共同决定发射数。</summary>
    private static int ResolveEmissionCount(
        ActorExtend caster,
        WeaponControlCategory category,
        BaseSimObject primaryTarget,
        int candidateCount,
        int affordable)
    {
        int maximum = ResolveEmissionCap(ResolveRealm(caster), category);
        maximum = Math.Min(maximum, Mathf.Max(0, affordable));
        if (maximum <= 0) return 0;

        float densityDivisor = category == WeaponControlCategory.Ranged ? 7f : 4f;
        float density = Mathf.Clamp01(Mathf.Max(1, candidateCount) / densityDivisor);
        float targetPower = primaryTarget.isActor()
            ? primaryTarget.a.GetExtend().GetPowerLevel()
            : caster.GetPowerLevel();
        float threat = Mathf.Clamp01((targetPower - caster.GetPowerLevel() + 3f) / 7f);
        float maxHealth = Mathf.Max(1f, caster.Base.stats[strings.S.health]);
        float injury = 1f - Mathf.Clamp01(caster.Base.data.health / maxHealth);
        float intent = category == WeaponControlCategory.Ranged
            ? 0.12f + density * 0.42f + threat * 0.32f + injury * 0.12f
            : 0.2f + density * 0.34f + threat * 0.28f + injury * 0.14f;
        return Mathf.Clamp(Mathf.CeilToInt(maximum * Mathf.Clamp01(intent)), 1, maximum);
    }

    /// <summary>返回筑基、金丹和元婴在远程或近战招式中的硬上限。</summary>
    private static int ResolveEmissionCap(int realm, WeaponControlCategory category)
    {
        bool ranged = category == WeaponControlCategory.Ranged;
        return realm switch
        {
            XianLevels.XianBase => ranged ? 24 : 8,
            XianLevels.Jindan => ranged ? 64 : 16,
            _ => ranged ? 256 : 32,
        };
    }

    /// <summary>按敌群密度和器形选择天空倾泻、箭雨、挥砍、突刺或重砸。</summary>
    private static WeaponControlCastMode ResolveMode(WeaponControlCategory category, int candidateCount)
    {
        if (category == WeaponControlCategory.Ranged)
        {
            float skyVolleyChance = candidateCount >= 4 ? 0.7f : 0.35f;
            return Randy.randomChance(skyVolleyChance)
                ? WeaponControlCastMode.SkyVolley
                : WeaponControlCastMode.ArrowRain;
        }

        return category switch
        {
            WeaponControlCategory.Spear => Randy.randomChance(0.82f)
                ? WeaponControlCastMode.MeleeThrust
                : WeaponControlCastMode.MeleeSweep,
            WeaponControlCategory.Hammer => Randy.randomChance(0.78f)
                ? WeaponControlCastMode.MeleeCrush
                : WeaponControlCastMode.MeleeSweep,
            WeaponControlCategory.Axe => Randy.randomChance(0.24f)
                ? WeaponControlCastMode.MeleeCrush
                : WeaponControlCastMode.MeleeSweep,
            WeaponControlCategory.Staff => Randy.randomChance(0.42f)
                ? WeaponControlCastMode.MeleeThrust
                : WeaponControlCastMode.MeleeSweep,
            WeaponControlCategory.Sword => Randy.randomChance(0.28f)
                ? WeaponControlCastMode.MeleeThrust
                : WeaponControlCastMode.MeleeSweep,
            _ => WeaponControlCastMode.MeleeSweep,
        };
    }

    /// <summary>判断当前器形是否拥有水平突刺动作。</summary>
    private static bool SupportsThrust(WeaponControlCategory category)
    {
        return category is WeaponControlCategory.Spear or WeaponControlCategory.Staff or WeaponControlCategory.Sword;
    }

    /// <summary>让更高境界用更密集的节奏展开攻击，同时保留可辨认的单道动作。</summary>
    private static float ResolveEmissionInterval(ActorExtend caster, WeaponControlCategory category)
    {
        int realm = ResolveRealm(caster);
        if (category == WeaponControlCategory.Ranged)
        {
            return realm switch
            {
                XianLevels.XianBase => 0.07f,
                XianLevels.Jindan => 0.045f,
                _ => 0.018f,
            };
        }

        return realm switch
        {
            XianLevels.XianBase => 0.11f,
            XianLevels.Jindan => 0.075f,
            _ => 0.045f,
        };
    }

    /// <summary>让第一击锁定主目标，其余步骤在敌群中均匀展开并定期回压主目标。</summary>
    private static BaseSimObject SelectTarget(
        BaseSimObject primaryTarget,
        IReadOnlyList<BaseSimObject> candidates,
        int index,
        WeaponControlCastMode mode)
    {
        if (index == 0 || candidates.Count <= 1) return primaryTarget;
        int primaryPeriod = mode == WeaponControlCastMode.MeleeThrust ? 2 : 4;
        if (index % primaryPeriod == 0) return primaryTarget;
        return candidates[1 + (index - 1) % (candidates.Count - 1)];
    }

    /// <summary>统计目标周围会被本次器形纳入决策的敌人数量。</summary>
    private static int CountNearbyEnemies(
        ActorExtend caster,
        BaseSimObject target,
        WeaponControlCategory category)
    {
        int count = 0;
        float radius = category == WeaponControlCategory.Ranged ? 10f : 6f;
        foreach (BaseSimObject _ in SkillUtils.IterEnemyInSphere(
                     target.current_position,
                     radius,
                     caster.Base,
                     caster.Base.kingdom))
        {
            count++;
            if (count >= MaxCandidateTargets) break;
        }
        return Mathf.Max(1, count);
    }
}
