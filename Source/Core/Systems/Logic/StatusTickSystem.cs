using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Logic;

public class StatusTickSystem : QuerySystem<StatusComponent, StatusTickState>
{
    public StatusTickSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        var deltaTime = Tick.deltaTime;
        Query.ForEachEntity(((ref StatusComponent status, ref StatusTickState tickState, Entity entity) =>
        {
            var settings = status.Type.TickSettings;
            if (!settings.enabled || settings.Action == null) return;

            var interval = Mathf.Max(settings.interval, 0.05f);
            tickState.Timer += deltaTime;
            while (tickState.Timer >= interval)
            {
                tickState.Timer -= interval;
                settings.Action(entity, interval);
            }
        }));
    }
}
