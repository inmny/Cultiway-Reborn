using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using DeliveryTag = Cultiway.Core.Semantics.SkillSemantics.Delivery;
using MotionTag = Cultiway.Core.Semantics.SkillSemantics.Motion;

namespace Cultiway.Content.Combat;

internal enum EquippedWeaponMotionKind
{
    Sweep,
    Thrust,
    Crush,
    GuardTurn,
    GuardHold,
    ForwardAnchor,
}

/// <summary>真实武器短动作的逐帧运动快照。</summary>
internal struct EquippedWeaponMotionState : IComponent
{
    public Item Weapon;
    public EquippedWeaponMotionKind Kind;
    public float Elapsed;
    public float Duration;
    public float MotionDuration;
    public float Reach;
    public float StartAngle;
    public float EndAngle;
    public float DamageMultiplier;
    public Vector3 Direction;
    public bool CollisionEnabled;
}

/// <summary>御器和骑士战技共用的真实武器连续运动。</summary>
public sealed class EquippedWeaponTrajectories : ExtendLibrary<TrajectoryAsset, EquippedWeaponTrajectories>
{
    public static TrajectoryAsset Motion { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.EquippedWeaponTrajectory";

    protected override void OnInit()
    {
        Motion.CanBeSelectedByModifier = false;
        Motion.EditorSelectable = false;
        Motion.Orientations = TrajectoryOrientation.Melee;
        Motion.WithDomains(SkillTrajectoryDomain.Melee)
            .AddSemantics(DeliveryTag.Melee, MotionTag.MeleeSweep);
        Motion.OnInit = entity => entity.AddComponent(new EquippedWeaponMotionState());
        Motion.Action = UpdateMotion;
    }

    private static void UpdateMotion(
        ref SkillContext context,
        ref Position position,
        ref Rotation rotation,
        Entity entity,
        float deltaTime)
    {
        Actor owner = context.SourceObj?.a;
        ref EquippedWeaponMotionState state = ref entity.GetComponent<EquippedWeaponMotionState>();
        if (!EquippedWeaponVisualService.IsCurrent(owner, state.Weapon) || owner.isFlying())
        {
            DisableAndRecycle(entity);
            return;
        }

        state.Elapsed += deltaTime;
        float duration = Mathf.Max(0.05f, state.Duration);
        float motionDuration = state.MotionDuration > 0f
            ? Mathf.Max(0.05f, state.MotionDuration)
            : duration;
        float lifeProgress = Mathf.Clamp01(state.Elapsed / duration);
        float motionProgress = Mathf.Clamp01(state.Elapsed / motionDuration);
        Vector3 direction = Normalize(state.Direction, context.TargetDir);
        switch (state.Kind)
        {
            case EquippedWeaponMotionKind.Thrust:
                UpdateThrust(owner, entity, ref state, ref position, ref rotation, direction, motionProgress);
                break;
            case EquippedWeaponMotionKind.Crush:
                UpdateCrush(owner, entity, ref state, ref position, ref rotation, direction, motionProgress);
                break;
            case EquippedWeaponMotionKind.GuardTurn:
                UpdateGuardTurn(owner, entity, ref state, ref position, ref rotation, direction, motionProgress);
                break;
            case EquippedWeaponMotionKind.GuardHold:
                UpdateGuardHold(owner, entity, ref state, ref position, ref rotation, direction, motionProgress);
                break;
            case EquippedWeaponMotionKind.ForwardAnchor:
                UpdateForwardAnchor(owner, entity, ref state, ref position, ref rotation, direction);
                break;
            default:
                UpdateSweep(owner, entity, ref state, ref position, ref rotation, direction, motionProgress);
                break;
        }

        if (lifeProgress >= 1f) ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
    }

    private static void UpdateSweep(
        Actor owner,
        Entity entity,
        ref EquippedWeaponMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, state.CollisionEnabled && progress >= 0.08f && progress <= 0.92f);
        float angle = Mathf.Lerp(state.StartAngle, state.EndAngle, Smooth(progress));
        Vector3 radial = Normalize(Quaternion.AngleAxis(angle, Vector3.forward) * direction, direction);
        float breathing = 0.88f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
        Vector3 origin = owner.GetSimPos();
        position.value = origin + radial * state.Reach * breathing;
        rotation.value = radial;
        entity.GetComponent<MotionRibbonTrail>().SourceOrigin = origin;
    }

