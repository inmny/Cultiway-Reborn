using System.Collections.Concurrent;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Performance;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.WeaponControl;

/// <summary>远程御器借用实际装备。放箭和两个视角都读取本次连续运动，装备仍在原槽中。</summary>
internal sealed class WeaponControlRangedFlight
{
    internal const float ReturnSeconds = .26f;
    private static readonly ConcurrentDictionary<long, WeaponControlRangedFlight> active = new();

    private readonly Actor owner;
    private readonly Item weapon;
    private readonly long ownerId, weaponId;
    private readonly SpatialVisualMesh model;
    private readonly double started;
    private readonly float liftSeconds, holdHeight;
    private readonly Vector2 holdOffset;
    private readonly (Vector3 Point, Quaternion Facing) takeoff;
    private double? returning;
    private (Vector3 Point, Quaternion Facing) returnFrom;

    private WeaponControlRangedFlight(Actor owner, Item weapon, Sprite sprite, WeaponControlCastMode mode, float holdHeight)
    {
        this.owner = owner;
        this.weapon = weapon;
        ownerId = owner.id;
        weaponId = weapon.data.id;
        model = SpatialSpriteMesh.Get(sprite);
        started = SimulationTime.DiagnosticTime;
        liftSeconds = LiftSeconds(mode);
        holdOffset = mode == WeaponControlCastMode.ArrowRain ? new Vector2(0f, .22f) : Vector2.zero;
        takeoff = Hand();
        this.holdHeight = mode == WeaponControlCastMode.ArrowRain
            ? Mathf.Max(holdHeight, takeoff.Point.z - owner.position_height + .27f)
            : holdHeight;
    }

    /// <summary>第一发等待弓到达发射姿态，之后仍沿用原逐发时间间隔。</summary>
    internal static float LiftSeconds(WeaponControlCastMode mode) => mode == WeaponControlCastMode.SkyVolley ? .24f : .14f;

    internal static WeaponControlRangedFlight TryBegin(Actor owner, Item weapon, EquipmentAsset asset,
        WeaponControlCastMode mode, float holdHeight)
    {
        Sprite sprite = EquippedWeaponVisualService.ResolveSprite(owner, asset);
        if (sprite == null) return null;
        var flight = new WeaponControlRangedFlight(owner, weapon, sprite, mode, holdHeight);
        active[owner.id] = flight;
        return flight;
    }

    private bool IsCurrent => owner?.data?.id == ownerId && owner.isAlive() && weapon?.data?.id == weaponId &&
        EquippedWeaponVisualService.IsCurrent(owner, weapon);

    private bool IsBorrowed(double now) => IsCurrent && (!returning.HasValue || now < returning.Value + ReturnSeconds);

    /// <summary>同款换装也立即解除旧器物的隐藏和控制。</summary>
    internal static bool IsBorrowed(Actor actor) => actor?.data != null &&
        active.TryGetValue(actor.data.id, out WeaponControlRangedFlight flight) &&
        ReferenceEquals(flight.owner, actor) && flight.IsBorrowed(SimulationTime.DiagnosticTime);

    internal static void Clear() => active.Clear();

    internal bool ReadyToRelease => SimulationTime.DiagnosticTime >= started + liftSeconds;

    internal void End()
    {
        if (!IsCurrent)
        {
            active.TryRemove(ownerId, out _);
            return;
        }
        if (returning.HasValue) return;
        double now = SimulationTime.DiagnosticTime;
        returnFrom = Sample(now);
        returning = now;
    }

    internal Vector3 LaunchPoint() => Sample(SimulationTime.DiagnosticTime).Point;

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
            WeaponControlRangedFlight flight = entry.Value;
            if (!flight.IsBorrowed(now))
            {
                active.TryRemove(entry.Key, out _);
                continue;
            }
            var pose = flight.Sample(now);
            if (!SpatialVisualEffects.IsRelevant(pose.Point, 1f)) continue;
            float scale = Mathf.Max(.05f, flight.owner.actor_scale);
            meshes.Add(new SpatialVisualMeshInstance(flight.model, pose.Point, pose.Facing, Vector3.one * scale,
                Color.white, drawNative: true));
        }
    }
}
