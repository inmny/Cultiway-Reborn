using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

[Dependency(typeof(ActorTraitGroups))]
public class ActorTraits : ExtendLibrary<ActorTrait, ActorTraits>
{
    public static ActorTrait Cultivator { get; private set; }
    public static ActorTrait PassiveXianCultivate { get; private set; }
    public static ActorTrait SignIn { get; private set; }
    [GetOnly("immortal")]
    public  static ActorTrait Immortal { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        Cultivator.group_id = ActorTraitGroups.Mind.id;
        Cultivator.path_icon = "cultiway/icons/traits/iconCultivator";

        PassiveXianCultivate.group_id = ActorTraitGroups.System.id;
        PassiveXianCultivate.path_icon = "cultiway/icons/traits/iconPassiveXianCultivate";
        PassiveXianCultivate.special_effect_interval = TimeScales.SecPerMonth;
        PassiveXianCultivate.action_special_effect = (actor, tile) =>
        {
            ActorExtend ae = actor.a.GetExtend();
            if (!ae.HasCultisys<Xian>()) return false;
            ref Xian xian = ref ae.GetCultisys<Xian>();
            Cultisyses.TakeWakanAndCultivate(ae, ref xian);
            if (Cultisyses.Xian.AllowUpgrade(ae)) Cultisyses.Xian.TryPerformUpgrade(ae);

            return true;
        };

        SignIn.group_id = ActorTraitGroups.System.id;
        SignIn.path_icon = "cultiway/icons/traits/iconPassiveXianCultivate";
        SignIn.special_effect_interval = TimeScales.SecPerYear;
        SignIn.action_special_effect = (actor, tile) =>
        {
            var a = actor.a;
            var ae = a.GetExtend();
            if (!ae.HasElementRoot())
            {
                ae.AddComponent(ElementRoot.Roll());
                return true;
            }

            if (!ae.HasCultisys<Xian>())
            {
                ae.NewCultisys(Cultisyses.Xian);
                return true;
            }

            if (!a.hasStatus(WorldboxGame.StatusEffects.Caffeinated.id))
            {
                a.addStatusEffect(WorldboxGame.StatusEffects.Caffeinated.id, TimeScales.SecPerYear * 100);
                return true;
            }
            
            ref var xian = ref ae.GetCultisys<Xian>();
            xian.wakan = a.stats[BaseStatses.MaxWakan.id];
            if (Cultisyses.Xian.AllowUpgrade(ae))
            {
                Cultisyses.Xian.TryPerformUpgrade(ae);
            }

            return true;
        };
    }

    protected override void PostInit(ActorTrait asset)
    {
        if (asset.group_id == ActorTraitGroups.System.id)
        {
            var list = new List<string>();
            foreach (ActorTrait trait in cached_library.list)
            {
                if (trait.id       == asset.id) continue;
                if (trait.group_id == ActorTraitGroups.System.id) list.Add(trait.id);
            }

            asset.oppositeArr = list.ToArray();
        }
    }
}