    private static void UpdateThrust(
        Actor owner,
        Entity entity,
        ref EquippedWeaponMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, state.CollisionEnabled && progress >= 0.12f && progress <= 0.72f);
        float extension = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 0.72f);
        Vector3 origin = owner.GetSimPos();
        position.value = origin + direction * (0.35f + state.Reach * extension);
        rotation.value = direction;
        ref MotionRibbonTrail trail = ref entity.GetComponent<MotionRibbonTrail>();
        trail.SourceOrigin = origin;
        if (progress >= 0.72f) trail.Enabled = false;
    }

    private static void UpdateCrush(
        Actor owner,
        Entity entity,
        ref EquippedWeaponMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, state.CollisionEnabled && progress >= 0.55f && progress <= 0.94f);
        float angle = Mathf.Lerp(-112f, 24f, Smooth(progress));
        Vector3 radial = Normalize(Quaternion.AngleAxis(angle, Vector3.forward) * direction, direction);
        float height = Mathf.Sin(Mathf.Clamp01(progress / 0.78f) * Mathf.PI) * state.Reach * 0.95f;
        position.value = owner.GetSimPos() + radial * state.Reach * 0.82f;
        position.value += Vector3.forward * Mathf.Max(0f, height);
        rotation.value = radial;
        entity.GetComponent<MotionRibbonTrail>().SourceOrigin = owner.GetSimPos();
        if (!entity.HasComponent<AnimAfterimage>()) return;
        ref AnimAfterimage afterimage = ref entity.GetComponent<AnimAfterimage>();
        afterimage.ArcRadius = state.Reach * 0.82f;
        afterimage.ArcDirection = 1f;
    }

    private static void UpdateGuardHold(
        Actor owner,
        Entity entity,
        ref EquippedWeaponMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, false);
        float angle = Mathf.Lerp(state.StartAngle, state.EndAngle, Smooth(progress));
        Vector3 radial = Normalize(Quaternion.AngleAxis(angle, Vector3.forward) * direction, direction);
        Vector3 origin = owner.GetSimPos();
        position.value = origin + radial * state.Reach;
        rotation.value = radial;
        ref MotionRibbonTrail trail = ref entity.GetComponent<MotionRibbonTrail>();
        trail.SourceOrigin = origin;
        if (progress >= 1f) trail.Enabled = false;
    }

    private static void UpdateGuardTurn(
        Actor owner,
        Entity entity,
        ref EquippedWeaponMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, false);
        float angle = Mathf.Lerp(state.StartAngle, state.EndAngle, Smooth(progress));
        Vector3 radial = Normalize(Quaternion.AngleAxis(angle, Vector3.forward) * direction, direction);
        position.value = owner.GetSimPos() + radial * state.Reach;
        rotation.value = radial;
    }

    private static void UpdateForwardAnchor(
        Actor owner,
        Entity entity,
        ref EquippedWeaponMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction)
    {
        SetColliderEnabled(entity, false);
        Vector3 origin = owner.GetSimPos();
        position.value = origin + direction * state.Reach;
        rotation.value = direction;
        entity.GetComponent<MotionRibbonTrail>().SourceOrigin = origin;
    }

    private static void DisableAndRecycle(Entity entity)
    {
        SetColliderEnabled(entity, false);
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
    }

    private static void SetColliderEnabled(Entity entity, bool enabled)
    {
        entity.GetComponent<ColliderConfig>().Enabled = enabled;
    }

    private static float Smooth(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private static Vector3 Normalize(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.right;
    }
}
