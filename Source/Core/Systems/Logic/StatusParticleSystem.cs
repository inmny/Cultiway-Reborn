using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Logic;

public class StatusParticleSystem : QuerySystem<StatusComponent, StatusParticleState>
{
    public StatusParticleSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var deltaTime = Tick.deltaTime;
        Query.ForEachEntity(((ref StatusComponent status, ref StatusParticleState state, Entity entity) =>
        {
            var settings = status.Type.ParticleSettings;
            if (!settings.enabled || settings.count <= 0)
            {
                return;
            }

            state.timer += deltaTime;
            var interval = Mathf.Max(settings.interval, 0.05f);
            while (state.timer >= interval)
            {
                state.timer -= interval;
                SpawnForOwners(entity, settings);
            }
        }));
    }

    private static void SpawnForOwners(Entity statusEntity, StatusParticleSettings settings)
    {
        foreach (var owner in statusEntity.GetIncomingLinks<StatusRelation>().Entities)
        {
            if (!owner.HasComponent<ActorBinder>()) continue;
            var actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor.isRekt()) continue;

            for (var i = 0; i < settings.count; i++)
            {
                actor.spawnParticle(settings.color);
            }
        }
    }
}
