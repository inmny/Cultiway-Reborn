using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Const;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Performance;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchFramePriorityScheduler
{
    private static bool pendingAutoSave;
    private static bool pendingAutoSaveSkipDelete;
    private static bool pendingAutoSaveForce;
    private static bool bypassAutoSaveDeferral;
    private static bool ensuringSaveBoundary;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapBox), "Update")]
    private static void BeforeMapBoxUpdate(out long __state)
    {
        __state = FramePriorityGovernor.StartHostMeasurement();
        if (Config.game_loaded &&
            !SmoothLoader.isLoading() &&
            CooperativeSimulationRunner.Instance.RequiresControl)
        {
            // 模拟可以跨帧，但动画时钟必须跟随渲染帧连续推进。
            AnimationHelper.updateTime(Time.unscaledDeltaTime, Time.unscaledDeltaTime);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapBox), "Update")]
    private static void AfterMapBoxUpdate(long __state)
    {
        FramePriorityGovernor.EndHostMeasurement(__state);
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

        bool initializationPending = IsWorldInitializationPending();
        if (initializationPending && !runner.Active)
        {
            return false;
        }

        if (!runner.RequiresControl)
        {
            return true;
        }

        try
        {
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
        return ModClass.I?.TileExtendManager?.IsWorldInitializationPending == true ||
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
        SimulationTime.UnbindWorld();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapBox), nameof(MapBox.finishMakingWorld))]
    private static void ResetAfterWorldCreation()
    {
        pendingAutoSave = false;
        CooperativeSimulationRunner.Instance.Abort();
        ModClass.I?.AbortPerformanceSchedulers();
        FramePriorityGovernor.ResetFault();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActorManager), nameof(ActorManager.calculateVisibleActors))]
    private static void PreparePresentationFrame()
    {
        PresentationInterpolator.PrepareFrame();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.updatePos))]
    private static void SmoothVisibleActorPresentation(Actor __instance, ref Vector3 __result)
    {
        PresentationInterpolator.Apply(__instance, ref __result);
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
