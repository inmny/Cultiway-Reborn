using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Crafting;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 将已经离开对应炼制任务的半成品转为不可恢复的废品。
/// </summary>
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
        {
            CraftFailureService.Fail(interruptedItems[i], CraftFailureReason.Interrupted);
        }
    }

    private static bool IsInterrupted(Entity item, CraftProcessType process)
    {
        foreach (Entity owner in item.GetIncomingLinks<InventoryRelation>().Entities)
        {
            if (!owner.TryGetComponent(out ActorBinder binder)) return true;

            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive()) return false;
            var task = actor.ai.task;
            return process switch
            {
                CraftProcessType.Alchemy =>
                    !ReferenceEquals(task, ActorTasks.CraftElixir) &&
                    !ReferenceEquals(task, ActorTasks.FindNewElixir),
                CraftProcessType.ArtifactRefining => !ReferenceEquals(task, ActorTasks.CraftArtifact),
                _ => true,
            };
        }

        return true;
    }
}
