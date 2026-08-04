using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Systems.Logic;

/// <summary>按月结算无需占用角色任务的修炼方式。</summary>
public sealed class TimedCultivationSystem : QuerySystem<Xian, ActorBinder>
{
    private const float UpdateInterval = TimeScales.SecPerMonth;
    private readonly List<ActorExtend> pendingActors = new();
    private float updateTimer = UpdateInterval;

    public TimedCultivationSystem()
    {
        Filter.AnyTags(Tags.Get<TimedCultivationTag>());
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        updateTimer -= Tick.deltaTime;
        if (updateTimer > 0f) return;
        updateTimer = UpdateInterval;
        pendingActors.Clear();
        Query.ForEachComponents((ref Xian xian, ref ActorBinder binder) =>
        {
            Actor actor = binder.Actor;
            if (actor != null && actor.isAlive()) pendingActors.Add(actor.GetExtend());
        });

        for (var i = 0; i < pendingActors.Count; i++)
        {
            var context = new CultivationTriggerContext(
                pendingActors[i],
                CultivationTriggerKind.TimedTick,
                elapsedSeconds: UpdateInterval);
            CultivateMethods.TryDispatch(in context);
        }
    }
}
