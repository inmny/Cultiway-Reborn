using ai.behaviours;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
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
        xian.wakan -= wakan_to_take;
        var power_level = ae.GetPowerLevel();
        var skill = ae.tmp_all_skills.GetRandom();
        var item = SpecialItemUtils.StartBuild(ItemShapes.Talisman.id, World.world.getCreationTime(), pObject.getName(), Mathf.Pow(power_level, 2)*10)
            .AddComponent(new Talisman()
            {
                PowerLevel = power_level - 1,
                SkillID = skill,
                Strength = wakan_to_take
            })
            .AddComponent(new EntityName(TalismanNameGenerator.Instance.GenerateName([ModClass.L.WrappedSkillLibrary.get(skill).GetName()])))
            .Build();
        if (pObject.city != null && Toolbox.randomChance(0.6f))
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