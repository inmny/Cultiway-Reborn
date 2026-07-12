using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>
/// 维护魔法师已掌握技能与魔法知识关系之间的一致性。
/// </summary>
public static class MagicKnowledgeService
{
    /// <summary>
    /// 移除失效关系，并为已掌握但尚未登记的 mana 技能补充知识关系。
    /// </summary>
    public static void Synchronize(ActorExtend actor)
    {
        if (actor == null) return;

        var staleKnowledge = new List<Entity>();
        foreach (var relation in actor.E.GetRelations<MagicSpellKnowledgeRelation>())
        {
            if (relation.SkillContainer.IsNull || !actor.OwnsLearnedSkill(relation.SkillContainer))
                staleKnowledge.Add(relation.SkillContainer);
        }

        foreach (var container in staleKnowledge)
            actor.E.RemoveRelation<MagicSpellKnowledgeRelation>(container);

        foreach (var skill in actor.GetLearnedSkillsInOrder())
            Ensure(actor, skill);
    }

    /// <summary>
    /// 确保施法者持有的 mana 技能具有对应的魔法知识关系。
    /// </summary>
    public static bool Ensure(ActorExtend actor, Entity skill)
    {
        if (actor == null || skill.IsNull || !actor.OwnsLearnedSkill(skill)) return false;
        if (!SkillCastResourceResolver.UsesResource(skill, SkillCastResources.Mana)) return false;
        if (Contains(actor, skill)) return true;
        if (MagicSpellProfile.Resolve(skill) == null) return false;

        actor.E.AddRelation(new MagicSpellKnowledgeRelation
        {
            SkillContainer = skill,
            LearnedWorldTime = GetWorldTime(),
            Source = MagicWebManager.Instance?.Contains(skill) == true
                ? MagicSpellKnowledgeSource.MagicWeb
                : MagicSpellKnowledgeSource.SelfCreated
        });
        return true;
    }

    /// <summary>
    /// 判断施法者是否登记了指定法术知识。
    /// </summary>
    public static bool Contains(ActorExtend actor, Entity skill)
    {
        if (actor == null || skill.IsNull) return false;

        foreach (var relation in actor.E.GetRelations<MagicSpellKnowledgeRelation>())
        {
            if (relation.SkillContainer == skill) return true;
        }

        return false;
    }

    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }
}
