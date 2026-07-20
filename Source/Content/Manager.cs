using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.ActorComponents;
using Cultiway.Content.ActiveAbilities;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;
using Cultiway.Content.Systems.Logic;
using Cultiway.Content.Systems.Render;
using Cultiway.Core;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SkillLibV3.ActiveAbilities;

namespace Cultiway.Content;

internal class Manager
{
    private List<ICanInit> libraries = new();
    internal static PathFinder FlyerPathFinder { get; } = new();

    public void Init()
    {
        Libraries.Manager.Init();
        FlyerPathFinder.UseGenerator(new PassthroughPathGenerator());

        var ns = GetType().Namespace;
        var library_ts = ModClass.A.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith(ns) &&
                        t.GetInterfaces().Contains(typeof(ICanInit))).ToList();
        library_ts = DependencyAttribute.SortManagerTypes(library_ts);
        foreach (var t in library_ts)
        {
            var library = Activator.CreateInstance(t) as ICanInit;

            if (library == null) 
            {
                ModClass.LogError($"({nameof(Content)}) failed to create instance of {t}");
                continue;
            }
            libraries.Add(library);
        }
        foreach (var library in libraries)
        {
            try
            {
                library.Init();
                ModClass.LogInfo($"({nameof(Content)}) initializes {library.GetType().Name}");
            }
            catch (Exception e)
            {
                ModClass.LogError($"({nameof(Content)}) failed to initialize {library.GetType().Name}\n{e.Message}\n{e.StackTrace}");
            }
        }

        // Content 的语义扩展已经全部注册，此时统一解析别名、父级和蕴含关系。
        ModClass.L.SemanticLibrary.LinkAndValidate();

        new Patch.Manager().Init();
        ModClass.I.GeneralLogicSystems.Add(new FlyCancelSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactVehicleFlightSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactVehiclePassengerSystem());
        ModClass.I.LogicRestoreStatusSystemGroup.Add(new RestoreWakanSystem());
        ModClass.I.LogicRestoreStatusSystemGroup.Add(new RestoreMagicResourceSystem());
        ModClass.I.LogicRestoreStatusSystemGroup.Add(new KnightAcquisitionSystem());
        ModClass.I.LogicRestoreStatusSystemGroup.Add(new KnightBreakthroughSystem());
        ModClass.I.GeneralLogicSystems.Add(new WakanSpreadSystem());
        ModClass.I.GeneralLogicSystems.Add(new TrainTrackRepairSystem());
        ModClass.I.GeneralLogicSystems.Add(new TrainTransportSystem());
        ModClass.I.GeneralLogicSystems.Add(new TeleportArraySystem());
        ModClass.I.GeneralLogicSystems.Add(new CityDistributeItemsSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactEquipmentSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactAbilityLifecycleSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactSectInstallationSystem());
        ArtifactSummonService.Init();
        ArtifactSpiritService.Init();
        ModClass.I.GeneralLogicSystems.Add(new ArtifactSummonSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactSpiritSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactSpiritAvatarCleanupSystem());
        ModClass.I.GeneralLogicSystems.Add(new ArtifactManifestationCleanupSystem());
        ModClass.I.GeneralLogicSystems.Add(new ContinuousCultivateSystem());
        ModClass.I.GeneralLogicSystems.Add(new SectConstructionSystem());
        ActorExtend.RegisterActionOnDeath(SectTreasureService.ReturnBorrowedOnDeath);
        ArtifactAbilityRuntimeBridge.Init();
        CoreFormationEffectRuntimeBridge.Init();
        ActiveAbilityService.Register(new CoreFormationActiveAbilityProvider());
        ModClass.I.GeneralLogicSystems.Add(new CoreFormationEffectSystem());
        ModClass.I.GeneralRenderSystems.Add(new BreakthroughVisualSystem());
        ModClass.I.GeneralRenderSystems.Add(new CloudRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new RealmAuraRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new RealmElementParticleRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new RealmIndicatorRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new ArtifactManifestationSystem());
        ModClass.I.GeneralRenderSystems.Add(new ArtifactSectManifestationSystem());
        ModClass.I.GeneralRenderSystems.Add(new ArtifactWorldRenderSystem());
        ModClass.I.GeneralRenderSystems.Add(new ArtifactAbilityVisualSystem());
        ModClass.I.GeneralRenderSystems.Add(new CoreFormationEffectVisualSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new CultibookGeneratedEventSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new CultibookImprovedEventSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new ElixirEffectGeneratedEventSystem());
        ModClass.I.LogicEventProcessSystemGroup.Add(new MagicSpellCastCompletedEventSystem());
        
        CultivateMethodTriggers.Init();
        KnightCombatTriggers.Init();
        KnightBloodline.Init();
        Train.Init();
    }

    public void OnReload()
    {
        foreach (var l in libraries)
        {
            if (l is not ICanReload lr)
                continue;
            lr.OnReload();
        }
    }
}
