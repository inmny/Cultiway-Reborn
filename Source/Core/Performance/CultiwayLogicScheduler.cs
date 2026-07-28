using System.Collections.Generic;
using System.Globalization;
using Cultiway.Const;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Performance;

internal sealed class CultiwayLogicScheduler
{
    private readonly CooperativeSystemRootRunner rootRunner = new();
    private readonly List<SystemGroup> roots = new(4);
    private int rootIndex;
    private UpdateTick cycleTick;
    private bool initializationOnly;
    private long runFrameCalls;
    private long executedSteps;
    private long budgetDenials;
    private long forcedGroupCommits;

    public bool Active { get; private set; }
    public bool IsAtGroupCommitBoundary =>
        Active && rootRunner.IsAtGroupCommitBoundary;

    public void RunFrame(UpdateTick requestedTick, bool requestCycle)
    {
        RunFrameCore(requestedTick, requestCycle, false);
    }

    public void RunInitializationFrame(UpdateTick requestedTick)
    {
        RunFrameCore(requestedTick, true, true);
    }

    private void RunFrameCore(
        UpdateTick requestedTick,
        bool requestCycle,
        bool requestInitializationCycle)
    {
        runFrameCalls++;
        if (!Active && requestCycle)
        {
            const string startPhase = "cultiway.cycle.start";
            if (!FramePriorityGovernor.CanRun(SimulationDomain.Cultiway, startPhase))
            {
                return;
            }

            FramePriorityGovernor.RunPhase(
                SimulationDomain.Cultiway,
                startPhase,
                () =>
                {
                    if (requestInitializationCycle)
                    {
                        StartInitializationCycle(requestedTick);
                    }
                    else
                    {
                        StartCycle(requestedTick);
                    }
                });
        }

        while (Active)
        {
            string phase = rootRunner.GetNextPhaseName();
            bool canRun = FramePriorityGovernor.CanRun(
                SimulationDomain.Cultiway,
                phase);
            bool forceBoundaryCommit = !canRun && IsAtGroupCommitBoundary;
            if (!canRun && !forceBoundaryCommit)
            {
                budgetDenials++;
                FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, phase);
                break;
            }

            if (forceBoundaryCommit)
            {
                forcedGroupCommits++;
            }

            FramePriorityGovernor.RunPhase(
                SimulationDomain.Cultiway,
                phase,
                () => Step());
            if (forceBoundaryCommit)
            {
                // 系统组提交包含命令缓冲回放，不能拆开；超预算时每帧最多强制一次。
                break;
            }
        }
    }

    public string GetNextPhaseName()
    {
        return rootRunner.GetNextPhaseName();
    }

    public string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "active={0} mode={8} root={1}/{2} calls={3} steps={4} denied={5} forced={6} {7}",
            Active,
            rootIndex,
            roots.Count,
            runFrameCalls,
            executedSteps,
            budgetDenials,
            forcedGroupCommits,
            rootRunner.GetDiagnostics(),
            initializationOnly ? "initialization" : "full");
    }

    public bool Step()
    {
        if (!Active)
        {
            return true;
        }

        ExecuteStep();
        return !Active;
    }

    public void Abort()
    {
        rootRunner.Abort();
        roots.Clear();
        rootIndex = 0;
        Active = false;
        initializationOnly = false;
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    public void DrainToBoundary()
    {
        while (Active)
        {
            ExecuteStep();
        }
    }

    public void StartCycle(UpdateTick tick)
    {
        cycleTick = tick;
        initializationOnly = false;
        roots.Clear();
        roots.Add(ModClass.I.GeneralLogicSystems);
        roots.Add(ModClass.I.TileLogicSystems);

        if (ModClass.I.TileExtendManager.Ready())
        {
            bool geoRegionReady =
                !ModClass.I.TileExtendManager.IsWorldInitializationPending &&
                WorldboxGame.I?.GeoRegions?.IsMembershipReady == true;
            if (geoRegionReady && GeneralSettings.EnableGeoSystems)
            {
                roots.Add(ModClass.I.Geo.LogicSystemRoot);
            }

            roots.Add(ModClass.I.Geo.BasicSystemRoot);
        }

        StartRoots();
    }

    private void StartInitializationCycle(UpdateTick tick)
    {
        cycleTick = tick;
        initializationOnly = true;
        roots.Clear();
        roots.Add(ModClass.I.LogicEventProcessSystemGroup);
        StartRoots();
    }

    private void StartRoots()
    {
        rootIndex = 0;
        Active = roots.Count > 0;
        if (Active)
        {
            rootRunner.Start(roots[rootIndex], cycleTick);
            FramePriorityGovernor.RecordCultiwayCycleStarted();
        }
    }

    private void AdvanceRoot()
    {
        rootIndex++;
        if (rootIndex < roots.Count)
        {
            rootRunner.Start(roots[rootIndex], cycleTick);
            return;
        }

        roots.Clear();
        rootIndex = 0;
        Active = false;
        initializationOnly = false;
        FramePriorityGovernor.RecordCultiwayCycleCompleted();
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    private void ExecuteStep()
    {
        executedSteps++;
        MapBox map = World.world;
        bool currentPaused = map._is_paused;
        map._is_paused = false;
        try
        {
            if (rootRunner.Step())
            {
                AdvanceRoot();
            }
        }
        finally
        {
            map._is_paused = currentPaused;
        }
    }
}
