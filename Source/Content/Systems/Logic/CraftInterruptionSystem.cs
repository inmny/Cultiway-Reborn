using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Crafting;
using Cultiway.Core.Components;
using Cultiway.Core.ControlledTasks;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>将失去任务和订单所有权的半成品转为不可恢复的废品。</summary>
public sealed class CraftInterruptionSystem : QuerySystem<CraftingElixir>
{
    private readonly ArchetypeQuery<CraftingArtifact> artifactQuery;
    private readonly List<Entity> interruptedItems = new();

    public CraftInterruptionSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
        var artifactFilter = new QueryFilter();
        artifactFilter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
        artifactQuery = world.Query<CraftingArtifact>(artifactFilter);
    }

    protected override void OnUpdate()
    {
        interruptedItems.Clear();
        Query.ForEachEntity((ref CraftingElixir _, Entity item) =>
        {
            if (IsInterrupted(item, CraftProcessType.Alchemy)) interruptedItems.Add(item);
        });
        artifactQuery.ForEachEntity((ref CraftingArtifact _, Entity item) =>
        {
            if (IsInterrupted(item, CraftProcessType.ArtifactRefining)) interruptedItems.Add(item);
        });

        for (int i = 0; i < interruptedItems.Count; i++)
            CraftFailureService.Fail(interruptedItems[i], CraftFailureReason.Interrupted);
    }

    private static bool IsInterrupted(Entity item, CraftProcessType process)
    {
        if (!item.TryGetComponent(out CraftSession session) || session.process != process) return true;
        foreach (Entity owner in item.GetIncomingLinks<InventoryRelation>().Entities)
        {
            if (!owner.TryGetComponent(out ActorBinder binder)) return true;
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive() || actor.getID() != session.actor_id) return true;

            bool correctTask = process switch
            {
                CraftProcessType.Alchemy => ReferenceEquals(actor.ai.task, ActorTasks.CraftElixir),
                CraftProcessType.ArtifactRefining => ReferenceEquals(actor.ai.task, ActorTasks.CraftArtifact),
                _ => false,
            };
            if (!correctTask) return true;
            if (session.order_id <= 0) return false;
            return !ControlledTaskOrderService.TryGetActiveOrderId(actor.getID(), out long orderId) ||
                   orderId != session.order_id;
        }
        return true;
    }
}
