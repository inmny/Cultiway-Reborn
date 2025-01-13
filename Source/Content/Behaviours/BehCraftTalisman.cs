using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehCraftTalisman : BehaviourActionActor
{
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
        var item = SpecialItemUtils.StartBuild(ItemShapes.Talisman.id, World.world.getCreationTime(), pObject.getName())
            .AddComponent(new Talisman()
            {
                PowerLevel = ae.GetPowerLevel() - 1,
                SkillID = ae.tmp_all_skills.GetRandom(),
                Strength = wakan_to_take
            })
            .Build();
        if (pObject.city != null)
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