using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Visuals;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 按飞行距离节流触发法术地面影响（火系烧焦、水系灭火、冰系凝冰等）。
/// 在 <see cref="LogicTrajectorySystem"/> 之后执行，保证读取的是更新后的位置。
/// </summary>
public class LogicSkillGroundFxRecordSystem :
    QuerySystem<Position, SkillGroundFxState, SkillEntity, Trajectory, ColliderSphere>
{
    /// <summary>飞行地面影响的距离阈值（世界单位）：每移动这么远触发一次 OnFlyOver。</summary>
    private const float FlyOverDistanceThreshold = 0.6f;

    public LogicSkillGroundFxRecordSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachComponents((ref Position pos, ref SkillGroundFxState fxState, ref SkillEntity skillEntity,
            ref Trajectory trajectory, ref ColliderSphere collider) =>
        {
            var currentPos = pos.value;
            if ((trajectory.Asset.Orientations & TrajectoryOrientation.Horizontal) == TrajectoryOrientation.None)
            {
                fxState.DistanceAccumulator = 0f;
                fxState.LastX = currentPos.x;
                fxState.LastY = currentPos.y;
                return;
            }

            var dx = currentPos.x - fxState.LastX;
            var dy = currentPos.y - fxState.LastY;
            var movedSq = dx * dx + dy * dy;
            if (movedSq <= 0.0001f) return;

            var distance = Mathf.Sqrt(movedSq);
            var firstSampleDistance = FlyOverDistanceThreshold - fxState.DistanceAccumulator;
            for (var sampleDistance = firstSampleDistance; sampleDistance <= distance;
                 sampleDistance += FlyOverDistanceThreshold)
            {
                var t = sampleDistance / distance;
                var samplePos = new Vector3(fxState.LastX + dx * t, fxState.LastY + dy * t, currentPos.z);
                SkillGroundFx.OnFlyOver(samplePos, collider.Radius, skillEntity.VfxElement);
            }

            var totalDistance = fxState.DistanceAccumulator + distance;
            fxState.DistanceAccumulator = totalDistance % FlyOverDistanceThreshold;
            fxState.LastX = currentPos.x;
            fxState.LastY = currentPos.y;
        });
    }
}
