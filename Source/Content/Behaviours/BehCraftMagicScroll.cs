using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 制作一个封存已掌握 mana 法术的魔法卷轴，并按符箓规则分配给城市或制作者。
/// </summary>
public sealed class BehCraftMagicScroll : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var actor = pObject.GetExtend();
        if (!actor.HasCultisys<Magic>()) return BehResult.Stop;

        using var candidates = new ListPool<Entity>();
        foreach (var skill in actor.GetLearnedSkillsInOrder())
        {
            if (CanCraftFromSkill(actor, skill)) candidates.Add(skill);
        }
        if (!candidates.Any()) return BehResult.Stop;

        var skillContainer = candidates.GetRandom();
        var manaCost = ResolveManaCost(skillContainer);
        if (manaCost <= 0 || pObject.getMana() < manaCost) return BehResult.Stop;

        var skillComponent = skillContainer.GetComponent<SkillContainer>();
        var powerLevel = actor.GetPowerLevel();
        var skillName = skillContainer.HasName ? skillContainer.Name.value : skillComponent.Asset.id;
        var scroll = SpecialItemUtils
            .StartBuild(ItemShapes.MagicScroll, WorldboxGame.I.GetWorldTime(), pObject.getName(),
                Mathf.Max(10f, Mathf.Pow(powerLevel, 2f) * 10f), pObject.asset?.id ?? string.Empty)
            .AddComponent(new MagicScroll
            {
                Strength = SkillContext.DefaultStrength,
                PowerLevel = powerLevel,
                SkillContainer = skillContainer
            })
            .AddComponent(new ItemIconData
            {
                ColorHex1 = skillComponent.Asset.Element.HexColor()
            })
            .AddComponent(new EntityName($"{skillName}卷轴"))
            .Build();
        scroll.AddRelation(new SkillMasterRelation
        {
            SkillContainer = skillContainer
        });
        pObject.setMana(pObject.getMana() - manaCost);

        if (pObject.city != null && Randy.randomChance(0.6f))
        {
            pObject.city.GetExtend().AddSpecialItem(scroll);
        }
        else
        {
            actor.AddSpecialItem(scroll);
        }
        return BehResult.Continue;
    }

    /// <summary>
    /// 判断魔法师当前是否掌握至少一个能够支付制作成本的 mana 法术。
    /// </summary>
    internal static bool CanCraft(ActorExtend actor)
    {
        if (actor == null || actor.Base.isRekt() || !actor.HasCultisys<Magic>()) return false;
        foreach (var skill in actor.GetLearnedSkillsInOrder())
        {
            if (CanCraftFromSkill(actor, skill)) return true;
        }
        return false;
    }

    private static bool CanCraftFromSkill(ActorExtend actor, Entity skill)
    {
        if (!SkillCastResourceResolver.UsesResource(skill, SkillCastResources.Mana)) return false;
        if (!skill.HasComponent<SkillCastParameters>()) return false;
        var manaCost = ResolveManaCost(skill);
        return manaCost > 0 && actor.Base.getMana() >= manaCost;
    }

    private static int ResolveManaCost(Entity skill)
    {
        return Mathf.Max(1, Mathf.CeilToInt(SkillCastCost.CalculateStepDemand(skill)));
    }
}
