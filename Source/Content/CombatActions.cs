using System.Linq;
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
        RegisterAssets("Cultiway.CombatActions");
        UseTalisman.rate = 10;
        UseTalisman.action = [Hotfixable] (data) =>
        {
            var ae = data.initiator.a.GetExtend();

            var talisman = ae.GetItems().Where(x => x.HasComponent<Talisman>()).ToList();

            Entity talisman_to_use = default;
            if (talisman.Count > 0)
            {
                talisman_to_use = talisman.GetRandom();
            }
            else if (ae.Base.city != null)
            {
                talisman = ae.Base.city.GetExtend().GetItems().Where(x => x.HasComponent<Talisman>()).ToList();
                if (talisman.Count > 0)
                {
                    talisman_to_use = talisman.GetRandom();
                }
            }

            if (talisman_to_use.IsNull)
            {
                return false;
            }
            
            ref var talisman_component = ref talisman_to_use.GetComponent<Talisman>();
            var addition_strength = talisman_component.Strength;
            var ae_power_level = ae.GetPowerLevel();
            if (talisman_component.PowerLevel > ae_power_level)
            {
                addition_strength *= Mathf.Pow(talisman_component.PowerLevel - ae_power_level, 2);
            }

            if (ae.CastSkillV2(talisman_component.SkillID, data.target, false, addition_strength))
            {
                ModClass.LogInfo($"Use talisman {talisman_to_use.Id} with strength {addition_strength}");
                talisman_to_use.DeleteEntity();
                return true;
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