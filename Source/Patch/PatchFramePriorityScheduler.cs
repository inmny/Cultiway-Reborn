using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Const;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.Performance;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchFramePriorityScheduler
{
    private struct MapBoxUpdateScope
    {
        internal long HostMeasurement;
        internal bool Closed;
    }

    private static bool pendingAutoSave;
    private static bool pendingAutoSaveSkipDelete;
    private static bool pendingAutoSaveForce;
    private static bool bypassAutoSaveDeferral;
    private static bool ensuringSaveBoundary;
    private static long initializationGateChecks;
    private static long tileInitializationBlocks;
    private static long geoInitializationBlocks;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapBox), "Update")]
    private static void BeforeMapBoxUpdate(
        out MapBoxUpdateScope __state)
    {
        EnsureActorReadBoundary("mapbox.frame_begin");
        EnsureBuildingReadBoundary("mapbox.frame_begin");
        CooperativeActorParallelJobRunner
            .RefreshFrameVisibility();
        PathFinder.Instance.ApplyWorkerWakeups();
        PresentationCommandQueue.DrainMainThread();
        __state = new MapBoxUpdateScope
        {
            HostMeasurement =
                FramePriorityGovernor.StartHostMeasurement()
        };
        if (Config.game_loaded &&
            !SmoothLoader.isLoading() &&
            CooperativeSimulationRunner.Instance.RequiresControl)
        {
            ActorPresentationSnapshots.RequestCapture();
            // 模拟可以跨帧，但动画时钟必须跟随渲染帧连续推进。
            AnimationHelper.updateTime(Time.unscaledDeltaTime, Time.unscaledDeltaTime);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapBox), "Update")]
    private static void AfterMapBoxUpdate(
        MapBox __instance,
        ref MapBoxUpdateScope __state)
    {
        try
        {
            CooperativeSimulationRunner.Instance.FinishPresentationFrame();
        }
        catch (Exception exception)
        {
            HandleBackgroundSimulationFault(exception);
        }
        finally
        {
            FramePriorityGovernor.EndHostMeasurement(
                __state.HostMeasurement);
            __state.Closed = true;
            WorldTimeRateTracker.Update(__instance);
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(MapBox), "Update")]
    private static Exception FinalizeFailedMapBoxUpdate(
        Exception __exception,
        ref MapBoxUpdateScope __state)
    {
        if (__exception == null)
        {
            return null;
        }

        try
        {
            CooperativeSimulationRunner.Instance
                .FinishPresentationFrame();
        }
        catch (Exception boundaryException)
        {
            ModClass.LogErrorConcurrent(
                "[FramePriority] MapBox.Update 异常后的后台提交也失败: " +
                boundaryException);
        }
        finally
        {
            CooperativeSimulationRunner.Instance.Abort();
            ModClass.I?.AbortPerformanceSchedulers();
            PresentationCommandQueue.Clear();
            if (!__state.Closed)
            {
                FramePriorityGovernor.EndHostMeasurement(
                    __state.HostMeasurement);
                __state.Closed = true;
            }
        }

        FramePriorityGovernor.MarkFault(__exception);
        Config.paused = true;
        ModClass.LogErrorConcurrent(
            "[FramePriority] MapBox.Update 失败，已终止后台模拟并暂停: " +
            __exception);
        return __exception;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapBox), nameof(MapBox.checkMainSimulationUpdate))]
    private static bool TakeOverMainSimulation(MapBox __instance)
    {
        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        if (SmoothLoader.isLoading())
        {
            if (runner.Active)
            {
                runner.Abort();
            }
            return false;
        }

        if (!runner.RequiresControl)
        {
            return true;
        }

        try
        {
            CultiwayLogicScheduler logicScheduler = ModClass.I.LogicScheduler;
            if (logicScheduler.Active &&
                !runner.OwnsCultiwayCycle)
            {
                // 已接纳的 Cultiway 周期必须在帧首预算内续跑，否则初始化周期会与正式模拟互相等待。
                logicScheduler.RunFrame(default, false);
                if (logicScheduler.Active)
                {
                    return false;
                }
            }

            bool initializationPending = IsWorldInitializationPending();
            if (initializationPending && !runner.Active)
            {
                var initializationTick = new Friflo.Engine.ECS.UpdateTick(
                    0f,
                    SimulationTime.NowFloat);
                logicScheduler.RunInitializationFrame(initializationTick);
                initializationPending = IsWorldInitializationPending();
                if (logicScheduler.Active || initializationPending)
                {
                    return false;
                }
            }

            // 初始化请求出现时，先完整提交已经接纳的 tick，但不再接纳下一 tick。
            runner.RunFrame(__instance, !initializationPending);
        }
        catch (Exception exception)
        {
            runner.Abort();
            FramePriorityGovernor.MarkFault(exception);
            Config.paused = true;
            ModClass.LogErrorConcurrent(
                "[FramePriority] 原版模拟调度失败，已暂停游戏以避免退回无预算模拟: " + exception);
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AutoSaveManager), nameof(AutoSaveManager.autoSave))]
    private static bool DeferAutoSaveUntilCycleBoundary(bool pSkipDelete, bool pForce)
    {
        if (bypassAutoSaveDeferral)
        {
            return true;
        }

        if (IsWorldInitializationPending())
        {
            QueueDeferredAutoSave(pSkipDelete, pForce);
            return false;
        }

        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        if (!runner.RequiresControl || runner.IsAtCycleBoundary)
        {
            pendingAutoSave = false;
            return true;
        }

        QueueDeferredAutoSave(pSkipDelete, pForce);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AutoSaveManager), nameof(AutoSaveManager.update))]
    private static void FlushDeferredAutoSaveAtCycleBoundary()
    {
        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        if (!pendingAutoSave ||
            !runner.IsAtCycleBoundary ||
            IsWorldInitializationPending())
        {
            return;
        }

        bool skipDelete = pendingAutoSaveSkipDelete;
        bool force = pendingAutoSaveForce;
        pendingAutoSave = false;
        bypassAutoSaveDeferral = true;
        try
        {
            AutoSaveManager.autoSave(skipDelete, force);
        }
        finally
        {
            bypassAutoSaveDeferral = false;
        }
    }

    private static void QueueDeferredAutoSave(bool skipDelete, bool force)
    {
        if (!pendingAutoSave)
        {
            pendingAutoSaveSkipDelete = skipDelete;
            pendingAutoSaveForce = force;
        }
        else
        {
            pendingAutoSaveSkipDelete &= skipDelete;
            pendingAutoSaveForce |= force;
        }

        pendingAutoSave = true;
    }

    private static bool IsWorldInitializationPending()
    {
        initializationGateChecks++;
        bool tilePending =
            ModClass.I?.TileExtendManager?.IsWorldInitializationPending == true;
        bool geoPending =
            WorldGeneratedPartitionGeoRegionsEventSystem.BlocksSimulation;
        if (tilePending)
        {
            tileInitializationBlocks++;
        }

        if (geoPending)
        {
            geoInitializationBlocks++;
        }

        return tilePending || geoPending;
    }

    internal static string GetInitializationDiagnostics()
    {
        return "checks=" + initializationGateChecks +
               " tile_blocks=" + tileInitializationBlocks +
               " geo_blocks=" + geoInitializationBlocks +
               " tile_now=" +
               (ModClass.I?.TileExtendManager?.IsWorldInitializationPending == true) +
               " geo_now=" +
               WorldGeneratedPartitionGeoRegionsEventSystem.BlocksSimulation;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DelayedActionsManager), nameof(DelayedActionsManager.update))]
    private static void SeparateDelayedActionClocks(ref float pElapsed)
    {
        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        if (!runner.IsAdvancingGameDelayedActions &&
            (runner.RequiresControl || runner.ControlledThisFrame))
        {
            // 游戏速度时间已经在每个固定步长 tick 内推进；这里仅保留真实时间部分。
            pElapsed = 0f;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.saveWorldToDirectory))]
    private static void FinishSimulationBeforeSave()
    {
        if (ensuringSaveBoundary)
        {
            return;
        }

        if (FramePriorityGovernor.IsExecutingSimulationPhase)
        {
            throw new InvalidOperationException("模拟阶段内部不能创建不完整存档");
        }

        ensuringSaveBoundary = true;
        try
        {
            WorldGeneratedPartitionGeoRegionsEventSystem.DrainPendingWork();
            CooperativeSimulationRunner.Instance.DrainToBoundary();
            ModClass.I?.DrainPerformanceSchedulers();
        }
        finally
        {
            ensuringSaveBoundary = false;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
    private static void ResetBeforeWorldClear()
    {
        pendingAutoSave = false;
        CooperativeSimulationRunner.Instance.Abort();
        ModClass.I?.AbortPerformanceSchedulers();
        ModClass.I?.TileExtendManager?.CancelFitNewWorld();
        WorldGeneratedPartitionGeoRegionsEventSystem.CancelPendingWork();
        ActorPresentationSnapshots.Reset();
        WorldObjectPresentationRenderer.Reset();
        PresentationCommandQueue.Clear();
        SimulationTime.UnbindWorld();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapBox), nameof(MapBox.finishMakingWorld))]
    private static void ResetAfterWorldCreation()
    {
        pendingAutoSave = false;
        CooperativeSimulationRunner.Instance.Abort();
        ModClass.I?.AbortPerformanceSchedulers();
        ActorPresentationSnapshots.Reset();
        WorldObjectPresentationRenderer.Reset();
        PresentationCommandQueue.Clear();
        FramePriorityGovernor.ResetFault();
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(BuildingManager),
        nameof(BuildingManager.calculateVisibleBuildings))]
    private static bool PrepareBuildingPresentationFrame(
        BuildingManager __instance)
    {
        ActorPresentationSnapshot snapshot =
            ActorPresentationSnapshots.AcquireLatest();
        return !WorldObjectPresentationRenderer.TryPrepareBuildings(
            __instance,
            snapshot);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActorManager), nameof(ActorManager.calculateVisibleActors))]
    private static bool PreparePresentationFrame(ActorManager __instance)
    {
        ActorPresentationSnapshot snapshot = ActorPresentationSnapshots.AcquireLatest();
        PresentationInterpolator.PrepareFrame();
        return !ActorPresentationRenderer.TryPrepare(__instance, snapshot);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnits")]
    private static void UseSnapshotUnitCount(out int __state)
    {
        try
        {
            CooperativeSimulationRunner.Instance
                .TryBeginActorPresentationOverlap();
        }
        catch (Exception exception)
        {
            HandleBackgroundSimulationFault(exception);
        }

        UseSnapshotBaseVisibleCount(out __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnits")]
    private static void RestoreUnitCount(int __state)
    {
        RestoreSnapshotBaseVisibleCount(__state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawBuildings")]
    private static void BeginBuildingPresentationOverlap()
    {
        try
        {
            CooperativeSimulationRunner.Instance
                .TryBeginBuildingPresentationOverlap();
        }
        catch (Exception exception)
        {
            HandleBackgroundSimulationFault(exception);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawStockpileResources")]
    private static bool DrawSnapshotBuildingStockpiles(
        QuantumSpriteAsset pAsset)
    {
        if (WorldObjectPresentationRenderer.TryDrawStockpileResources(
                pAsset))
        {
            return false;
        }

        EnsureBuildingReadBoundary("quantum.building_stockpiles");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawBuildingsLightWindows")]
    private static bool DrawSnapshotBuildingLightWindows(
        QuantumSpriteAsset pAsset)
    {
        if (WorldObjectPresentationRenderer.TryDrawBuildingLightWindows(
                pAsset))
        {
            return false;
        }

        EnsureBuildingReadBoundary("quantum.building_light_windows");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "checkBuildingLights")]
    private static bool DrawSnapshotBuildingLights(
        Building pBuilding,
        Color pColor)
    {
        if (WorldObjectPresentationRenderer.TryDrawBuildingLights(
                pBuilding,
                pColor))
        {
            return false;
        }

        EnsureBuildingReadBoundary("quantum.building_light_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawLightAreas")]
    private static bool DrawSnapshotLightAreas()
    {
        return !WorldObjectPresentationRenderer.TryDrawLightAreas();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawFires")]
    private static bool DrawSnapshotFires(QuantumSpriteAsset pAsset)
    {
        if (WorldObjectPresentationRenderer.TryDrawFires(pAsset))
        {
            return false;
        }

        EnsureLiveObjectReadBoundary("quantum.fire_tiles_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawProjectiles")]
    private static bool DrawSnapshotProjectiles(QuantumSpriteAsset pAsset)
    {
        return !WorldObjectPresentationRenderer.TryDrawProjectiles(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawProjectileShadows")]
    private static bool DrawSnapshotProjectileShadows(
        QuantumSpriteAsset pAsset)
    {
        return !WorldObjectPresentationRenderer.TryDrawProjectileShadows(
            pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawParabolicUnload")]
    private static bool DrawSnapshotResourceThrows(QuantumSpriteAsset pAsset)
    {
        return !WorldObjectPresentationRenderer.TryDrawResourceThrows(
            pAsset,
            shadows: false);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawThrowingItemsShadows")]
    private static bool DrawSnapshotResourceThrowShadows(
        QuantumSpriteAsset pAsset)
    {
        return !WorldObjectPresentationRenderer.TryDrawResourceThrows(
            pAsset,
            shadows: true);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitItems")]
    private static void UseSnapshotUnitItemCount(out int __state)
    {
        UseSnapshotBaseVisibleCount(out __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitItems")]
    private static void RestoreUnitItemCount(int __state)
    {
        RestoreSnapshotBaseVisibleCount(__state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawShadowsUnit")]
    private static void UseSnapshotUnitShadowCount(out int __state)
    {
        UseSnapshotBaseVisibleCount(out __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawShadowsUnit")]
    private static void RestoreUnitShadowCount(int __state)
    {
        RestoreSnapshotBaseVisibleCount(__state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitsAvatars")]
    private static bool DrawSnapshotUnitAvatars()
    {
        return !ActorPresentationOverlays.TryDrawAvatars();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawHealthbars")]
    private static bool DrawSnapshotHealthbars(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawHealthbars(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitHappinessIcons")]
    private static bool DrawSnapshotUnitHappinessIcons(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawHappinessIcons(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitTaskIcons")]
    private static bool DrawSnapshotUnitTaskIcons(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawTaskIcons(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitMetas")]
    private static bool DrawSnapshotUnitMetas(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawUnitMetas(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "checkUnitLight")]
    private static bool DrawSnapshotUnitLights(Actor pActor, Color pColor)
    {
        if (ActorPresentationOverlays.TryDrawUnitLights(pActor, pColor))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.unit_light_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitsEffectDamage")]
    private static bool DrawSnapshotActorDamageEffects(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawDamage(pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.actor_damage_effect_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitsEffectHighlight")]
    private static bool DrawSnapshotActorHighlightEffects(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawHighlights(pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.actor_highlight_effect_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawCursorAttackRecharge")]
    private static bool DrawSnapshotControlledActorRecharge(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawControlledRecharge(
                pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.controlled_actor_recharge_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawCursorTargetSubspecies")]
    private static bool DrawSnapshotCursorSubspeciesTarget(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawCursorSubspecies(
                pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.cursor_subspecies_target_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawPlots")]
    private static bool DrawSnapshotPlotActorIcons(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawPlots(pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.plot_actor_icons_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawPlotRemovals")]
    private static bool DrawSnapshotPlotActorRemovalIcons(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawPlotRemovals(
                pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.plot_actor_removals_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawMagnetUnits")]
    private static bool DrawSnapshotMagnetActorIcons(
        QuantumSpriteAsset pAsset)
    {
        if (ActorTransientPresentationFrame.TryDrawMagnetUnits(pAsset))
        {
            return false;
        }

        EnsureActorReadBoundary("quantum.magnet_units_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapBox), "updateDebugGroupSystem")]
    private static void GuardActorDebugRendering()
    {
        if (!RequiresLiveDebugTextBoundary())
        {
            return;
        }

        EnsureActorReadBoundary("mapbox.debug_render");
        EnsureBuildingReadBoundary("mapbox.debug_render");
    }

    private static bool RequiresLiveDebugTextBoundary()
    {
        return DebugConfig.isOn(DebugOption.OverlaySoundsAttached) ||
               DebugConfig.isOn(DebugOption.OverlayBoatTransport) ||
               DebugConfig.isOn(DebugOption.OverlayActorCivs) ||
               DebugConfig.isOn(DebugOption.OverlayCursorActor) ||
               DebugConfig.isOn(DebugOption.OverlayActorGroupLeaderOnly) ||
               DebugConfig.isOn(DebugOption.OverlayActorFavoritesOnly) ||
               DebugConfig.isOn(DebugOption.OverlayActorMobs) ||
               DebugConfig.isOn(DebugOption.OverlayTrees) ||
               DebugConfig.isOn(DebugOption.OverlayPlants) ||
               DebugConfig.isOn(DebugOption.OverlayCivBuildings) ||
               DebugConfig.isOn(DebugOption.OverlayOtherBuildings) ||
               DebugConfig.isOn(DebugOption.OverlayArmies) ||
               DebugConfig.isOn(DebugOption.OverlayCity) ||
               DebugConfig.isOn(DebugOption.OverlayCityTasks) ||
               DebugConfig.isOn(DebugOption.OverlayKingdom);
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(QuantumSpriteLibrary),
        "drawUnexploredAugmentationSprite")]
    private static bool DrawSnapshotUnexploredAugmentations(
        QuantumSpriteAsset pQAsset)
    {
        return !ActorPresentationOverlays.TryDrawUnexploredAugmentations(
            pQAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitBanners")]
    private static bool DrawSnapshotUnitBanners(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawBanners(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawFavoritesMap")]
    private static bool DrawSnapshotFavoritesMap(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawFavoritesMap(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawFavoritesGame")]
    private static bool DrawSnapshotFavoritesGame(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawFavoritesGame(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawSelectedUnits")]
    private static bool DrawSnapshotSelectedUnits(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawSelectedUnits(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawUnitsToBeSelectedBySquareTool")]
    private static bool DrawSnapshotSquareSelectionUnits(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawSquareSelectionUnits(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawSocialize")]
    private static bool DrawSnapshotSocialize(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawSocialize(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawJustAte")]
    private static bool DrawSnapshotJustAte(QuantumSpriteAsset pAsset)
    {
        return !ActorPresentationOverlays.TryDrawJustAte(pAsset);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawStatusEffects")]
    private static bool DrawSnapshotStatuses(
        QuantumSpriteAsset pAsset)
    {
        ActorPresentationSnapshot actorSnapshot =
            ActorPresentationRenderer.PreparedSnapshot;
        if (actorSnapshot != null &&
            ReferenceEquals(
                actorSnapshot,
                WorldObjectPresentationRenderer.PreparedSnapshot))
        {
            ActorPresentationOverlays.TryDrawStatuses(pAsset);
            WorldObjectPresentationRenderer.TryDrawBuildingStatuses(
                pAsset);
            return false;
        }

        EnsureActorReadBoundary("quantum.actor_status_fallback");
        EnsureBuildingReadBoundary("quantum.building_status_fallback");
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.updatePos))]
    private static bool UseSnapshotActorPresentationPosition(
        Actor __instance,
        ref Vector3 __result)
    {
        if (!PresentationInterpolator.TryApply(
                __instance,
                out Vector3 position))
        {
            return true;
        }

        __result = position;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.updateRotation))]
    private static bool UseSnapshotActorPresentationRotation(
        Actor __instance,
        ref Vector3 __result)
    {
        if (!ActorPresentationRenderer.TryGetPreparedSample(
                __instance,
                out ActorPresentationSample sample))
        {
            return true;
        }

        __result = sample.Rotation;
        return false;
    }

    private static void UseSnapshotBaseVisibleCount(out int previousCount)
    {
        ActorManager manager = World.world?.units;
        if (!ActorPresentationRenderer.TryUseBaseVisibleCount(
                manager,
                out previousCount))
        {
            previousCount = -1;
        }
    }

    private static void RestoreSnapshotBaseVisibleCount(int previousCount)
    {
        if (previousCount >= 0)
        {
            ActorPresentationRenderer.RestoreVisibleCount(
                World.world?.units,
                previousCount);
        }
    }

    private static void EnsureActorReadBoundary(string reason)
    {
        try
        {
            CooperativeSimulationRunner.Instance
                .EnsureActorReadBoundary(reason);
        }
        catch (Exception exception)
        {
            HandleBackgroundSimulationFault(exception);
        }
    }

    private static void EnsureBuildingReadBoundary(string reason)
    {
        try
        {
            CooperativeSimulationRunner.Instance
                .EnsureBuildingReadBoundary(reason);
        }
        catch (Exception exception)
        {
            HandleBackgroundSimulationFault(exception);
        }
    }

    internal static void EnsureLiveObjectReadBoundary(string reason)
    {
        EnsureActorReadBoundary(reason);
        EnsureBuildingReadBoundary(reason);
    }

    private static void HandleBackgroundSimulationFault(Exception exception)
    {
        CooperativeSimulationRunner.Instance.Abort();
        FramePriorityGovernor.MarkFault(exception);
        Config.paused = true;
        ModClass.LogErrorConcurrent(
            "[FramePriority] 后台模拟与表现边界失败，已暂停游戏: " +
            exception);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MapBox), "Update")]
    private static IEnumerable<CodeInstruction> KeepEveryRenderFrameAtHighSpeed(
        IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo renderSkipField = AccessTools.Field(
            typeof(WorldTimeScaleAsset),
            nameof(WorldTimeScaleAsset.render_skip));
        MethodInfo filterMethod = AccessTools.Method(
            typeof(PatchFramePriorityScheduler),
            nameof(FilterRenderSkip));
        int replacements = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, renderSkipField))
            {
                replacements++;
                yield return new CodeInstruction(OpCodes.Call, filterMethod);
            }
        }

        if (replacements == 0)
        {
            throw new InvalidOperationException("无法关闭 MapBox.Update 的高倍速跳帧");
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ActorManager), "fillVisibleObjects")]
    private static IEnumerable<CodeInstruction> RefreshVisibilityDuringRendering(
        IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo visibleField = AccessTools.Field(typeof(Actor), nameof(Actor.is_visible));
        MethodInfo refreshMethod = AccessTools.Method(
            typeof(PatchFramePriorityScheduler),
            nameof(GetPresentationVisibility));
        int replacements = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, visibleField))
            {
                yield return instruction;
                continue;
            }

            var replacement = new CodeInstruction(OpCodes.Call, refreshMethod);
            replacement.labels.AddRange(instruction.labels);
            replacement.blocks.AddRange(instruction.blocks);
            replacements++;
            yield return replacement;
        }

        if (replacements == 0)
        {
            throw new InvalidOperationException("无法接管 ActorManager.fillVisibleObjects 的可见性判断");
        }
    }

    private static bool GetPresentationVisibility(Actor actor)
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler)
        {
            return actor.is_visible;
        }

        bool visible;
        if (actor.isInMagnet() || actor.isInsideSomething())
        {
            visible = false;
        }
        else if (MapBox.isRenderGameplay())
        {
            visible = actor.current_tile.zone.visible;
        }
        else
        {
            visible = actor.asset.visible_on_minimap;
        }

        actor.is_visible = visible;
        return visible;
    }

    private static bool FilterRenderSkip(bool configuredRenderSkip)
    {
        CooperativeSimulationRunner runner = CooperativeSimulationRunner.Instance;
        return runner.RequiresControl || runner.ControlledThisFrame
            ? false
            : configuredRenderSkip;
    }

    public static void SpecialPatch()
    {
        MethodInfo criticalMethod = AccessTools.Method(typeof(MapBox), nameof(MapBox.checkMainSimulationUpdate));
        Patches patchInfo = Harmony.GetPatchInfo(criticalMethod);
        bool installed = patchInfo?.Prefixes.Any(patch => patch.owner == "inmny.cultiway") == true;
        if (!installed)
        {
            throw new InvalidOperationException("无法接管 MapBox.checkMainSimulationUpdate");
        }

        FramePriorityGovernor.MarkCriticalHookInstalled();
    }
}

/// <summary>
/// 原版调试 QuantumSprite 会直接遍历角色或建筑。调试项关闭时这些方法
/// 不会被调用；开启任一项时先提交后台写入，保证诊断代码看到一致对象。
/// </summary>
[HarmonyPatch]
internal static class PatchFramePriorityDebugRenderBoundary
{
    private static readonly string[] LiveObjectReaders =
    {
        "drawMoney",
        "drawUnitAttackRange",
        "drawUnitSize",
        "debugDrawArrowsUnitAttackTargets",
        "debugDrawArrowsUnitBehTarget",
        "debugDrawArrowsUnitNavigationTargets",
        "debugDrawArrowsUnitHeight",
        "debugDrawArrowsUnitNavigationPath",
        "debugDrawArrowsUnitNextStepTile",
        "debugDrawArrowsUnitNextStepPosition",
        "debugDrawArrowsUnitCurrentPosition",
        "debugDrawArrowsBoatPassengers",
        "debugDrawArrowsPassengerTaxiRequestTargets",
        "debugDrawArrowsBuildingResidents",
        "debugDrawArrowsLovers",
        "debugDrawFavoriteFoods",
        "debugDrawKingdomIcons",
        "debugDrawHoldingFoods",
        "debugDrawGodFingerTiles",
        "debugDrawDragonAttackTiles",
        "drawSwimTargets",
        "debugDrawDeadUnits",
        "debugCityZoneRange",
        "debugEnemyFinder"
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        for (int i = 0; i < LiveObjectReaders.Length; i++)
        {
            MethodInfo method = AccessTools.Method(
                typeof(QuantumSpriteLibrary),
                LiveObjectReaders[i],
                new[] { typeof(QuantumSpriteAsset) });
            if (method != null)
            {
                yield return method;
            }
        }
    }

    [HarmonyPrefix]
    private static void BeforeDebugLiveObjectRead()
    {
        PatchFramePriorityScheduler.EnsureLiveObjectReadBoundary(
            "quantum.debug_live_objects");
    }
}
