using System.Collections.Generic;
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
/// 在 <see cref="LogicTrajectorySystem"/> 之后执行，只处理原本具备碰撞体的飞行实体。
/// </summary>
public class LogicSkillGroundFxRecordSystem :
    QuerySystem<Position, SkillGroundFxState, SkillEntity, Trajectory, ColliderSphere>
{
    private readonly List<PendingGroundEffect> _pendingEffects = new();

    public LogicSkillGroundFxRecordSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoTravelEffects>());
    }

    protected override void OnUpdate()
    {
        _pendingEffects.Clear();
        Query.ForEachEntity((ref Position pos, ref SkillGroundFxState fxState, ref SkillEntity skillEntity,
            ref Trajectory trajectory, ref ColliderSphere collider, Entity entity) =>
        {
            var currentPos = pos.value;
            if (skillEntity.Asset.WorldVisualProfile?.SuppressDefaultFlyOver == true)
            {
                fxState.DistanceAccumulator = 0f;
                fxState.LastX = currentPos.x;
                fxState.LastY = currentPos.y;
                return;
            }
            if ((trajectory.Asset.Domains & SkillGroundFx.FlyOverDomains) == SkillTrajectoryDomain.None)
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
            var firstSampleDistance = SkillGroundFx.FlyOverDistanceThreshold - fxState.DistanceAccumulator;
            for (var sampleDistance = firstSampleDistance; sampleDistance <= distance;
                 sampleDistance += SkillGroundFx.FlyOverDistanceThreshold)
            {
                var t = sampleDistance / distance;
                var samplePos = new Vector3(fxState.LastX + dx * t, fxState.LastY + dy * t, currentPos.z);
                _pendingEffects.Add(new PendingGroundEffect(samplePos, skillEntity.VfxElement));
            }

            var totalDistance = fxState.DistanceAccumulator + distance;
            fxState.DistanceAccumulator = totalDistance % SkillGroundFx.FlyOverDistanceThreshold;
            fxState.LastX = currentPos.x;
            fxState.LastY = currentPos.y;
        });

        for (int i = 0; i < _pendingEffects.Count; i++)
        {
            PendingGroundEffect effect = _pendingEffects[i];
            SkillGroundFx.OnFlyOver(effect.Position, effect.Element);
        }
    }

    private readonly struct PendingGroundEffect(Vector3 position, SkillVfxElementAsset element)
    {
        public Vector3 Position { get; } = position;
        public SkillVfxElementAsset Element { get; } = element;
    }
}
