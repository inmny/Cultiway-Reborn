using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3;
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

/// <summary>御器和骑士战技保存及选择时使用的轨迹登记。</summary>
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
    }
}
