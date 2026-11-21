using System.Collections.Concurrent;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Performance;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.WeaponControl;

/// <summary>御器借用实际装备。发射点、近战接触和两个视角读取同一件器物的姿态。</summary>
internal sealed class WeaponControlFlight
{
    internal const float ReturnSeconds = .26f;
    private static readonly ConcurrentDictionary<long, WeaponControlFlight> active = new();

    private readonly Actor owner;
    private readonly Item weapon;
    private readonly long ownerId, weaponId;
    private readonly SpatialVisualMesh model;
    private readonly WeaponControlCastMode mode;
    private readonly float scale, spriteAngle;
    private readonly Vector3 strikingEnd;
    private readonly double started;
    private readonly float liftSeconds, holdHeight;
    private readonly Vector2 holdOffset;
    private readonly (Vector3 Point, Quaternion Facing) takeoff;
    private double? returning;
    private (Vector3 Point, Quaternion Facing) returnFrom;
    private (Vector3 Point, Quaternion Facing) meleePose, strikeFrom;
    private bool striking, ending;

    private WeaponControlFlight(Actor owner, Item weapon, Sprite sprite, WeaponControlCastMode mode, float holdHeight)
    {
        this.owner = owner;
        this.weapon = weapon;
        ownerId = owner.id;
        weaponId = weapon.data.id;
        this.mode = mode;
        model = SpatialSpriteMesh.Get(sprite);
        scale = Mathf.Max(.05f, owner.actor_scale);
        spriteAngle = EquippedWeaponVisualService.ResolveSpriteAngle(sprite);
        Bounds bounds = model.Bounds;
        bool horizontal = spriteAngle == 0f;
        Vector3 edge = horizontal ? Vector3.right : Vector3.up;
        float length = horizontal ? bounds.size.x : bounds.size.y;
        float width = horizontal ? bounds.size.y : bounds.size.x;
        ContactRadius = Mathf.Min(length * .28f, width * .5f) * scale;
        strikingEnd = (bounds.center + edge * length * .5f) * scale - edge * ContactRadius;
        started = SimulationTime.DiagnosticTime;
        liftSeconds = LiftSeconds(mode);
        holdOffset = mode == WeaponControlCastMode.ArrowRain ? new Vector2(0f, .22f) : Vector2.zero;
        takeoff = Hand();
        meleePose = takeoff;
        this.holdHeight = mode == WeaponControlCastMode.ArrowRain
            ? Mathf.Max(holdHeight, takeoff.Point.z - owner.position_height + .27f)
            : holdHeight;
    }

    /// <summary>第一发等待弓到达发射姿态，之后仍沿用原逐发时间间隔。</summary>
    internal static float LiftSeconds(WeaponControlCastMode mode) => mode switch
    {
        WeaponControlCastMode.SkyVolley => .24f,
        WeaponControlCastMode.ArrowRain => .14f,
        _ => 0f,
    };

    internal static WeaponControlFlight TryBegin(Actor owner, Item weapon, EquipmentAsset asset,
        WeaponControlCastMode mode, float holdHeight)
    {
        Sprite sprite = EquippedWeaponVisualService.ResolveSprite(owner, asset);
        if (sprite == null) return null;
        var flight = new WeaponControlFlight(owner, weapon, sprite, mode, holdHeight);
        active[owner.id] = flight;
        return flight;
    }

    internal bool IsCurrent => owner?.data?.id == ownerId && owner.isAlive() && weapon?.data?.id == weaponId &&
        EquippedWeaponVisualService.IsCurrent(owner, weapon);

    private bool IsBorrowed(double now) => IsCurrent && (!returning.HasValue || now < returning.Value + ReturnSeconds);

    /// <summary>同款换装也立即解除旧器物的隐藏和控制。</summary>
    internal static bool IsBorrowed(Actor actor) => actor?.data != null &&
        active.TryGetValue(actor.data.id, out WeaponControlFlight flight) &&
        ReferenceEquals(flight.owner, actor) && flight.IsBorrowed(SimulationTime.DiagnosticTime);

    internal static void Clear() => active.Clear();

    internal bool ReadyToRelease => !striking && SimulationTime.DiagnosticTime >= started + liftSeconds;

    internal float ContactRadius { get; }

    internal Vector3 ContactPoint => meleePose.Point + meleePose.Facing * strikingEnd;

    internal void End()
    {
        if (!IsCurrent)
        {
            active.TryRemove(ownerId, out _);
            return;
        }
        ending = true;
        if (!striking) BeginReturn();
    }

    private void BeginReturn()
    {
        if (returning.HasValue) return;
        double now = SimulationTime.DiagnosticTime;
        returnFrom = Sample(now);
        returning = now;
    }

    internal Vector3 LaunchPoint() => Sample(SimulationTime.DiagnosticTime).Point;

    internal void BeginStrike()
    {
        striking = true;
        strikeFrom = meleePose;
    }

    internal void FinishStrike()
    {
        striking = false;
        if (ending) End();
    }

