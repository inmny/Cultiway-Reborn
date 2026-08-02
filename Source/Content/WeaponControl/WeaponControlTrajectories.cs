using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using DeliveryTag = Cultiway.Core.Semantics.SkillSemantics.Delivery;
using MotionTag = Cultiway.Core.Semantics.SkillSemantics.Motion;

namespace Cultiway.Content.WeaponControl;

/// <summary>御器近战执行体使用的内容侧轨迹。</summary>
public sealed class WeaponControlTrajectories : ExtendLibrary<TrajectoryAsset, WeaponControlTrajectories>
{
    /// <summary>按执行体动作快照分派挥砍、突刺或砸落。</summary>
    public static TrajectoryAsset WeaponMotion { get; private set; }

    /// <summary>自动注册本库公开的轨迹资产。</summary>
    protected override bool AutoRegisterAssets() => true;

    /// <summary>返回御器轨迹的稳定资产前缀。</summary>
    protected override string Prefix() => "Cultiway.WeaponControlTrajectory";

    /// <summary>配置不可编辑的御器专用近战轨迹。</summary>
    protected override void OnInit()
    {
        WeaponMotion.CanBeSelectedByModifier = false;
        WeaponMotion.EditorSelectable = false;
        WeaponMotion.Orientations = TrajectoryOrientation.Melee;
        WeaponMotion.WithDomains(SkillTrajectoryDomain.Melee)
            .AddSemantics(DeliveryTag.Melee, MotionTag.MeleeSweep);
        WeaponMotion.OnInit = entity => entity.AddComponent(new WeaponControlMotionState());
        WeaponMotion.Action = UpdateMotion;
    }

    /// <summary>根据执行体快照推进当前武器动作，并在武器失效时立即停止碰撞。</summary>
    private static void UpdateMotion(
        ref SkillContext context,
        ref Position position,
        ref Rotation rotation,
        Entity entity,
        float deltaTime)
    {
        Actor owner = context.SourceObj?.a;
        ref WeaponControlMotionState state = ref entity.GetComponent<WeaponControlMotionState>();
        if (!WeaponControlRuntimeService.IsCurrentWeapon(owner, state.Weapon) || owner.isFlying())
        {
            DisableAndRecycle(entity);
            return;
        }

        state.Elapsed += deltaTime;
        float duration = Mathf.Max(0.05f, state.Duration);
        float progress = Mathf.Clamp01(state.Elapsed / duration);
        Vector3 direction = Normalize(state.Direction, context.TargetDir);
        switch (state.Kind)
        {
            case WeaponControlMotionKind.Thrust:
                UpdateThrust(owner, entity, ref state, ref position, ref rotation, direction, progress);
                break;
            case WeaponControlMotionKind.Crush:
                UpdateCrush(owner, entity, ref state, ref position, ref rotation, direction, progress);
                break;
            default:
                UpdateSweep(owner, entity, ref state, ref position, ref rotation, direction, progress);
                break;
        }

        if (progress >= 1f) ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
    }

    /// <summary>沿角色周围的大角度弧线平滑挥动武器。</summary>
    private static void UpdateSweep(
        Actor owner,
        Entity entity,
        ref WeaponControlMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, progress >= 0.08f && progress <= 0.92f);
        float eased = Smooth(progress);
        float angle = Mathf.Lerp(state.StartAngle, state.EndAngle, eased);
        Vector3 radial = Normalize(Quaternion.AngleAxis(angle, Vector3.forward) * direction, direction);
        float breathing = 0.88f + Mathf.Sin(progress * Mathf.PI) * 0.12f;
        position.value = owner.GetSimPos() + radial * state.Reach * breathing;
        rotation.value = radial;
        if (entity.HasComponent<AnimAfterimage>())
        {
            ref AnimAfterimage afterimage = ref entity.GetComponent<AnimAfterimage>();
            afterimage.ArcRadius = state.Reach;
            afterimage.ArcDirection = Mathf.Sign(state.EndAngle - state.StartAngle);
        }
    }

    /// <summary>先迅速送出武器，再以较慢节奏收回，形成清晰的水平戳刺。</summary>
    private static void UpdateThrust(
        Actor owner,
        Entity entity,
        ref WeaponControlMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        SetColliderEnabled(entity, progress >= 0.12f && progress <= 0.72f);
        float extension = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 0.72f);
        position.value = owner.GetSimPos() + direction * (0.35f + state.Reach * extension);
        rotation.value = direction;
    }

    /// <summary>将武器抬过头顶后加速砸向目标方向，碰撞只在下落阶段生效。</summary>
    private static void UpdateCrush(
        Actor owner,
        Entity entity,
        ref WeaponControlMotionState state,
        ref Position position,
        ref Rotation rotation,
        Vector3 direction,
        float progress)
    {
        bool impactPhase = progress >= 0.55f && progress <= 0.94f;
        SetColliderEnabled(entity, impactPhase);
        float angle = Mathf.Lerp(-112f, 24f, Smooth(progress));
        Vector3 radial = Normalize(Quaternion.AngleAxis(angle, Vector3.forward) * direction, direction);
        float height = Mathf.Sin(Mathf.Clamp01(progress / 0.78f) * Mathf.PI) * state.Reach * 0.95f;
        position.value = owner.GetSimPos() + radial * state.Reach * 0.82f;
        position.z += Mathf.Max(0f, height);
        rotation.value = radial;
        if (entity.HasComponent<AnimAfterimage>())
        {
            ref AnimAfterimage afterimage = ref entity.GetComponent<AnimAfterimage>();
            afterimage.ArcRadius = state.Reach * 0.82f;
            afterimage.ArcDirection = 1f;
        }
    }

    /// <summary>禁用实体碰撞并请求在本帧末回收。</summary>
    private static void DisableAndRecycle(Entity entity)
    {
        SetColliderEnabled(entity, false);
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
    }

    /// <summary>切换执行体碰撞开关，不改变其命中对象类别。</summary>
    private static void SetColliderEnabled(Entity entity, bool enabled)
    {
        ref ColliderConfig collider = ref entity.GetComponent<ColliderConfig>();
        collider.Enabled = enabled;
    }

    /// <summary>应用无过冲的三次平滑插值。</summary>
    private static float Smooth(float value)
    {
        return value * value * (3f - 2f * value);
    }

    /// <summary>在方向过小时使用稳定回退方向。</summary>
    private static Vector3 Normalize(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude > 0.0001f) return value.normalized;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.right;
    }
}
