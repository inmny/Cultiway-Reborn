using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Impacts;

public static class SkillImpactRuntime
{
    public static void Initialize(Entity entity)
    {
        SkillEntityAsset asset = entity.GetComponent<SkillEntity>().Asset;
        SkillImpactProfileAsset profile = asset.ImpactProfile;

        SkillImpactTuning tuning = asset.ImpactTuning;
        entity.GetComponent<AliveTimeLimit>().value = profile.Lifetime * tuning.LifetimeMultiplier;
        float radiusScale = entity.GetComponent<EffectRadiusScale>().Value;

        if (profile.HitOncePerTarget || profile.RepeatHitInterval > 0f)
        {
            SetOrAdd(entity, SkillHitMemory.Create());
        }
        if (entity.HasComponent<SkillPositionImpactState>())
        {
            entity.GetComponent<SkillPositionImpactState>() = default;
        }
        if ((profile.LinearForward > 0f || profile.LinearBackward > 0f) &&
            entity.HasComponent<ColliderLinearExtent>())
        {
            ref ColliderLinearExtent extent = ref entity.GetComponent<ColliderLinearExtent>();
            extent.Forward = profile.LinearForward * radiusScale;
            extent.Backward = profile.LinearBackward * radiusScale;
        }

        if (profile.IsBeam)
        {
            SetOrAdd(entity, new ColliderLinearExtent
            {
                UseEntityRotation = true
            });
        }

        if (profile.IsField)
        {
            SetOrAdd(entity, new SkillPersistentState
            {
                Kind = SkillPersistentKind.Field,
                MaxInstances = profile.PersistentLimit
            });
            return;
        }

        if (!profile.IsBarrier) return;
        SkillPersistentKind kind = profile.Kind == SkillImpactKind.Shield
            ? SkillPersistentKind.Shield
            : SkillPersistentKind.Barrier;
        SetOrAdd(entity, new SkillPersistentState
        {
            Kind = kind,
            MaxInstances = profile.PersistentLimit,
            Durability = entity.GetComponent<SkillContext>().Strength * profile.DurabilityMultiplier,
            Length = profile.BarrierLength * tuning.BarrierLengthMultiplier * radiusScale,
            Width = profile.BarrierWidth * radiusScale
        });
        if (kind == SkillPersistentKind.Barrier)
        {
            float halfLength = profile.BarrierLength * tuning.BarrierLengthMultiplier * radiusScale * 0.5f;
            SetOrAdd(entity, new ColliderLinearExtent
            {
                Forward = halfLength,
                Backward = halfLength,
                UseEntityRotation = true
            });
        }
    }

    public static bool RequestPositionImpact(Entity entity)
    {
        if (!entity.HasComponent<SkillPositionImpactState>()) return false;
        ref SkillPositionImpactState state = ref entity.GetComponent<SkillPositionImpactState>();
        if (!state.Resolved) state.Requested = true;
        return true;
    }

    private static void SetOrAdd<TComponent>(Entity entity, TComponent component)
        where TComponent : struct, IComponent
    {
        if (entity.HasComponent<TComponent>())
        {
            entity.GetComponent<TComponent>() = component;
        }
        else
        {
            entity.AddComponent(component);
        }
    }
}
