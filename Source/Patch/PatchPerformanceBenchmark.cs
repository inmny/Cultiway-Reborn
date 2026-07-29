using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ai.behaviours;
using Cultiway.Core.Performance;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchPerformanceBenchmark
{
    private const string AiTasksGroupId = "ai_tasks";
    private const string AiTasksTotalGroupId = "ai_tasks_total";
    private const string AiActionsGroupId = "ai_actions";
    private const string AiActionsTotalGroupId = "ai_actions_total";

    private static readonly HashSet<BehaviourActionActor> SeenActions =
        new(ReferenceComparer<BehaviourActionActor>.Instance);
    private static readonly Dictionary<string, AiMetric> ActionMetrics =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<RateCounter, ActionCounterInfo> ActionPerformanceCounters =
        new(ReferenceComparer<RateCounter>.Instance);
    private static bool actionCountersMapped;

    internal static void FlushAiMetricsForReport()
    {
        if (!SimulationTickBenchmark.ShouldCollectAiDetails ||
            AssetManager.tasks_actor == null)
        {
            return;
        }

        if (!actionCountersMapped)
        {
            RegisterActionCounters();
        }

        FinishTaskMetrics();
        FinishActionMetrics();
    }

    [HarmonyPrefix, HarmonyPatch(typeof(MapBox), "Update")]
    private static void MapBoxUpdatePrefix()
    {
        SimulationTickBenchmark.ApplyAiDetailsPolicy();
        if (!SimulationTickBenchmark.ShouldCollectAiDetails)
        {
            if (actionCountersMapped)
            {
                ActionPerformanceCounters.Clear();
                actionCountersMapped = false;
            }

            return;
        }

        if (!actionCountersMapped)
        {
            RegisterActionCounters();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(RateCounter),
        nameof(RateCounter.registerEvent),
        new Type[] { })]
    private static bool RateCounterRegisterEventPrefix(RateCounter __instance)
    {
        if (SimulationTickBenchmark.IsCollectingAiDetails)
        {
            RegisterEvent(__instance, 0.0);
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(RateCounter),
        nameof(RateCounter.registerEvent),
        new[] { typeof(double) })]
    private static bool RateCounterRegisterValuePrefix(
        RateCounter __instance,
        double pValue)
    {
        if (SimulationTickBenchmark.IsCollectingAiDetails)
        {
            RegisterEvent(__instance, pValue);
            if (ActionPerformanceCounters.TryGetValue(__instance, out ActionCounterInfo action))
            {
                GoToProfiler.CompleteAction(action.Kind, action.Id, pValue);
            }
        }

        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Bench), "finishSplitBenchmarkGroupAI")]
    private static bool FinishSplitBenchmarkGroupAiPrefix()
    {
        FlushAiMetricsForReport();
        return false;
    }

    private static void RegisterEvent(RateCounter counter, double value)
    {
        counter._timestamps.Add(new RateCounterData(
            World.world.getCurWorldTime(),
            value));
        counter._total++;
    }

    private static void FinishTaskMetrics()
    {
        double totalSeconds = 0.0;
        int totalCalls = 0;
        List<BehaviourTaskActor> tasks = AssetManager.tasks_actor.list;
        for (int i = 0; i < tasks.Count; i++)
        {
            BehaviourTaskActor task = tasks[i];
            AiMetric metric = ReadMetric(task);
            totalSeconds += metric.Seconds;
            totalCalls += metric.Calls;
            SaveMetric(task.id, metric, AiTasksGroupId);
        }

        SaveMetric(
            AiTasksGroupId,
            new AiMetric(totalSeconds, totalCalls),
            AiTasksTotalGroupId);
    }

    private static void FinishActionMetrics()
    {
        SeenActions.Clear();
        ActionMetrics.Clear();
        List<BehaviourTaskActor> tasks = AssetManager.tasks_actor.list;
        for (int i = 0; i < tasks.Count; i++)
        {
            List<BehaviourActionActor> actions = tasks[i].list;
            for (int j = 0; j < actions.Count; j++)
            {
                BehaviourActionActor action = actions[j];
                if (action == null || !SeenActions.Add(action))
                {
                    continue;
                }

                RegisterActionCounter(action);
                AiMetric metric = ReadMetric(action);
                if (ActionMetrics.TryGetValue(action.id, out AiMetric current))
                {
                    ActionMetrics[action.id] = current.Add(metric);
                }
                else
                {
                    ActionMetrics.Add(action.id, metric);
                }
            }
        }

        double totalSeconds = 0.0;
        int totalCalls = 0;
        foreach (KeyValuePair<string, AiMetric> entry in ActionMetrics)
        {
            totalSeconds += entry.Value.Seconds;
            totalCalls += entry.Value.Calls;
            SaveMetric(entry.Key, entry.Value, AiActionsGroupId);
        }

        SaveMetric(
            AiActionsGroupId,
            new AiMetric(totalSeconds, totalCalls),
            AiActionsTotalGroupId);
    }

    private static void RegisterActionCounters()
    {
        if (AssetManager.tasks_actor == null)
        {
            return;
        }

        List<BehaviourTaskActor> tasks = AssetManager.tasks_actor.list;
        for (int i = 0; i < tasks.Count; i++)
        {
            List<BehaviourActionActor> actions = tasks[i].list;
            for (int j = 0; j < actions.Count; j++)
            {
                RegisterActionCounter(actions[j]);
            }
        }

        actionCountersMapped = ActionPerformanceCounters.Count > 0;
    }

    private static void RegisterActionCounter(BehaviourActionActor action)
    {
        if (action?.rate_counter_performance == null)
        {
            return;
        }

        ActionPerformanceCounters[action.rate_counter_performance] =
            new ActionCounterInfo(action.id, GoToProfiler.Classify(action));
        actionCountersMapped = true;
    }

    private static AiMetric ReadMetric(BehaviourElementAI element)
    {
        if (element.rate_counter_calls == null ||
            element.rate_counter_performance == null)
        {
            return default;
        }

        int calls = element.rate_counter_calls.getEventsPerTick();
        element.rate_counter_performance.getEventsPerTick();
        return new AiMetric(
            element.rate_counter_performance.getValuesAll(),
            calls);
    }

    private static void SaveMetric(
        string id,
        AiMetric metric,
        string groupId)
    {
        Bench.benchSave(id, metric.Seconds, metric.Calls, groupId);
        Bench.saveAverageCounter(id, groupId);
    }

    private readonly struct AiMetric
    {
        internal AiMetric(double seconds, int calls)
        {
            Seconds = seconds;
            Calls = calls;
        }

        internal double Seconds { get; }
        internal int Calls { get; }

        internal AiMetric Add(AiMetric other)
        {
            return new AiMetric(Seconds + other.Seconds, Calls + other.Calls);
        }
    }

    private readonly struct ActionCounterInfo
    {
        internal ActionCounterInfo(string id, GoToActionKind kind)
        {
            Id = id;
            Kind = kind;
        }

        internal string Id { get; }
        internal GoToActionKind Kind { get; }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        internal static ReferenceComparer<T> Instance { get; } = new();

        public bool Equals(T left, T right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }
    }
}
