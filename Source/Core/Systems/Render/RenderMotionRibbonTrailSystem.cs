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
    /// <summary>可选空间显示接入后，原版远景仍保留真实运动历史。</summary>
    internal static bool SpatialHistoryRequested;
    /// <summary>与二维轨迹共享运动历史的空间线段，三维显示只读取。</summary>
    internal static readonly List<SpatialVisualLine> SpatialLines = new();

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
        bool drawNative = MapBox.isRenderGameplay();
        if (!drawNative && !SpatialHistoryRequested)
        {
            Clear();
            return;
        }

        frame++;
        float now = (float)World.world.getCurWorldTime();
        SpatialLines.Clear();
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
                position.value,
                origin,
                trail,
                now,
                frame);
        });

        for (int i = activeViews.Count - 1; i >= 0; i--)
        {
            MotionRibbonTrailView view = activeViews[i];
            if (view.LastTouchedFrame != frame) view.Detach();
            if (view.Render(now, drawNative))
            {
                view.AppendSpatialLines(SpatialLines, now);
                continue;
            }
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
        SpatialLines.Clear();
    }
}
