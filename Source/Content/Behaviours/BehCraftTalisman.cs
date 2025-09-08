using ai.behaviours;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehCraftTalisman : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        
        ref var xian = ref ae.GetCultisys<Xian>();

        var percent = 0.01f;
        var wakan_to_take = pObject.stats[BaseStatses.MaxWakan.id] * percent;
        if (xian.wakan < wakan_to_take)
        {
            return BehResult.Continue;
        }

        if (ae.all_skills.Count == 0)
        {
            return BehResult.Continue;
        }
        var skill_v3 = ae.all_skills.GetRandom();
        if (skill_v3.IsNull)
        {
            return BehResult.Continue;
        }

        skill_v3 = skill_v3.Store.CloneEntity(skill_v3);
        xian.wakan -= wakan_to_take;
        var power_level = ae.GetPowerLevel();
        var item = SpecialItemUtils.StartBuild(ItemShapes.Talisman.id, WorldboxGame.I.GetWorldTime(), pObject.getName(), Mathf.Pow(power_level, 2)*10)
            .AddComponent(new Talisman()
            {
                PowerLevel = power_level - 1,
                Strength = wakan_to_take,
                SkillContainer = skill_v3
            })
            .AddComponent(new EntityName(TalismanNameGenerator.Instance.GenerateName([skill_v3.Name.value])))
            .Build();
        item.AddRelation(new SkillMasterRelation()
        {
            SkillContainer = skill_v3
        });
        if (pObject.city != null && Randy.randomChance(0.6f))
        {
            pObject.city.GetExtend().AddSpecialItem(item);
        }
        else
        {
            ae.AddSpecialItem(item);
        }
        return BehResult.Continue;
    }
}