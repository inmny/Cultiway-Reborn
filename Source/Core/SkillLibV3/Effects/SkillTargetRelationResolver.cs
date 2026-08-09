using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.SkillLibV3.Effects;

/// <summary>统一技能效果、玩家选取和战术 AI 使用的敌我关系判定。</summary>
public static class SkillTargetRelationResolver
{
    /// <summary>判断目标是否属于施法者本人，或不存在战斗敌意的同国、同联盟单位。</summary>
    public static bool IsFriendly(BaseSimObject source, BaseSimObject target)
    {
        if (source == null || target == null || source.isRekt() || target.isRekt()) return false;
        if (source == target) return true;
        if (!target.isActor()) return false;
        if (HasHostileRelation(source, target)) return false;
        if (source.kingdom == null || target.kingdom == null) return false;
        if (source.kingdom == target.kingdom) return true;
        var sourceAlliance = source.kingdom.getAlliance();
        return sourceAlliance != null && sourceAlliance == target.kingdom.getAlliance();
    }

    /// <summary>判断目标是否可以作为群组共享的敌人；个人仇恨不会传播，发狂目标会传播。</summary>
    public static bool IsSharedHostile(
        BaseSimObject source,
        BaseSimObject target,
        Kingdom attackKingdom = null)
    {
        if (source == null || target == null || source.isRekt() || target.isRekt() || source == target)
            return false;
        Kingdom sourceKingdom = attackKingdom ?? source.kingdom;
        return sourceKingdom?.isEnemy(target.kingdom) == true ||
               target.isActor() && target.a.hasStatusTantrum();
    }

    /// <summary>判断目标是否与来源存在公开敌对、发狂敌对或来源自身的个人仇恨。</summary>
    public static bool HasHostileRelation(
        BaseSimObject source,
        BaseSimObject target,
        Kingdom attackKingdom = null)
    {
        if (source == null || target == null || source.isRekt() || target.isRekt() || source == target)
            return false;
        if (IsSharedHostile(source, target, attackKingdom)) return true;
        return source.isActor() &&
               target.isActor() &&
               source.a.isInAggroList(target.a);
    }

    /// <summary>判断目标是否与来源敌对且当前允许受到来源攻击。</summary>
    public static bool IsHostile(BaseSimObject source, BaseSimObject target, Kingdom attackKingdom = null)
    {
        if (!HasHostileRelation(source, target, attackKingdom)) return false;
        return !source.isActor() || source.a.canAttackTarget(target);
    }

    /// <summary>判断对象是否满足指定结构化效果的目标关系。</summary>
    public static bool Matches(
        SkillEffectTargetRelation relation,
        BaseSimObject source,
        BaseSimObject target,
        Kingdom attackKingdom = null)
    {
        return relation switch
        {
            SkillEffectTargetRelation.Hostile => IsHostile(source, target, attackKingdom),
            SkillEffectTargetRelation.Friendly => IsFriendly(source, target),
            SkillEffectTargetRelation.Self => source == target,
            _ => false,
        };
    }

    /// <summary>判断对象是否满足主动能力公开的敌对、友方、自身或世界地块关系。</summary>
    public static bool Matches(
        SkillUseTargetRelation relation,
        BaseSimObject source,
        BaseSimObject target,
        Kingdom attackKingdom = null)
    {
        return relation switch
        {
            SkillUseTargetRelation.Hostile => IsHostile(source, target, attackKingdom),
            SkillUseTargetRelation.Friendly => IsFriendly(source, target),
            SkillUseTargetRelation.Self => source == target,
            _ => false,
        };
    }
}
