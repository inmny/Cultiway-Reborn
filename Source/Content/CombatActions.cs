using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

public class CombatActions : ExtendLibrary<CombatActionAsset, CombatActions>
{
    public static CombatActionAsset UseTalisman { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        UseTalisman.rate = 10;
        UseTalisman.action = [Hotfixable] (data) =>
        {
            var ae = data.initiator.a.GetExtend();

            using var talisman_pool = new ListPool<Entity>(ae.GetItems().Where(x => !x.IsNull && x.HasComponent<Talisman>()));

            bool has_casted = false;

            while (talisman_pool.Any())
            {
                var talisman_to_use = talisman_pool.GetRandom();
                if (talisman_to_use.IsNull)
                {
                    return has_casted;
                }
                ref var talisman_component = ref talisman_to_use.GetComponent<Talisman>();
                if (!ae.CanUseSkillContainerAtCurrentDistance(talisman_component.SkillContainer, data.target,
                        SkillCastFundingSource.Prepaid))
                {
                    break;
                }
                var addition_strength = talisman_component.Strength;
                var ae_power_level = ae.GetPowerLevel();
                if (talisman_component.PowerLevel > ae_power_level)
                {
                    addition_strength *= Mathf.Pow(2, talisman_component.PowerLevel - ae_power_level);
                }

                if (ae.CastSkillV3(talisman_component.SkillContainer, data.target, addition_strength,
                        talisman_component.PowerLevel, SkillCastFundingSource.Prepaid))
                {
                    var direction = data.target.GetSimPos() - data.initiator.GetSimPos();
                    TalismanVfxManager.QueueActivation(data.initiator, talisman_to_use,
                        talisman_component.SkillContainer, direction, talisman_component.PowerLevel,
                        addition_strength);
                    talisman_to_use.DeleteEntity();
                    has_casted = true;
                }
                else
                {
                    break;
                }
            }
            return has_casted;
        };
        
        ActorExtend.RegisterExternalMagicAction(new ExternalMagicActionProvider(
            UseTalisman,
            CanPrepareTalisman,
            ResolveTalismanWeight));
    }

    private static bool CanPrepareTalisman(ActorExtend caster, BaseSimObject target)
    {
        if (!caster.HasComponent<Xian>()) return false;
        foreach (var item in caster.GetItems())
        {
            if (!item.HasComponent<Talisman>()) continue;
            var skill = item.GetComponent<Talisman>().SkillContainer;
            if (caster.CanPrepareSkillContainer(skill, target, SkillCastFundingSource.Prepaid)) return true;
        }
        return false;
    }

    private static int ResolveTalismanWeight(ActorExtend caster, BaseSimObject target)
    {
        if (!caster.HasComponent<Xian>()) return 0;
        foreach (var item in caster.GetItems())
        {
            if (!item.HasComponent<Talisman>()) continue;
            var skill = item.GetComponent<Talisman>().SkillContainer;
            if (caster.CanUseSkillContainerAtCurrentDistance(skill, target, SkillCastFundingSource.Prepaid)) return 1;
        }
        return 0;
    }
}
