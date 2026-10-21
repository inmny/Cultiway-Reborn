using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>落雷的共用空间形状，只读取原有下降、落点结算和动画阶段。</summary>
internal static class LightningStrikeSpatialVisual
{
    /// <summary>有限组固定折线，同一次施放在两个视角保持一致。</summary>
    private static readonly SpatialVisualMesh[] paths = SpatialEnergyGeometry.Bolts;

    /// <summary>由原执行体的位置伸展雷路，不产生另一套抵达时间和伤害。</summary>
    /// <param name="entity">仍在运行的原落雷。</param>
    /// <param name="position">实际下降位置。</param>
    /// <param name="lifecycle">实际动画阶段。</param>
    /// <param name="animation">当前阶段进度。</param>
    /// <param name="meshes">两种视角共用的快照。</param>
    internal static void Append(Entity entity, Vector3 position, SkillAnimationLifecycleState lifecycle,
        AnimData animation, List<SpatialVisualMeshInstance> meshes)
    {
        if (lifecycle.Phase == SkillAnimationPhase.RecycleReady) return;
        SkillContext context = entity.GetComponent<SkillContext>();
        TrajectoryRuntimeState trajectory = entity.GetComponent<TrajectoryRuntimeState>();
        TrajectoryParams parameters = entity.GetComponent<TrajectoryParams>();
        SkillPositionImpactState impact = entity.GetComponent<SkillPositionImpactState>();
        Vector3 target = context.TargetObj != null && !context.TargetObj.isRekt()
            ? context.TargetObj.GetSimPos() : context.TargetPos;
        float progress = Mathf.Clamp01((animation.frame_idx + animation.frame_timer /
            SkillAnimationLifecycle.ResolveCurrentFrameInterval(entity)) / animation.frames.Length);
        // 此变体的出现阶段已经允许下降，必须以轨迹是否启动判断，不能等待出现动画播完。
        bool appearing = trajectory.Elapsed <= 0f;
        bool ending = lifecycle.Phase == SkillAnimationPhase.Dissipation;
        float top = trajectory.Elapsed > 0f ? trajectory.StartPosition.z : target.z + parameters.StartHeight;
        float bottom = appearing ? top - 0.25f - progress * 0.5f : position.z;
        Vector3 origin = appearing ? target : new Vector3(position.x, position.y, target.z);
        origin.z = bottom;
        float length = Mathf.Max(0.12f, top - bottom);
        float fade = ending ? Mathf.Pow(1f - progress, 2f) : appearing ? 0.35f + progress * 0.35f : 1f;
        float width = ending ? 1f - progress * 0.8f : 1f;
        meshes.Add(new SpatialVisualMeshInstance(paths[(entity.Id & int.MaxValue) % paths.Length], origin,
            Quaternion.identity, new Vector3(width * Mathf.Sqrt(entity.GetComponent<EffectRadiusScale>().Value) * 2f, width * Mathf.Sqrt(entity.GetComponent<EffectRadiusScale>().Value) * 2f, length), new Color(0.82f, 0.85f, 1f, fade), true, true));
        if (!impact.Resolved) return;
        // 地面放电跟随落点结算；空地也显示，不能据此伪造击中单位。
        float radius = SkillEffectRadius.Resolve(entity, entity.GetComponent<ColliderSphere>().Radius);
        float contactFade = ending ? 1f - progress : 1f;
        Vector3 ground = new(position.x, position.y, target.z + 0.025f);
        meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Ring, ground, Quaternion.identity,
            Vector3.one * radius * (0.2f + progress * 0.75f),
            new Color(0.64f, 0.7f, 1f, contactFade * 0.7f), true, true));
        meshes.Add(new SpatialVisualMeshInstance(SpatialVisualGeometry.Spark, ground + Vector3.forward * 0.08f,
            Quaternion.identity, Vector3.one * Mathf.Min(radius * 0.35f, 0.45f) * contactFade,
            new Color(0.92f, 0.95f, 1f, contactFade), true, true));
    }

}
