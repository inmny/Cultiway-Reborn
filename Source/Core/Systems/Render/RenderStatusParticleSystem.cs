using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

/// <summary>状态的稀疏粒子使用共用空间表现，不依赖原版镜头与 Unity 粒子系统。</summary>
public class RenderStatusParticleSystem : QuerySystem<StatusComponent, StatusParticleState>
{
    /// <summary>跳过预制体和已经结束的状态。</summary>
    public RenderStatusParticleSystem() => Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());

    /// <summary>按世界时间发射，长帧不补发历史粒子，纯视觉种子不消耗战斗随机数。</summary>
    protected override void OnUpdate()
    {
        float delta = Tick.deltaTime;
        Query.ForEachEntity((ref StatusComponent status, ref StatusParticleState state, Entity entity) =>
        {
            var settings = status.Type.ParticleSettings;
            if (!settings.enabled || settings.count <= 0) return;
            state.timer += delta;
            float interval = Mathf.Max(.05f, settings.interval);
            if (state.timer < interval) return;
            state.timer %= interval;
            foreach (Entity owner in entity.GetIncomingLinks<StatusRelation>().Entities)
            {
                if (!owner.TryGetComponent(out ActorBinder binder)) continue;
                Actor actor = binder.Actor;
                if (actor == null || !actor.isAlive()) continue;
                Vector3 foot = new(actor.current_position.x, actor.current_position.y, actor.position_height);
                float height = Mathf.Max(5f, actor.getAnimationFrameData()?.size_unit.y ?? 8f) * actor.actor_scale;
                if (!SpatialVisualEffects.IsRelevant(foot, height)) continue;
                for (int i = 0; i < settings.count; i++)
                {
                    float angle = (float)World.world.getCurWorldTime() * 1.7f + i * 2.4f + entity.Id;
                    Vector3 point = foot + new Vector3(Mathf.Cos(angle) * .3f, Mathf.Sin(angle) * .2f, .3f + .2f * Mathf.Sin(angle * 2f)) * height;
                    SpatialVisualEffects.Particle(point, Vector3.forward * height * .2f, settings.color, height * .07f, .6f);
                }
            }
        });
    }
}
