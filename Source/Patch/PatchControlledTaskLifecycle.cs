using Cultiway.Core.ControlledTasks;
using HarmonyLib;
using ActorAiSystemBase = AiSystem<Actor, ActorJob, ai.behaviours.BehaviourTaskActor,
    ai.behaviours.BehaviourActionActor, BehaviourActorCondition>;

namespace Cultiway.Patch;

/// <summary>在任务被清空前区分行为序列自然完成、提前停止和外部中断。</summary>
internal static class PatchControlledTaskLifecycle
{
    [HarmonyPrefix, HarmonyPatch(typeof(ActorAiSystemBase), nameof(ActorAiSystemBase.setTaskBehFinished))]
    private static void setTaskBehFinished_prefix(ActorAiSystemBase __instance)
    {
        if (__instance is not AiSystemActor actorAiSystem) return;
        bool completedTaskSequence = actorAiSystem.task != null &&
                                     actorAiSystem.action_index >= actorAiSystem.task.list.Count;
        ControlledTaskOrderService.NotifyTaskFinishing(actorAiSystem, completedTaskSequence);
    }
}
