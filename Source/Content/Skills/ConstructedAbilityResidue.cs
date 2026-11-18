using System.Collections.Generic;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>主线程把真实材料与空间选择交给唯一世界维护者。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>同一确认入口复用的物理选择缓冲。</summary>
    private readonly List<AbilityCapsule> materialSelection = new();

    /// <summary>一次确认复用的世界材料转换缓冲。</summary>
    private readonly List<AbilityWorldMatterRequest> materialChanges = new();
    /// <summary>本次确认明确指定的世界材料，不在空输入时退回范围重选。</summary>
    private readonly List<long> materialInputs = new();

    /// <summary>只能在已经回退并确认的共同时间执行，不在预测中产生世界事实。</summary>
    /// <param name="running">已经真实推进的施放。</param><param name="clock">确认时点。</param>
    private static void ConfirmWorldMaterial(RunningCast running, double clock)
    {
        if (!running.Cast.Plan.HasWorldMaterials) return;
        long epoch = worldGeneration;
        if (running.ErosionWatches != null)
            for (int i = running.ErosionWatches.Count - 1; i >= 0; i--)
            {
                var watch = running.ErosionWatches[i];
                if (watch.State == ConstructionStepState.Running) continue;
                if (watch.State == ConstructionStepState.Completed) running.Cast.ConfirmStepWorld(watch.Step, watch.Receipt);
                else running.Cast.InterruptStepWorld(watch.Step, watch.Receipt);
                if (epoch != worldGeneration) return;
                WorldMaterialService.ReleaseErosion(watch); running.ErosionWatches.RemoveAt(i);
            }
        running.Cast.TakeWorldMatterRequests(simulation.materialChanges);
        foreach (var request in simulation.materialChanges)
        {
            simulation.materialSelection.Clear();
            running.Cast.MaterialSelection(request.Node, simulation.materialSelection);
            var identities = running.Cast.ReadWorldMaterialInputs(request.Step, simulation.materialInputs) ? simulation.materialInputs : null;
            if (request.Erosion != null)
            {
                var watch = WorldMaterialService.Erode(running.Owner(request.Node).Id, simulation.materialSelection,
                    request.Erosion, clock, running.Id, running.Cast.Plan.Id, request.Step, identities);
                if (epoch != worldGeneration) return;
                if (watch.State == ConstructionStepState.Interrupted) running.Cast.InterruptStepWorld(request.Step, watch.Receipt);
                else
                {
                    (running.ErosionWatches ??= new List<WorldErosionWatch>()).Add(watch);
                    running.Cast.AwaitWorldErosion(request.Step);
                }
                continue;
            }
            if (request.Retiming != null)
            {
                var receipt = WorldMaterialService.RetimeErosion(running.Owner(request.Node).Id, simulation.materialSelection,
                    request.Retiming, clock, running.Id, running.Cast.Plan.Id, identities);
                if (epoch != worldGeneration) return;
                if (receipt.Materials.Count == 0) running.Cast.InterruptStepWorld(request.Step, receipt);
                else running.Cast.ConfirmStepWorld(request.Step, receipt);
                continue;
            }
            if (request.Conduction != null)
            {
                bool confirming = simulation.confirmingContact;
                double time = simulation.contactTime;
                simulation.confirmingContact = true;
                simulation.contactTime = clock;
                try
                {
                    var receipt = Conduct(running, request.Node, request.Conduction, clock, request.Pulse, identities, out var arrival);
                    if (epoch == worldGeneration && receipt != null)
                    {
                        running.Cast.ConfirmStepWorld(request.Step, receipt, request.Pulse >= request.Conduction.Pulses);
                        if (arrival.HasValue)
                            running.Cast.ConfirmNetworkArrival(request.Node, ConstructionChangeTrigger.WaterRouteArrival, arrival.Value);
                    }
                }
                finally
                {
                    simulation.confirmingContact = confirming;
                    simulation.contactTime = time;
                }

                if (epoch != worldGeneration) return;
                continue;
            }

            var converted = WorldMaterialService.ConvertMatter(running.Owner(request.Node).Id, simulation.materialSelection,
                request.Matter, request.Selection, clock, running.Id, running.Cast.Plan.Id, identities);
            if (epoch != worldGeneration) return;
            if (converted != null) running.Cast.ConfirmStepWorld(request.Step, converted);
        }

        for (int node = 0; node < running.Cast.Plan.FormCount; node++)
        {
            if (running.Cast.HasLeave(node))
            {
                AbilityMaterial material = running.Cast.CaptureMaterial(node);
                int expected = running.Cast.Plan.Nodes[node].FlowSettings?.Cells ?? 0;
                running.ReservedResidues--;
                running.ReservedMaterialCells -= expected * expected;
                WorldMaterialService.Release(1, expected * expected);
                long id = WorldMaterialService.Accept(material, running.SourceId, running.Owner(node).Id,
                    running.Funding,
                    running.Cast.Plan.Nodes[node].Residue.Lifetime, clock);
                running.Cast.ConfirmLeave(node, id, clock);
                WorldMaterialService.PublishAccepted(id, running.Id, running.Cast.Plan.Id);
                if (epoch != worldGeneration) return;
            }

            if (!running.Cast.HasWorldRecovery(node)) continue;
            running.Cast.TakeWorldRecovery(node);
            simulation.materialSelection.Clear();
            running.Cast.MaterialSelection(node, simulation.materialSelection);
            var selected = running.Cast.ReadRecoveryMaterials(node, simulation.materialInputs) ? simulation.materialInputs : null;
            WorldMaterialService.Recover(running.Owner(node).Id, simulation.materialSelection,
                running.Cast.RecoveryFor(node), clock, selected);
            if (epoch != worldGeneration) return;
        }
    }

    /// <summary>施放终止只释放未用额度，不删除已经交出的世界材料。</summary>
    /// <param name="running">真正结束的施放。</param>
    private static void ReleaseMaterialReservation(RunningCast running)
    {
        TrackWorldRecovery(running, false);
        if (running.ErosionWatches != null)
        {
            foreach (var watch in running.ErosionWatches) WorldMaterialService.ReleaseErosion(watch);
            running.ErosionWatches = null;
        }
        WorldMaterialService.Release(running.ReservedResidues, running.ReservedMaterialCells);
        running.ReservedResidues = running.ReservedMaterialCells = 0;
    }
}
