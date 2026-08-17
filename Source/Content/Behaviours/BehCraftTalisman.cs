using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehCraftTalisman : BehaviourActionActor
{
    internal static bool CanCraft(ActorExtend actor)
    {
        if (actor == null || actor.Base.isRekt() || !actor.HasCultisys<Xian>()) return false;

        ref var xian = ref actor.GetCultisys<Xian>();
        var maximumWakan = actor.Base.stats[BaseStatses.MaxWakan.id];
        var wakanCost = maximumWakan * 0.01f;
        if (maximumWakan <= 0f || xian.wakan < wakanCost) return false;

        foreach (var skill in actor.all_skills)
        {
            if (!skill.IsNull && skill.HasComponent<SkillContainer>()) return true;
        }
        return false;
    }

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        if (!CanCraft(ae)) return BehResult.Stop;
        
        ref var xian = ref ae.GetCultisys<Xian>();

        var percent = 0.01f;
        var wakan_to_take = pObject.stats[BaseStatses.MaxWakan.id] * percent;
        if (xian.wakan < wakan_to_take)
        {
            return BehResult.Stop;
        }

        using var candidates = new ListPool<Entity>();
        foreach (var skill in ae.all_skills)
        {
            if (!skill.IsNull && skill.HasComponent<SkillContainer>()) candidates.Add(skill);
        }
        if (!candidates.Any()) return BehResult.Stop;
        var skill_v3 = candidates.GetRandom();

        skill_v3 = skill_v3.Store.CloneEntity(skill_v3);
        WakanResourceService.Spend(ae, ref xian, wakan_to_take);
        var power_level = ae.GetPowerLevel();
        var skillContainer = skill_v3.GetComponent<SkillContainer>();
        var colorPalette = skillContainer.ColorPalette;

        string skill_name = skill_v3.HasName ? skill_v3.Name.value : skillContainer.Asset.id;
        var item = SpecialItemUtils.StartBuild(ItemShapes.Talisman, WorldboxGame.I.GetWorldTime(), pObject.getName(), Mathf.Pow(power_level, 2)*10)
            .AddComponent(new Talisman()
            {
                PowerLevel = power_level - 1,
                Strength = wakan_to_take,
                SkillContainer = skill_v3
            })
            .AddComponent(new ItemIconData()
            {
                ColorHex1 = colorPalette.GetHex(0),
                ColorHex2 = colorPalette.GetHex(1),
                ColorHex3 = colorPalette.GetHex(2)
            })
            .AddComponent(new EntityName(TalismanNameGenerator.Instance.GenerateName([skill_name])))
            .Build();
        item.AddRelation(new SkillMasterRelation()
        {
            SkillContainer = skill_v3
        });
        ArtifactProductionResultEvent result = ArtifactProductionService.DispatchResult(
            ae,
            ArtifactProductionProcesses.TalismanCrafting,
            skill_v3,
            item);
        if (result.QualityBonus != 0)
        {
            ref ItemLevel level = ref item.GetComponent<ItemLevel>();
            level = ItemLevel.FromValue(level + result.QualityBonus);
        }
        int outputCount = ArtifactProductionService.ResolveOutputCount(result.YieldMultiplier);
        IHasInventory receiver = pObject.city != null && Randy.randomChance(0.6f)
            ? pObject.city.GetExtend()
            : ae;
        ArtifactProductionService.AddOutputs(receiver, item, outputCount, clone =>
        {
            Entity skill = clone.GetComponent<Talisman>().SkillContainer;
            clone.AddRelation(new SkillMasterRelation { SkillContainer = skill });
        });
        return BehResult.Continue;
    }
}
