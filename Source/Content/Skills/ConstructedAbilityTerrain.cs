using System;
using System.Linq;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>主线程采集地表，角色、动作和形体在同一轮使用相同的绝对高度。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>把旧法宝的相对地表姿态转换为共同数值执行使用的绝对世界姿态。</summary>
    /// <param name="pose">原模型与原显示共用的位置、朝向及承托来源。</param>
    internal static Matrix4x4 CarrierFrame(in SpatialVisualMeshInstance pose)
    {
        Vector3 position = pose.Position;
        if (pose.GroundActor != null)
            position += BodyFoot(pose.GroundActor) - SpatialActorPose.Foot(pose.GroundActor);
        else position.z += simulation?.terrain?.Sample(pose.GroundAnchor.x, pose.GroundAnchor.y) ?? 0f;
        return Matrix4x4.TRS(position, pose.Rotation, Vector3.one);
    }

    /// <summary>可选环境提供实际地表；未安装时保留原版零平面。</summary>
    internal static Func<Rect, AbilityHeightField> GroundProvider;

    /// <summary>可选实际模型阻挡，与本轮共同地表在主线程一起采集。</summary>
    internal static Action<Rect, AbilityHeightField> CaptureBodyObstacles;

    /// <summary>可选模型环境把普通站立桥面纳入世界脚下；地表高度由本轮采样提供，不重复创建地形。</summary>
    internal static Func<Actor, Vector3, float, Vector3> StandingFootProvider;

    /// <summary>这一轮冻结的地表，不在工作计算中替换。</summary>
    private AbilityHeightField terrain;

    /// <summary>地表改变后先重新采样连接，不将地形抬升误作整段扫掠。</summary>
    private bool terrainNeedsRefresh;

    /// <summary>按实际活动区域刷新环境，并保持已经发布的旧快照不变。</summary>
    /// <param name="extra">新出手需要覆盖的来源和目标。</param>
    /// <param name="bodyMovement">新出手尚未入列，但本次需要身体移动阻挡。</param>
    private void CaptureTerrain(Rect? extra = null, bool bodyMovement = false)
    {
        if (GroundProvider == null) { CapturePropagationGround(); return; }
        Rect area = extra ?? new Rect(0, 0, 1, 1);
        bool first = !extra.HasValue;
        foreach (RunningCast running in casts)
        {
            Include(running.LastSourceFoot);
            Include(running.LastTargetFoot);
            if (running.Cast.Plan.HasPropagation)
            {
                float range = running.Cast.Plan.EventOperations.Where(operation => operation.Propagation != null)
                    .Select(operation => operation.Propagation.Range * running.Cast.Plan.Nodes[operation.Node].Scale)
                    .DefaultIfEmpty(0f).Max();
                Include(running.LastSourceFoot + new Vector3(range, range, 0f));
                Include(running.LastSourceFoot - new Vector3(range, range, 0f));
            }
            if (running.Movement != null)
            {
                bodyMovement = true;
                float travel = running.Cast.Plan.Body.Construction.FootworkTravel * running.LastSourceHeight;
                Include(running.LastSourceFoot + new Vector3(travel, travel, 0f));
                Include(running.LastSourceFoot - new Vector3(travel, travel, 0f));
            }

            for (int i = 0; i < running.Cast.Plan.FormCount; i++)
                if (running.Cast.NodeAge(i) >= 0)
                {
                    Vector3 centre = running.Cast.ContactOrigin(i);
                    Include(centre);
                    CompiledConstructionRadialMotion field = running.Cast.Plan.Nodes[i].RadialMotion;
                    if (field != null)
                    {
                        Include(centre - new Vector3(field.Radius, field.Radius, 0f));
                        Include(centre + new Vector3(field.Radius, field.Radius, 0f));
                    }
                    Rect? flow = running.Cast.FlowBounds(i);
                    if (flow.HasValue)
                    {
                        Include(flow.Value.min);
                        Include(flow.Value.max);
                    }
                }
        }

        area.xMin -= 20;
        area.yMin -= 20;
        area.xMax += 20;
        area.yMax += 20;
        ApplyTerrain(GroundProvider(area));
        // 普通桥面也参与出生点与整个收势过程，不能只在带步法的动作里保留这份实际环境。
        if (bodyMovement || StandingFootProvider != null) CaptureBodyObstacles?.Invoke(area, terrain);
        CapturePropagationGround();

        void Include(Vector3 point)
        {
            if (first)
            {
                area = new Rect(point.x, point.y, 0, 0);
                first = false;
            }
            else
            {
                area.xMin = Mathf.Min(area.xMin, point.x);
                area.yMin = Mathf.Min(area.yMin, point.y);
                area.xMax = Mathf.Max(area.xMax, point.x);
                area.yMax = Mathf.Max(area.yMax, point.y);
            }
        }
    }

    /// <summary>实际世界地形改变后，下一次预测前使用新高度；原版零平面不另建数组。</summary>
    /// <param name="tile">已经改变的地块。</param>
    private void RefreshTerrainAt(WorldTile tile)
    {
        if (GroundProvider == null) { CapturePropagationGround(); return; }
        AbilityHeightField prior = terrain;
        ApplyTerrain(GroundProvider(new Rect(tile.pos.x, tile.pos.y, 1, 1)));
        CapturePropagationGround();
        if (ReferenceEquals(prior, terrain)) return;
        // 人物前后态原来包含旧地面；只替换地面差，保留共同时间内真实运动。
        actorIndex.Clear();
        for (int i = 0; i < actors.Count; i++)
        {
            ActorMotion old = actors[i];
            Vector3 from = old.From, to = old.To;
            from.z += (terrain?.Sample(from.x, from.y) ?? 0f) - (prior?.Sample(from.x, from.y) ?? 0f);
            to.z += (terrain?.Sample(to.x, to.y) ?? 0f) - (prior?.Sample(to.x, to.y) ?? 0f);
            actors[i] = new ActorMotion(old.Actor, old.Id, from, to, old.FromHeight, old.Height, old.Body);
            float reach = Mathf.Max(old.FromHeight, old.Height) *
                          (1f + (old.Body?.Cast.Plan.Body.MaximumTorsoOffset ?? 0f) +
                           (old.Body?.Cast.Plan.Body.Construction.FootworkTravel ?? 0f));
            actorIndex.Add(AbilityContactIndex.Swept(new AbilityCapsule(0, from, from, reach, null),
                new AbilityCapsule(0, to, to, reach, null)));
        }
    }

    /// <summary>共同模拟开始或真实世界操作之后，发布本轮完整的地面输入。</summary>
    private void CapturePropagationGround()
    {
        PropagationGround current = null;
        foreach (RunningCast running in casts)
            if (running.Cast.Plan.HasPropagation)
            {
                current ??= PropagationGroundMap.Capture(terrain);
                running.Cast.UpdatePropagationGround(current);
            }
    }

    /// <summary>所有施放同时接收新只读地面，旧发布帧继续保留原快照。</summary>
    /// <param name="current">本次实际地表。</param>
    private void ApplyTerrain(AbilityHeightField current)
    {
        ConstructedActorImpulse.PublishTerrain(current);
        if (ReferenceEquals(current, terrain)) return;
        AbilityHeightField prior = terrain;
        terrain = current;
        terrainNeedsRefresh = true;
        for (int i = 0; i < casts.Count; i++)
        {
            RunningCast running = casts[i];
            // 地点输入保留原离地高度，随地表更新，避免随动主体仍连接旧地面。
            if (running.Target == null && running.TargetId == 0)
            {
                Vector3 point = running.LastTargetFoot;
                float difference = (current?.Sample(point.x, point.y) ?? 0f) -
                                   (prior?.Sample(point.x, point.y) ?? 0f);
                running.LastTargetFoot.z += difference;
                running.LastTarget.z += difference;
            }
            BodyConnection connection = running.Connection;
            if (connection != null &&
                (Moved(running.Cast.BodyPose.Value.Foot) || Moved(connection.TargetFoot)))
                ReleaseBodyConnection(running, "TerrainChanged", fade: false);
            running.Cast.UpdateTerrain(current);
        }

        /// <summary>地表重采样已经改变实际支撑高度，零时长刷新前先归还旧世界连接。</summary>
        /// <param name="foot">原共同脚下。</param>
        bool Moved(Vector3 foot) => Mathf.Abs((current?.Sample(foot.x, foot.y) ?? 0f) -
                                              (prior?.Sample(foot.x, foot.y) ?? 0f)) > .0001f;
    }

    /// <summary>原版离地高度加上本轮地表及实际普通承托，历史仍保留原版位置。</summary>
    /// <param name="actor">主线程中的真实人物。</param>
    private static Vector3 PhysicsFoot(Actor actor) => WorldFoot(actor, SpatialActorPose.Foot(actor));

    /// <summary>新旧位置沿同一世界高度换算，桥面不会在接管时才突然出现。</summary>
    /// <param name="actor">真实人物。</param><param name="foot">原版位置与离地高度。</param>
    private static Vector3 WorldFoot(Actor actor, Vector3 foot)
    {
        if (ConstructedActorImpulse.TryBody(actor.id, out var flying)) return flying.Surface.Foot;
        float ground = simulation?.terrain?.Sample(foot.x, foot.y) ?? 0f;
        foot.z += ground;
        return StandingFootProvider?.Invoke(actor, foot, ground) ?? foot;
    }

    /// <summary>离地高度不为零也可能站在模型屋面，只有实际脚下承托成立才允许支撑动作。</summary>
    /// <param name="actor">实际身体。</param>
    internal static bool OnSurface(Actor actor)
    {
        Vector3 foot = PhysicsFoot(actor);
        float floor = BodyLocomotion.SurfaceHeight?.Invoke(foot, SpatialActorPose.Height(actor) * .16f,
            foot.z + .0001f) ?? float.NegativeInfinity;
        return Mathf.Abs(foot.z - floor) < .0002f;
    }
}
