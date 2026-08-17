using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

/// <summary>记录带有 MotionRibbonTrail 的实体运动历史，并维护离体后继续消散的池化视图。</summary>
internal sealed class RenderMotionRibbonTrailSystem :
    QuerySystem<Position, MotionRibbonTrail, MotionRibbonTrailBinder>,
    IWorldStateClearable
{
    private readonly MonoObjPool<MotionRibbonTrailView> pool;
    private readonly List<MotionRibbonTrailView> activeViews = new();
    private int frame;

    /// <summary>建立世界轨迹根节点、视图池和活动实体查询。</summary>
    public RenderMotionRibbonTrailSystem()
    {
        GameObject root = new("motion_ribbon_trails");
        root.transform.SetParent(World.world.transform, false);
        MotionRibbonTrailView prefab = MotionRibbonTrailView.CreatePrefab();
        pool = new MonoObjPool<MotionRibbonTrailView>(
            prefab,
            root.transform,
            view => view.CreateMeshInstances(),
            view => view.ResetView(),
            view => view.ResetView());
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    void IWorldStateClearable.ClearWorldState()
    {
        Clear();
    }

    /// <summary>采样活动实体，再让已结束实体的历史轨迹独立淡出。</summary>
    protected override void OnUpdate()
    {
        if (!MapBox.isRenderGameplay())
        {
            Clear();
            return;
        }

        frame++;
        float now = Time.time;
        Query.ForEachEntity((
            ref Position position,
            ref MotionRibbonTrail trail,
            ref MotionRibbonTrailBinder binder,
            Entity entity) =>
        {
            if (!trail.Enabled)
            {
                if (binder.Value != null)
                {
                    binder.Value.Detach();
                    binder.Value = null;
                }
                return;
            }

            MotionRibbonTrailView view = binder.Value;
            if (view == null || !view.IsBoundTo(entity.Id))
            {
                view = pool.GetNext();
                view.Bind(entity.Id);
                activeViews.Add(view);
                binder.Value = view;
            }
            Vector3 origin = trail.SourceOrigin;
            view.Touch(
                new Vector3(position.x, position.y + position.z),
                new Vector3(origin.x, origin.y + origin.z),
                trail,
                now,
                frame);
        });

        for (int i = activeViews.Count - 1; i >= 0; i--)
        {
            MotionRibbonTrailView view = activeViews[i];
            if (view.LastTouchedFrame != frame) view.Detach();
            if (view.Render(now)) continue;
            activeViews.RemoveAt(i);
            pool.Return(view);
        }
    }

    /// <summary>离开世界渲染态时解除活动绑定并立即回收全部视图。</summary>
    private void Clear()
    {
        if (Query != null)
        {
            Query.ForEachComponents((
                ref Position _,
                ref MotionRibbonTrail _,
                ref MotionRibbonTrailBinder binder) => binder.Value = null);
        }
        for (int i = activeViews.Count - 1; i >= 0; i--) pool.Return(activeViews[i]);
        activeViews.Clear();
    }
}
