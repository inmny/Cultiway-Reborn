using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Events;
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
    public static CombatActionAsset UseMagicScroll { get; private set; }
    public static CombatActionAsset ArtifactSpatialAttack { get; private set; }
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

        UseMagicScroll.rate = 10;
        UseMagicScroll.action = [Hotfixable] (data) =>
        {
            var caster = data.initiator.a.GetExtend();
            using var scrollPool = new ListPool<Entity>();
            foreach (var item in caster.GetItems())
            {
                if (item.IsNull || !item.HasComponent<MagicScroll>()) continue;
                var scroll = item.GetComponent<MagicScroll>();
                if (caster.CanUseSkillContainerAtCurrentDistance(scroll.SkillContainer, data.target,
                        SkillCastFundingSource.Prepaid))
                    scrollPool.Add(item);
            }

            if (!scrollPool.Any()) return false;
            var scrollEntity = scrollPool.GetRandom();
            var scrollComponent = scrollEntity.GetComponent<MagicScroll>();
            if (!caster.CastSkillV3(scrollComponent.SkillContainer, data.target, scrollComponent.Strength,
                    scrollComponent.PowerLevel, SkillCastFundingSource.Prepaid)) return false;

            scrollEntity.DeleteEntity();
            return true;
        };

        ActorExtend.RegisterExternalMagicAction(new ExternalMagicActionProvider(
            UseMagicScroll,
            CanPrepareMagicScroll,
            ResolveMagicScrollWeight));

        ArtifactSpatialAttack.rate = 8;
        ArtifactSpatialAttack.is_spell_use = true;
        ArtifactSpatialAttack.action = data =>
        {
            ArtifactSpatialAttackEvent evt = new(data.target);
            return ArtifactAbilityDispatcher.Dispatch(data.initiator.a.GetExtend().E, evt) > 0;
        };
        ActorExtend.RegisterExternalMagicAction(new ExternalMagicActionProvider(
            ArtifactSpatialAttack,
            CanPrepareArtifactSpatialAttack,
            ResolveArtifactSpatialAttackWeight));
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

    private static bool CanPrepareMagicScroll(ActorExtend caster, BaseSimObject target)
    {
        if (!caster.HasCultisys<Magic>()) return false;
        foreach (var item in caster.GetItems())
        {
            if (item.IsNull || !item.HasComponent<MagicScroll>()) continue;
            var skill = item.GetComponent<MagicScroll>().SkillContainer;
            if (caster.CanPrepareSkillContainer(skill, target, SkillCastFundingSource.Prepaid)) return true;
        }
        return false;
    }

    private static int ResolveMagicScrollWeight(ActorExtend caster, BaseSimObject target)
    {
        if (!caster.HasCultisys<Magic>()) return 0;

        // 卷轴作为个人法术因法杖或 mana 不可用时的后备手段，避免 AI 无谓消耗一次性物品。
        foreach (var skill in caster.all_attack_skills)
        {
            if (caster.CanUseSkillContainerAtCurrentDistance(skill, target,
                    SkillCastFundingSource.CasterResources)) return 0;
        }

        foreach (var item in caster.GetItems())
        {
            if (item.IsNull || !item.HasComponent<MagicScroll>()) continue;
            var skill = item.GetComponent<MagicScroll>().SkillContainer;
            if (caster.CanUseSkillContainerAtCurrentDistance(skill, target, SkillCastFundingSource.Prepaid)) return 1;
        }
        return 0;
    }

    private static bool CanPrepareArtifactSpatialAttack(ActorExtend caster, BaseSimObject target)
    {
        return !target.isRekt() && ArtifactAbilityDispatcher.HasHandler<ArtifactSpatialAttackEvent>(
            caster.E,
            ArtifactControlState.Ready);
    }

    private static int ResolveArtifactSpatialAttackWeight(ActorExtend caster, BaseSimObject target)
    {
        return ArtifactAbilityDispatcher.CanDispatch(caster.E, new ArtifactSpatialAttackEvent(target)) ? 1 : 0;
    }
}
