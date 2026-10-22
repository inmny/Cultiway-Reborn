using System;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>主线程采集地表，角色、动作和形体在同一轮使用相同的绝对高度。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>可选环境提供实际地表；未安装时保留原版零平面。</summary>
    internal static Func<Rect, AbilityHeightField> GroundProvider;
    /// <summary>这一轮冻结的地表，不在工作计算中替换。</summary>
    private AbilityHeightField terrain;
    /// <summary>地表改变后先重新采样连接，不将地形抬升误作整段扫掠。</summary>
    private bool terrainNeedsRefresh;

    /// <summary>按实际活动区域刷新环境，并保持已经发布的旧快照不变。</summary>
    /// <param name="extra">新出手需要覆盖的来源和目标。</param>
    private void CaptureTerrain(Rect? extra = null)
    {
        if (GroundProvider == null) return;
        Rect area = extra ?? new Rect(0, 0, 1, 1);
        bool first = !extra.HasValue;
        foreach (RunningCast running in casts)
        {
            Include(running.LastSourceFoot); Include(running.LastTargetFoot);
            for (int i = 0; i < running.Cast.Plan.FormCount; i++)
                if (running.Cast.NodeAge(i) >= 0) Include(running.Cast.ContactOrigin(i));
        }
        area.xMin -= 20; area.yMin -= 20; area.xMax += 20; area.yMax += 20;
        AbilityHeightField current = GroundProvider(area);
        if (ReferenceEquals(current, terrain)) return;
        terrain = current; terrainNeedsRefresh = true;
        foreach (RunningCast running in casts) running.Cast.UpdateTerrain(current);
        void Include(Vector3 point)
        {
            if (first) { area = new Rect(point.x, point.y, 0, 0); first = false; }
            else { area.xMin = Mathf.Min(area.xMin, point.x); area.yMin = Mathf.Min(area.yMin, point.y); area.xMax = Mathf.Max(area.xMax, point.x); area.yMax = Mathf.Max(area.yMax, point.y); }
        }
    }

    /// <summary>原版离地高度加上本轮地表，历史仍保留原版位置以重算变化后的地面。</summary>
    /// <param name="actor">主线程中的真实人物。</param>
    private static Vector3 PhysicsFoot(Actor actor)
    {
        Vector3 foot = SpatialActorPose.Foot(actor);
        foot.z += simulation?.terrain?.Sample(foot.x, foot.y) ?? 0f;
        return foot;
    }
}
