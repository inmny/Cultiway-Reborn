using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.ActiveAbilities;
using Cultiway.Content.Systems.Logic;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Progression;
using Cultiway.Patch;
using Friflo.Engine.ECS;

namespace Cultiway.Content.YaoBeasts;

/// <summary>接通妖兽玩法的全部系统、服务与生命周期钩子。</summary>
[Dependency(typeof(YaoContent), typeof(SkillEntities), typeof(SkillCastResources), typeof(BaseStatses))]
public sealed class YaoModule : ICanInit
{
    public void Init()
    {
        // 静态技能容器：器官与妖丹引用的技能只建立一次。
        CreatureOrganSkillRegistry.EnsureBuilt();
        foreach (YaoCorePatternAsset pattern in YaoCorePatterns.All)
        {
            foreach (string skillId in pattern.SkillIds) CreatureOrganSkillRegistry.RegisterSkill(skillId);
        }

        // 玩法服务钩子：启灵积累、精华掉落、天劫与涅槃。
        YaoAwakeningService.Initialize();
        YaoDigestionService.Initialize();
        YaoTribulationService.Initialize();
        YaoNirvanaService.Initialize();
        PatchMapBox.RegisterActionOnClearWorld(YaoDigestionService.ClearWorldState);
        PatchMapBox.RegisterActionOnClearWorld(YaoTribulationService.ClearWorldState);
        PatchMapBox.RegisterActionOnClearWorld(YaoHumanFormService.ClearWorldState);

        // 大境界提交后的固定后续玩法：保命次数、固血与返祖。
        ProgressionLifecycle.RegisterCommitted(evt =>
        {
            if (evt.Kind != ProgressionKind.Major) return;
            if (!evt.Actor.HasCultisys<Yao>()) return;
            ref Yao yao = ref evt.Actor.GetCultisys<Yao>();
            if (HasOrgan(evt.Actor, "yao.heart.nirvana"))
            {
                yao.PhoenixRevivalUses = System.Math.Min(yao.PhoenixRevivalUses + 1, 3);
                evt.Actor.GetCultisys<Yao>() = yao;
            }

            if (TryGetOrganRank(evt.Actor, "yao.crown.tails", out int crownRank))
            {
                yao.NineTailLifeUses = crownRank;
                evt.Actor.GetCultisys<Yao>() = yao;
            }

            YaoBloodlineService.TrySolidify(evt.Actor, ref yao);
            if (evt.Actor.E.TryGetComponent(out YaoGenome genome))
            {
                YaoAtavismResolver.Resolve(evt.Actor, YaoAtavismNode.MajorBreakthrough, ref genome);
            }
        });

        // 逻辑系统。
        ModClass.I.GeneralLogicSystems.Add(new RestoreYaoPowerSystem());
        ModClass.I.GeneralLogicSystems.Add(new YaoAwakeningSystem());
        ModClass.I.GeneralLogicSystems.Add(new YaoDigestionSystem());
        ModClass.I.GeneralLogicSystems.Add(new YaoTribulationSystem());
        ModClass.I.GeneralLogicSystems.Add(new NirvanaSystem());

        // 妖丹神通的统一主动能力入口。
        ActiveAbilityService.Register(new YaoCoreActiveAbilityProvider());
    }

    /// <summary>判断当前身体是否拥有指定器官。</summary>
    private static bool HasOrgan(ActorExtend actor, string organId)
    {
        return TryGetOrganRank(actor, organId, out _);
    }

    /// <summary>读取当前身体上指定器官的等级。</summary>
    private static bool TryGetOrganRank(ActorExtend actor, string organId, out int rank)
    {
        rank = 0;
        if (!actor.E.TryGetComponent(out YaoBody body) ||
            !body.TryGetActiveForm(out YaoFormRecord form))
            return false;
        foreach (YaoOrganRecord organ in form.Organs)
        {
            if (!string.Equals(organ.OrganId, organId, System.StringComparison.Ordinal)) continue;
            rank = organ.Rank;
            return true;
        }

        return false;
    }
}