    /// <summary>先从上一姿态连续起势，再让可见的刃端、枪尖或锤头走实际接触路线。</summary>
    internal bool MoveStrike(in EquippedWeaponMotionState motion, float progress)
    {
        Vector3 foot = SpatialActorPose.Foot(owner);
        float angle = Mathf.Atan2(motion.Direction.y, motion.Direction.x) * Mathf.Rad2Deg;
        float phase = Mathf.InverseLerp(.22f, .86f, progress);
        float reach = motion.Reach;
        float height = foot.z + SpatialActorPose.Height(owner) * .42f;
        float pitch = 0f;
        Vector3? sweepPoint = null;
        switch (mode)
        {
            case WeaponControlCastMode.MeleeThrust:
                float extension = Mathf.Min(phase / .30f, (1f - phase) / .30f);
                reach = Mathf.SmoothStep(.38f, reach, extension);
                pitch = -6f;
                break;
            case WeaponControlCastMode.MeleeCrush:
                float fall = Mathf.Clamp01(phase / .75f);
                angle += Mathf.Sign(motion.StartAngle) * Mathf.SmoothStep(42f, 0f, fall);
                reach *= Mathf.Lerp(.76f, 1f, fall);
                height += Mathf.Pow(1f - fall, 2f) * Mathf.Min(2f, motion.Reach * .75f);
                pitch = Mathf.SmoothStep(-58f, 46f, fall);
                break;
            default:
                float turnAngle = Mathf.SmoothStep(motion.StartAngle, motion.EndAngle, phase);
                float arc = turnAngle * Mathf.Deg2Rad;
                float forward = Mathf.Lerp(.62f, 1f, Mathf.Cos(arc) / .66f);
                sweepPoint = foot + Quaternion.AngleAxis(angle, Vector3.forward) *
                    new Vector3(forward * reach, Mathf.Sin(arc) * reach, 0f);
                angle += turnAngle;
                pitch = Mathf.Lerp(-9f, 12f, phase);
                break;
        }
        Quaternion turn = Quaternion.AngleAxis(angle, Vector3.forward);
        Quaternion facing = turn * Quaternion.Euler(36f, pitch, 0f) * Quaternion.AngleAxis(spriteAngle, Vector3.forward);
        Vector3 contact = sweepPoint ?? foot + turn * Vector3.right * reach;
        contact.z = height;
        meleePose = Interpolate(strikeFrom, (contact - facing * strikingEnd, facing), progress / .22f);
        return mode switch
        {
            WeaponControlCastMode.MeleeThrust => progress is >= .28f and <= .72f,
            WeaponControlCastMode.MeleeCrush => progress is >= .63f and <= .86f,
            _ => progress is >= .22f and <= .86f,
        };
    }

    /// <summary>原版安装点使用人物当前动画帧和实际比例，缺少挂点时沿用共用手部估计。</summary>
    private (Vector3 Point, Quaternion Facing) Hand()
    {
        Quaternion facing = Quaternion.Euler(90f, 0f, owner.flip ? 180f : 0f);
        if (owner.getAnimationFrameData() is not { } frame)
            return (SpatialActorPose.Hand(owner, owner.flip ? Vector3.left : Vector3.right, .15f), facing);
        Vector2 mount = frame.pos_item;
        Vector3 foot = SpatialActorPose.Foot(owner);
        foot.x += mount.x * owner.current_scale.x;
        foot.z += mount.y * owner.current_scale.y - .01f;
        return (foot, facing);
    }

    private (Vector3 Point, Quaternion Facing) Sample(double now)
    {
        if (returning.HasValue) return Interpolate(returnFrom, Hand(), (float)(now - returning.Value) / ReturnSeconds);
        if (liftSeconds == 0f) return meleePose;
        var foot = SpatialActorPose.Foot(owner);
        Vector3 hold = foot + new Vector3(holdOffset.x, holdOffset.y, holdHeight);
        Quaternion aim = Quaternion.Euler(90f, 0f, owner.flip ? 180f : 0f);
        return Interpolate(takeoff, (hold, aim), (float)(now - started) / liftSeconds);
    }

    private static (Vector3 Point, Quaternion Facing) Interpolate((Vector3 Point, Quaternion Facing) first,
        (Vector3 Point, Quaternion Facing) last, float portion)
    {
        float t = Mathf.SmoothStep(0f, 1f, portion);
        return (Vector3.LerpUnclamped(first.Point, last.Point, t), Quaternion.SlerpUnclamped(first.Facing, last.Facing, t));
    }

    /// <summary>共用画面只输出一件原器物。同一世界时间内暂停或换镜头都不推进运动。</summary>
    internal static void AppendSpatial(List<SpatialVisualMeshInstance> meshes)
    {
        double now = SimulationTime.DiagnosticTime;
        foreach (var entry in active)
        {
            WeaponControlFlight flight = entry.Value;
            if (!flight.IsBorrowed(now))
            {
                active.TryRemove(entry.Key, out _);
                continue;
            }
            var pose = flight.Sample(now);
            if (!SpatialVisualEffects.IsRelevant(pose.Point, 1f)) continue;
            float size = flight.liftSeconds == 0f ? flight.scale : Mathf.Max(.05f, flight.owner.actor_scale);
            meshes.Add(new SpatialVisualMeshInstance(flight.model, pose.Point, pose.Facing, Vector3.one * size,
                Color.white, drawNative: true));
        }
    }
}
