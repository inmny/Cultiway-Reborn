using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Performance;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

public class ArtifactEquipmentSystem : QuerySystem<ActorBinder, ArtifactLoadoutState>
{
    private const float EvaluationInterval = 2f;
    private double _lastEvaluationTime = double.MinValue;

    protected override void OnUpdate()
    {
        if (!GeneralSettings.EnableArtifactSystems) return;

        double now = SimulationTime.Now;
        if (_lastEvaluationTime != double.MinValue && now - _lastEvaluationTime < EvaluationInterval) return;
        
        float elapsed = _lastEvaluationTime == double.MinValue
            ? 0f
            : Mathf.Clamp((float)(now - _lastEvaluationTime), 0f, 10f);
        _lastEvaluationTime = now;

        using ListPool<ActorExtend> actors = new();
        Query.ForEachEntity((ref ActorBinder binder, ref ArtifactLoadoutState _, Entity _) =>
        {
            Actor actor = binder.Actor;
            if (actor != null && actor.isAlive()) actors.Add(binder.AE);
        });

        foreach (ActorExtend actor in actors)
        {
            ArtifactLoadoutPlanner.Refresh(actor, true, elapsed);
            if (!actor.HasItem<Artifact>())
            {
                actor.E.RemoveComponent<ArtifactLoadoutState>();
            }
        }
    }
}
