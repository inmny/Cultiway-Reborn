using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
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
            const SkillTrajectoryDomain flyoverDomains = SkillTrajectoryDomain.FlyingBody |
                                                         SkillTrajectoryDomain.FlyingWave |
                                                         SkillTrajectoryDomain.Ballistic |
                                                         SkillTrajectoryDomain.GroundTravel |
                                                         SkillTrajectoryDomain.MobileField;
            if ((trajectory.Asset.Domains & flyoverDomains) == SkillTrajectoryDomain.None)
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
            var effectRadius = SkillEffectRadius.Resolve(entity, collider.Radius);
            for (var sampleDistance = firstSampleDistance; sampleDistance <= distance;
                 sampleDistance += FlyOverDistanceThreshold)
            {
                var t = sampleDistance / distance;
                var samplePos = new Vector3(fxState.LastX + dx * t, fxState.LastY + dy * t, currentPos.z);
                _pendingEffects.Add(new PendingGroundEffect(
                    samplePos, effectRadius, skillEntity.VfxElement, skillEntity.ColorPalette));
            }

            var totalDistance = fxState.DistanceAccumulator + distance;
            fxState.DistanceAccumulator = totalDistance % FlyOverDistanceThreshold;
            fxState.LastX = currentPos.x;
            fxState.LastY = currentPos.y;
        });

        for (int i = 0; i < _pendingEffects.Count; i++)
        {
            PendingGroundEffect effect = _pendingEffects[i];
            SkillGroundFx.OnFlyOver(effect.Position, effect.Radius, effect.Element, effect.ColorPalette);
        }
    }

    private readonly struct PendingGroundEffect(
        Vector3 position,
        float radius,
        SkillVfxElementAsset element,
        SemanticColorPalette colorPalette)
    {
        public Vector3 Position { get; } = position;
        public float Radius { get; } = radius;
        public SkillVfxElementAsset Element { get; } = element;
        public SemanticColorPalette ColorPalette { get; } = colorPalette;
    }
}
