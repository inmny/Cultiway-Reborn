using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>把修炼、生产和组织领域事件映射为十八项修仙成就。</summary>
internal static class CultivationAchievementService
{
    private const string EnterFoundationTransition = "xian.enter_foundation";
    private const string FormJindanTransition = "xian.form_jindan";
    private const string RefineJindanTransition = "xian.refine_jindan";
    private const string FormYuanyingTransition = "xian.form_yuanying";

    private static bool initialized;

    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        CultivationAchievementUnlockService.Initialize();
        ProgressionLifecycle.RegisterCommitted(OnProgressionCommitted);
        ProductionLifecycle.RegisterCompleted(OnProductionCompleted);
        ActorExtend.RegisterActionOnKill(OnActorKilled);
    }

    internal static void OnXianAcquired(ActorExtend actor)
    {
        if (!CanEvaluate(actor) || !actor.HasElementRoot() || !actor.HasCultisys<Xian>()) return;
        Unlock(CultivationAchievementIds.RootAwakened);
    }

    internal static void OnArtifactSpiritAwakened(Entity artifact)
    {
        if (!CultivationAchievementUnlockService.CanProcessRuntimeEvent || !artifact.IsAvailable() ||
            !artifact.TryGetComponent(out ArtifactSpiritState state) || !state.awakened) return;
        Unlock(CultivationAchievementIds.ArtifactSpiritAwakened);
    }

    internal static void OnSectFounded(Sect sect)
    {
        if (!CanEvaluate(sect)) return;
        Unlock(CultivationAchievementIds.SectFounded);
    }

    internal static void OnSectMemberJoined(Sect sect)
    {
        if (!CanEvaluate(sect) || !IsGreatSect(sect, null)) return;
        Unlock(CultivationAchievementIds.GreatSect);
    }

    internal static void OnSectBuildingCompleted(Sect sect, Building building)
    {
        if (!CanEvaluate(sect) || !IsUsableSectBuilding(building) || !IsGreatSect(sect, building)) return;
        Unlock(CultivationAchievementIds.GreatSect);
    }

    internal static void OnApprenticeRecruited(ActorExtend master)
    {
        if (!CanEvaluate(master) || master.GetApprentices().Count < 5) return;
        Unlock(CultivationAchievementIds.FiveDisciples);
    }

    internal static void OnApprenticeGraduated(ActorExtend master, ActorExtend apprentice)
    {
        if (!CanEvaluate(master) || !CanEvaluate(apprentice)) return;
        Unlock(CultivationAchievementIds.ApprenticeGraduated);
    }

    private static void OnProgressionCommitted(ProgressionCommittedEvent evt)
    {
        if (!CanEvaluate(evt.Actor) || evt.Cultisys != Cultisyses.Xian ||
            evt.Mode == ProgressionMode.Synchronize) return;

        if (evt.Kind == ProgressionKind.Minor)
        {
            if (evt.TransitionId == RefineJindanTransition &&
                evt.Actor.TryGetComponent(out Jindan refinedJindan) && refinedJindan.stage >= 9)
                Unlock(CultivationAchievementIds.NinefoldCore);
            return;
        }

        switch (evt.TransitionId)
        {
            case EnterFoundationTransition:
                if (evt.Actor.TryGetComponent(out XianBase foundation) && foundation.formation.IsValid)
                    Unlock(CultivationAchievementIds.FoundationEstablished);
                break;
            case FormJindanTransition:
                CheckJindanAchievements(evt.Actor);
                break;
            case FormYuanyingTransition:
                CheckYuanyingAchievements(evt.Actor);
                break;
        }
    }

    private static void CheckJindanAchievements(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out Jindan jindan) || !jindan.formation.IsFinalized) return;

        Unlock(CultivationAchievementIds.GoldenCoreFormed);
        if (jindan.GetQuality().Stage == 3) Unlock(CultivationAchievementIds.HeavenGradeCore);
    }

    private static void CheckYuanyingAchievements(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out Yuanying yuanying) || !yuanying.formation.IsFinalized) return;

        if (yuanying.inherited_jindan_stage >= 9) Unlock(CultivationAchievementIds.NinefoldCore);
        Unlock(CultivationAchievementIds.NascentSoulFormed);
        if (HasFlawlessLineage(actor, yuanying)) Unlock(CultivationAchievementIds.FlawlessLineage);
        if (actor.HasElementRoot() && actor.GetElementRoot().Type == ModClass.L.ElementRootLibrary.Entropy)
            Unlock(CultivationAchievementIds.ChaosNascentSoul);
    }

    private static bool HasFlawlessLineage(ActorExtend actor, Yuanying yuanying)
    {
        if (yuanying.inherited_jindan_stage < 9 ||
            !actor.TryGetComponent(out QiRefinementState qi) ||
            !actor.TryGetComponent(out XianBase foundation) ||
            !actor.TryGetComponent(out Jindan jindan)) return false;

        return IsEarthOrBetter(qi.formation) &&
               IsEarthOrBetter(foundation.formation) &&
               IsEarthOrBetter(jindan.formation) &&
               IsEarthOrBetter(yuanying.formation);
    }

    private static bool IsEarthOrBetter(CoreFormationSnapshot formation)
    {
        return formation.IsFinalized && formation.quality.Stage >= 2;
    }

    private static void OnActorKilled(ActorExtend killer, Actor deadUnit, Kingdom _)
    {
        if (!CanEvaluate(killer) || deadUnit == null || deadUnit.data == null ||
            ReferenceEquals(killer.Base, deadUnit)) return;

        ActorExtend victim = deadUnit.GetExtend();
        if (!victim.HasCultisys<Xian>() || !killer.HasCultisys<Xian>()) return;
        if (victim.GetCultisys<Xian>().CurrLevel - killer.GetCultisys<Xian>().CurrLevel < 1) return;
        Unlock(CultivationAchievementIds.RealmDefier);
    }

    private static void OnProductionCompleted(ProductionCompletedEvent evt)
    {
        if (!CanEvaluate(evt.Producer) || !evt.Product.IsAvailable()) return;

        switch (evt.Process)
        {
            case ArtifactProductionProcesses.Alchemy when evt.Product.HasComponent<Elixir>():
                Unlock(CultivationAchievementIds.FirstElixir);
                if (evt.FinalLevel.Stage >= 2) Unlock(CultivationAchievementIds.EarthGradeElixir);
                break;
            case ArtifactProductionProcesses.ArtifactRefining when evt.Product.HasComponent<Artifact>():
                Unlock(CultivationAchievementIds.FirstArtifact);
                if (evt.FinalLevel.Stage >= 2) Unlock(CultivationAchievementIds.EarthGradeArtifact);
                break;
        }
    }

    private static bool IsGreatSect(Sect sect, Building completedBuilding)
    {
        if (sect.GetLivingMembers().Count < 20) return false;
        return HasUsableBuilding(sect, Buildings.SectHall.id, completedBuilding) &&
               HasUsableBuilding(sect, Buildings.SectScripturePavilion.id, completedBuilding) &&
               HasUsableBuilding(sect, Buildings.SectTreasurePavilion.id, completedBuilding);
    }

    private static bool HasUsableBuilding(Sect sect, string buildingId, Building completedBuilding)
    {
        if (IsUsableSectBuilding(completedBuilding) && completedBuilding.asset.id == buildingId) return true;
        return sect.CountBuildingsOfID(buildingId) > 0;
    }

    private static bool IsUsableSectBuilding(Building building)
    {
        return building != null && building.asset != null && !building.isRekt() &&
               building.isUsable() && !building.isUnderConstruction();
    }

    private static bool CanEvaluate(ActorExtend actor)
    {
        return CultivationAchievementUnlockService.CanProcessRuntimeEvent && actor?.Base != null &&
               actor.Base.data != null && !actor.Base.isRekt();
    }

    private static bool CanEvaluate(Sect sect)
    {
        return CultivationAchievementUnlockService.CanProcessRuntimeEvent && sect != null &&
               sect.data != null && !sect.isRekt();
    }

    private static void Unlock(string id)
    {
        CultivationAchievementUnlockService.TryUnlock(CultivationAchievements.Get(id));
    }
}
