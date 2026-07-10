using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Logic;

public class StatusTickSystem : QuerySystem<StatusComponent, StatusTickState>
{
    private readonly List<PendingTick> _pendingTicks = new();

    public StatusTickSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        _pendingTicks.Clear();
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
                _pendingTicks.Add(new PendingTick(entity, settings.Action, interval));
            }
        }));

        for (int i = 0; i < _pendingTicks.Count; i++)
        {
            PendingTick tick = _pendingTicks[i];
            if (!tick.Entity.IsNull)
            {
                tick.Action(tick.Entity, tick.Interval);
            }
        }
    }

    private readonly struct PendingTick(Entity entity, StatusTickAction action, float interval)
    {
        public Entity Entity { get; } = entity;
        public StatusTickAction Action { get; } = action;
        public float Interval { get; } = interval;
    }
}
