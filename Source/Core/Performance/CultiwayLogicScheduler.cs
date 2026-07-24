using System.Collections.Generic;
using Cultiway.Const;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Performance;

internal sealed class CultiwayLogicScheduler
{
    private readonly CooperativeSystemRootRunner rootRunner = new();
    private readonly List<SystemRoot> roots = new(4);
    private int rootIndex;
    private UpdateTick cycleTick;

    public bool Active { get; private set; }

    public void RunFrame(UpdateTick requestedTick, bool requestCycle)
    {
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
                () => StartCycle(requestedTick));
        }

        while (Active)
        {
            string phase = rootRunner.GetNextPhaseName();
            if (!FramePriorityGovernor.CanRun(SimulationDomain.Cultiway, phase))
            {
                FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, phase);
                break;
            }

            FramePriorityGovernor.RunPhase(
                SimulationDomain.Cultiway,
                phase,
                () => Step());
        }
    }

    public string GetNextPhaseName()
    {
        return rootRunner.GetNextPhaseName();
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
        FramePriorityGovernor.RecordCultiwayCycleCompleted();
        FramePriorityGovernor.SetPhase(SimulationDomain.Cultiway, "idle");
    }

    private void ExecuteStep()
    {
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
