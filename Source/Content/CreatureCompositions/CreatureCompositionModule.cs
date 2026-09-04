using Cultiway.Abstract;
using Cultiway.Content.CreatureCompositions.ActiveAbilities;
using Cultiway.Content.CreatureCompositions.Combat;
using Cultiway.Content.CreatureCompositions.Presentation;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Content.Systems.Logic;
using Cultiway.Content.Systems.Render;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Patch;

namespace Cultiway.Content.CreatureCompositions;

/// <summary>接通组合生灵共用身体框架：整理缓存、属性与语义贡献、主动能力与被动效果入口。</summary>
public sealed class CreatureCompositionModule : ICanInit
{
    public void Init()
    {
        CreaturePhenotypeCompiler.Initialize();
        PatchMapBox.RegisterActionOnClearWorld(CreatureOrganSkillRegistry.ClearWorldState);
        PatchMapBox.RegisterActionOnClearWorld(CreatureConsequenceQueue.ClearWorldState);
        PatchMapBox.RegisterActionOnClearWorld(CreatureOverlayRenderService.ClearWorldState);

        ActorExtend.RegisterCachedStatsBuilder(CreaturePhenotypeStatsContributor.Contribute);
        SemanticContributorService.Register(new CreaturePhenotypeSemanticContributor());
        CreatureOrganEffectDispatcher.Initialize();

        var provider = new CreaturePhenotypeActiveAbilityProvider();
        ActiveAbilityService.Register(provider);
        SourceGrantedSkillService.Register(provider);

        ModClass.I.GeneralLogicSystems.Add(new CreatureConsequenceSystem());
        ModClass.I.GeneralRenderSystems.Add(new CreatureOverlayRenderSystem());
    }
}
