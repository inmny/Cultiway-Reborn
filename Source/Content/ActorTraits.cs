using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.CultisysComponents;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

[Dependency(typeof(ActorTraitGroups))]
public class ActorTraits : ExtendLibrary<ActorTrait, ActorTraits>
{
    public static ActorTrait PassiveXianCultivate { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets();

        PassiveXianCultivate.group_id = ActorTraitGroups.System.id;
        PassiveXianCultivate.path_icon = "cultiway/icons/traits/iconCultivating";
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
    }
}