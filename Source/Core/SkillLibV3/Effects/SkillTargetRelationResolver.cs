using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.SkillLibV3.Effects;

/// <summary>统一技能效果、玩家选取和战术 AI 使用的敌我关系判定。</summary>
public static class SkillTargetRelationResolver
{
    /// <summary>判断目标是否属于施法者本人、同国或同联盟单位。</summary>
    public static bool IsFriendly(BaseSimObject source, BaseSimObject target)
    {
        if (source == null || target == null || source.isRekt() || target.isRekt()) return false;
        if (source == target) return true;
        if (!target.isActor()) return false;
        if (source.kingdom == null || target.kingdom == null) return false;
        if (source.kingdom == target.kingdom) return true;
        var sourceAlliance = source.kingdom.getAlliance();
        return sourceAlliance != null && sourceAlliance == target.kingdom.getAlliance();
    }

    /// <summary>判断目标是否是来源当前允许攻击的对象。</summary>
    public static bool IsHostile(BaseSimObject source, BaseSimObject target, Kingdom attackKingdom = null)
    {
        if (source == null || target == null || source.isRekt() || target.isRekt() || source == target)
            return false;
        if (source.isActor() && source.a.canAttackTarget(target)) return true;
        Kingdom sourceKingdom = attackKingdom ?? source.kingdom;
        return sourceKingdom?.isEnemy(target.kingdom) ?? false;
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
