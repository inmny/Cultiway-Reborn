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

    /// <summary>只能在已经回退并确认的共同时间执行，不在预测中产生世界事实。</summary>
    /// <param name="running">已经真实推进的施放。</param><param name="clock">确认时点。</param>
    private static void ConfirmWorldMaterial(RunningCast running, double clock)
    {
        if (!running.Cast.Plan.HasWorldMaterials) return;
        long epoch = worldGeneration;
        running.Cast.TakeWorldMatterRequests(simulation.materialChanges);
        foreach (var request in simulation.materialChanges)
        {
            simulation.materialSelection.Clear();
            running.Cast.MaterialSelection(request.Node, simulation.materialSelection);
            if (request.Conduction != null)
            {
                bool confirming = simulation.confirmingContact;
                double time = simulation.contactTime;
                simulation.confirmingContact = true;
                simulation.contactTime = clock;
                try
                {
                    Conduct(running, request.Node, request.Conduction, clock);
                }
                finally
                {
                    simulation.confirmingContact = confirming;
                    simulation.contactTime = time;
                }

                if (epoch != worldGeneration) return;
                continue;
            }

            WorldMaterialService.ConvertMatter(running.Owner(request.Node).Id, simulation.materialSelection,
                request.Matter, request.Selection, clock, running.Id, running.Cast.Plan.Id);
            if (epoch != worldGeneration) return;
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
                    running.Resource,
                    running.Cast.Plan.Nodes[node].Residue.Lifetime, clock);
                running.Cast.CompleteLeave(node);
                WorldMaterialService.PublishAccepted(id, running.Id, running.Cast.Plan.Id);
                if (epoch != worldGeneration) return;
            }

            if (!running.Cast.HasWorldRecovery(node)) continue;
            running.Cast.TakeWorldRecovery(node);
            simulation.materialSelection.Clear();
            running.Cast.MaterialSelection(node, simulation.materialSelection);
            WorldMaterialService.Recover(running.Owner(node).Id, simulation.materialSelection,
                running.Cast.RecoveryFor(node), clock);
            if (epoch != worldGeneration) return;
        }
    }

    /// <summary>施放终止只释放未用额度，不删除已经交出的世界材料。</summary>
    /// <param name="running">真正结束的施放。</param>
    private static void ReleaseMaterialReservation(RunningCast running)
    {
        WorldMaterialService.Release(running.ReservedResidues, running.ReservedMaterialCells);
        running.ReservedResidues = running.ReservedMaterialCells = 0;
    }
}