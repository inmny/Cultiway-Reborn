using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

/// <summary>从真实状态与持有者生成空间标记，不生成循环动画实体或第二份状态寿命。</summary>
public sealed class RenderStatusAnimationSystem : QuerySystem<StatusComponent>, IWorldStateClearable
{
    /// <summary>当前显示帧的共同空间快照，供内容侧显示边界读取。</summary>
    internal static readonly List<SpatialVisualMeshInstance> Meshes = new();

    /// <summary>跳过尚未启用与已经结束的状态。</summary>
    public RenderStatusAnimationSystem() => Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());

    /// <summary>读取实际持有关系和世界时间，状态移除后本帧就不再显示。</summary>
    protected override void OnUpdate()
    {
        Meshes.Clear();
        double now = World.world.getCurWorldTime();
        Query.ForEachEntity((ref StatusComponent status, Entity entity) =>
        {
            StatusSpatialSettings settings = status.Type.SpatialSettings;
            if (settings == null) return;
            float multiplier = entity.TryGetComponent(out StatusAnimationState state) && state.ScaleMultiplier > 0f
                ? state.ScaleMultiplier : 1f;
            foreach (Entity owner in entity.GetIncomingLinks<StatusRelation>().Entities)
            {
                if (!owner.TryGetComponent(out ActorBinder binder)) continue;
                Actor actor = binder.Actor;
                if (actor == null || !actor.isAlive()) continue;
                Vector3 foot = new(actor.current_position.x, actor.current_position.y, actor.position_height);
                if (!SpatialVisualEffects.IsRelevant(foot, 2f)) continue;
                int first = Meshes.Count;
                float height = Mathf.Max(5f, actor.getAnimationFrameData()?.size_unit.y ?? 8f) * actor.actor_scale;
                float radius = height * settings.RadiusRatio * multiplier;
                Vector3 center = foot + Vector3.forward * height * .5f;
                if (settings.Form == StatusSpatialForm.Reserve)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (float)now + i * Mathf.PI * 2f / 3f;
                        SpatialVisualDraw.Mesh(Meshes, SpatialVisualGeometry.Stone,
                            center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Sin(angle * 2f) * .2f) * radius,
                            Quaternion.identity, Vector3.one * height * .06f, settings.Color, true, foot);
                    }
                }
                else if (settings.Form is StatusSpatialForm.Bind or StatusSpatialForm.Freeze)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 3f;
                        Vector3 point = foot + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * .7f;
                        SpatialVisualDraw.Mesh(Meshes, settings.Form == StatusSpatialForm.Freeze
                                ? SpatialVisualGeometry.Shard : SpatialVisualGeometry.Leaf,
                            point, Quaternion.Euler(0f, 12f, angle * Mathf.Rad2Deg),
                            new Vector3(radius * .28f, radius * .2f, height * .8f), settings.Color, false, foot);
                    }
                    SpatialVisualDraw.Ring(Meshes, center, radius, settings.Color, Quaternion.identity, foot);
                }
                else
                {
                    SpatialVisualDraw.Mesh(Meshes, SpatialVisualGeometry.Arc, center,
                        Quaternion.Euler(45f, 0f, (float)now * 24f), Vector3.one * radius, settings.Color, true, foot);
                    SpatialVisualDraw.Ring(Meshes, foot + Vector3.forward * .04f, radius, settings.Color,
                        Quaternion.identity, foot);
                }
                for (int i = first; i < Meshes.Count; i++) Meshes[i] = Meshes[i].WithBodyAnchor(actor);
            }
        });
    }

    /// <summary>世界切换后不保留持有者或表现引用。</summary>
    void IWorldStateClearable.ClearWorldState() => Meshes.Clear();
}
