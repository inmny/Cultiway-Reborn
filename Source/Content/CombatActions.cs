using System.Linq;
using System.Runtime.Remoting.Channels;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

public class CombatActions : ExtendLibrary<CombatActionAsset, CombatActions>
{
    public static CombatActionAsset UseTalisman { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        UseTalisman.rate = 10;
        UseTalisman.action = [Hotfixable] (data) =>
        {
            var ae = data.initiator.a.GetExtend();

            using var talisman_pool = new ListPool<Entity>(ae.GetItems().Where(x => x.HasComponent<Talisman>()));

            bool has_casted = false;

            while (talisman_pool.Any())
            {
                var talisman_to_use = talisman_pool.GetRandom();
                if (talisman_to_use.IsNull)
                {
                    return has_casted;
                }
                ref var talisman_component = ref talisman_to_use.GetComponent<Talisman>();
                var addition_strength = talisman_component.Strength;
                var ae_power_level = ae.GetPowerLevel();
                if (talisman_component.PowerLevel > ae_power_level)
                {
                    addition_strength *= Mathf.Pow(2, talisman_component.PowerLevel - ae_power_level);
                }

                if (ae.CastSkillV3(talisman_component.SkillContainer, data.target))
                {
                    talisman_to_use.DeleteEntity(); 
                    has_casted = true;
                }
                else
                {
                    break;
                }
            }
            return false;
        };
        
        ActorExtend.RegisterCombatActionOnAttack(((ae, target, list) =>
        {
            if (ae.HasComponent<Xian>())
            {
                UseTalisman.AddToPool(list);
            }
        }));
    }
}