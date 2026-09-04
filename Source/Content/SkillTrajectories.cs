using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;
using MotionTag = Cultiway.Core.Semantics.SkillSemantics.Motion;

namespace Cultiway.Content;

public class SkillTrajectories : ExtendLibrary<TrajectoryAsset, SkillTrajectories>
{
    private const float TwoPi = 6.2831855f;

    public static TrajectoryAsset TowardsDirection { get; private set; }
    public static TrajectoryAsset TowardsDirectionNoRot { get; private set; }
    public static TrajectoryAsset TowardsPosition { get; private set; }
    public static TrajectoryAsset TowardsTarget { get; private set; }
    public static TrajectoryAsset DriftHoming { get; private set; }
    public static TrajectoryAsset SineWave { get; private set; }
    public static TrajectoryAsset Zigzag { get; private set; }
    public static TrajectoryAsset SpiralHoming { get; private set; }
    public static TrajectoryAsset OrbitTarget { get; private set; }
    public static TrajectoryAsset Boomerang { get; private set; }
    public static TrajectoryAsset SlowVortex { get; private set; }
    public static TrajectoryAsset ArcToPosition { get; private set; }
    public static TrajectoryAsset FallingStrike { get; private set; }
    public static TrajectoryAsset GroundCrawl { get; private set; }
    public static TrajectoryAsset LightningSnap { get; private set; }
    public static TrajectoryAsset RainFall { get; private set; }
    public static TrajectoryAsset AppearAtTarget { get; private set; }
    public static TrajectoryAsset GroundEruptAtTarget { get; private set; }
    public static TrajectoryAsset FieldAtTarget { get; private set; }
    public static TrajectoryAsset FieldAdvance { get; private set; }
    public static TrajectoryAsset FieldFollowTarget { get; private set; }
    public static TrajectoryAsset InstantBeam { get; private set; }
    public static TrajectoryAsset TrackingBeam { get; private set; }
    public static TrajectoryAsset ChainTargets { get; private set; }
    public static TrajectoryAsset WallBetweenCasterAndTarget { get; private set; }
    public static TrajectoryAsset ShieldOnCaster { get; private set; }
    public static TrajectoryAsset MeleeSweep { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        SetupTowardsDirection();
        SetupTowardsDirectionNoRot();
        SetupTowardsPosition();
        SetupTowardsTarget();
        SetupDriftHoming();
        SetupSineWave();
        SetupZigzag();
        SetupSpiralHoming();
        SetupOrbitTarget();
        SetupBoomerang();
        SetupSlowVortex();
        SetupArcToPosition();
        SetupFallingStrike();
        SetupGroundCrawl();
        SetupLightningSnap();
        SetupRainFall();
        SetupAppearAtTarget();
        SetupGroundEruptAtTarget();
        SetupFieldAtTarget();
        SetupFieldAdvance();
        SetupFieldFollowTarget();
        SetupInstantBeam();
        SetupTrackingBeam();
        SetupChainTargets();
        SetupWallBetweenCasterAndTarget();
        SetupShieldOnCaster();
        SetupMeleeSweep();
        ConfigureEditorMetadata();
    }

    private static void ConfigureEditorMetadata()
    {
        var flying = SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.FlyingWave;
        ConfigureEditor(TowardsDirection, flying, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(TowardsDirectionNoRot, flying, false, true,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(TowardsPosition, flying, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(TowardsTarget, flying, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(DriftHoming, flying, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(SineWave, flying, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(Zigzag, flying, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(SpiralHoming, SkillTrajectoryDomain.FlyingBody, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(OrbitTarget, SkillTrajectoryDomain.FlyingBody, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(Boomerang, SkillTrajectoryDomain.FlyingBody, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(SlowVortex, SkillTrajectoryDomain.FlyingBody | SkillTrajectoryDomain.MobileField, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(ArcToPosition, SkillTrajectoryDomain.Ballistic, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(FallingStrike, SkillTrajectoryDomain.Skyfall, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(GroundCrawl, SkillTrajectoryDomain.GroundTravel, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(LightningSnap, SkillTrajectoryDomain.FlyingBody, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(RainFall, SkillTrajectoryDomain.Skyfall, true, false, SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(AppearAtTarget, SkillTrajectoryDomain.TargetManifest, true, false,
            SkillEditorCompatibilityKeys.Instant,
            SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(GroundEruptAtTarget, SkillTrajectoryDomain.GroundManifest, true, false,
            SkillEditorCompatibilityKeys.Instant, SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(FieldAtTarget, SkillTrajectoryDomain.StationaryField, true, false,
            SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(FieldAdvance, SkillTrajectoryDomain.MobileField, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(FieldFollowTarget, SkillTrajectoryDomain.MobileField, true, false,
            SkillEditorCompatibilityKeys.Travel);
        ConfigureEditor(InstantBeam, SkillTrajectoryDomain.Beam, true, false,
            SkillEditorCompatibilityKeys.Instant, SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(TrackingBeam, SkillTrajectoryDomain.Beam, true, false,
            SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(ChainTargets, SkillTrajectoryDomain.Chain, true, false,
            SkillEditorCompatibilityKeys.Instant, SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(WallBetweenCasterAndTarget, SkillTrajectoryDomain.Barrier, true, false,
            SkillEditorCompatibilityKeys.Static);
        ConfigureEditor(ShieldOnCaster, SkillTrajectoryDomain.Aura, true, false,
            SkillEditorCompatibilityKeys.Static);
        // 当前没有真正的近身斩击实体，先保留轨迹实现但不向编辑器开放。
        ConfigureEditor(MeleeSweep, SkillTrajectoryDomain.Melee, false, false, SkillEditorCompatibilityKeys.Travel);
    }

    private static void ConfigureEditor(TrajectoryAsset trajectory, SkillTrajectoryDomain domains, bool selectable,
        bool persistWhenHidden, params string[] compatibilityKeys)
    {
        trajectory.WithDomains(domains);
        trajectory.EditorDescriptionKey = $"{trajectory.id}.Description";
        trajectory.EditorSortOrder = ModClass.I.SkillV3.TrajLib.list.IndexOf(trajectory);
        trajectory.EditorSelectable = selectable;
        trajectory.EditorPersistWhenHidden = persistWhenHidden;
        trajectory.EditorCompatibilityKeys.UnionWith(compatibilityKeys);
    }

    private static void SetupTowardsDirection()
    {
        TowardsDirection.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            var targetDir = SafeNormalized(context.TargetDir, rot.value);
            var currentDir = SafeNormalized(rot.value, targetDir);

            if (Vector3.Dot(currentDir, targetDir) < 0.9999f)
            {
                rot.value = SmoothTurn(currentDir, targetDir, GetTurnRate(e, 180f) * dt);
            }
            else
            {
                rot.value = targetDir;
            }

            pos.value += SafeNormalized(rot.value, targetDir) * dt * GetVelocity(e, 32f, dt);
        };
        TowardsDirection.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            EnsureTurnRate(e, 180f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        TowardsDirection.AddSemantics(MotionTag.Direct);
    }

    private static void SetupTowardsDirectionNoRot()
    {
        TowardsDirectionNoRot.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e,
            float dt) =>
        {
            pos.value += SafeNormalized(rot.value, context.TargetDir) * dt * GetVelocity(e, 32f, dt);
        };
        TowardsDirectionNoRot.CanBeSelectedByModifier = false;
        TowardsDirectionNoRot.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        TowardsDirectionNoRot.AddSemantics(MotionTag.Direct);
    }

    private static void SetupTowardsPosition()
    {
        TowardsPosition.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            MoveSmoothlyTo(context.TargetPos, ref pos, ref rot, e, dt, 20f, 180f);
        };
        TowardsPosition.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            EnsureTurnRate(e, 180f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        TowardsPosition.AddSemantics(MotionTag.Direct);
    }

    private static void SetupTowardsTarget()
    {
        TowardsTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            MoveSmoothlyTo(GetTargetPos(ref context), ref pos, ref rot, e, dt, 20f, 180f);
        };
        TowardsTarget.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            EnsureTurnRate(e, 180f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        TowardsTarget.AddSemantics(MotionTag.Homing);
    }

    private static void SetupDriftHoming()
    {
        DriftHoming.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            var target = GetTargetPos(ref context);
            var step = GetVelocity(e, 35f, dt) * dt;
            if (TryHandleTargetArrival(target, ref pos, ref rot, e, ref state, step)) return;

            var targetDir = DirectionTo(target, pos.value, context.TargetDir);
            var side = PerpendicularInPlane(targetDir);
            var drift = Mathf.Lerp(0.65f, 0f, Mathf.Clamp01(state.Elapsed / 0.75f));
            var desired = SafeNormalized(targetDir + side * Mathf.Sign(state.Phase) * drift, targetDir);
            var current = SafeNormalized(rot.value, desired);

            rot.value = SmoothTurn(current, desired, GetTurnRate(e, 220f) * dt);
            pos.value += SafeNormalized(rot.value, desired) * step;
        };
        DriftHoming.OnInit = e =>
        {
            EnsureVelocity(e, 35f);
            EnsureTurnRate(e, 220f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        DriftHoming.AddSemantics(MotionTag.Homing);
    }

    private static void SetupSineWave()
    {
        SineWave.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            var speed = GetVelocity(e, 30f, dt);
            state.DistanceTravelled += speed * dt;

            var wave = GetParams(e, TrajectoryKind.Wave,
                new TrajectoryParams { Kind = TrajectoryKind.Wave, Amplitude = 0.6f, Frequency = 3.5f, Phase = state.Phase });
            var baseDir = SafeNormalized(state.StartDirection, context.TargetDir);
            var side = PerpendicularInPlane(baseDir);
            var spatialTime = state.DistanceTravelled / 30f;
            var forward = baseDir * state.DistanceTravelled;
            var sideOffset = side * (Mathf.Sin(spatialTime * wave.Frequency * TwoPi + wave.Phase + state.Phase)
                                     * wave.Amplitude);
            var next = state.StartPosition + forward + sideOffset;

            rot.value = DirectionTo(next, pos.value, baseDir);
            pos.value = next;
        };
        SineWave.OnInit = e =>
        {
            EnsureVelocity(e, 30f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Wave,
                Amplitude = 0.65f,
                Frequency = 3.2f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        SineWave.AddSemantics(MotionTag.Wave);
    }

    private static void SetupZigzag()
    {
        Zigzag.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            var speed = GetVelocity(e, 34f, dt);
            state.DistanceTravelled += speed * dt;

            var zigzag = GetParams(e, TrajectoryKind.Zigzag,
                new TrajectoryParams { Kind = TrajectoryKind.Zigzag, SideAmplitude = 0.75f, SegmentDuration = 0.12f });
            var baseDir = SafeNormalized(state.StartDirection, context.TargetDir);
            var side = PerpendicularInPlane(baseDir);
            var segmentDuration = Mathf.Max(0.03f, zigzag.SegmentDuration);
            var spatialTime = state.DistanceTravelled / 34f;
            var sideT = Mathf.PingPong(spatialTime / segmentDuration, 1f) * 2f - 1f;
            var next = state.StartPosition
                       + baseDir * state.DistanceTravelled
                       + side * (sideT * zigzag.SideAmplitude);

            rot.value = DirectionTo(next, pos.value, baseDir);
            pos.value = next;
        };
        Zigzag.OnInit = e =>
        {
            EnsureVelocity(e, 34f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Zigzag,
                SideAmplitude = 0.75f,
                SegmentDuration = 0.12f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        Zigzag.AddSemantics(MotionTag.Zigzag);
    }

    private static void SetupSpiralHoming()
    {
        SpiralHoming.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            var speed = GetVelocity(e, 30f, dt);
            var step = speed * dt;
            state.DistanceTravelled += step;
            var target = GetTargetPos(ref context);
            if (TryHandleTargetArrival(target, ref pos, ref rot, e, ref state, step)) return;

            var spiral = GetParams(e, TrajectoryKind.Spiral,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.Spiral,
                    Radius = 0.7f,
                    Frequency = 4f,
                    RadiusDamping = 0.75f,
                    HomingStrength = 0.7f
                });
            var targetDir = DirectionTo(target, pos.value, context.TargetDir);
            var current = SafeNormalized(rot.value, targetDir);
            var homing = Mathf.Clamp01(spiral.HomingStrength);
            var baseDir = SmoothTurn(current, targetDir, GetTurnRate(e, 240f) * homing * dt);
            var side = PerpendicularInPlane(baseDir);
            var spatialTime = state.DistanceTravelled / 30f;
            var radius = spiral.Radius * Mathf.Exp(-Mathf.Max(0f, spiral.RadiusDamping) * spatialTime);
            var angle = spatialTime * spiral.Frequency * TwoPi + state.Phase;
            var swirl = side * (Mathf.Sin(angle) * radius);
            var desired = SafeNormalized(baseDir + swirl, baseDir);

            rot.value = desired;
            pos.value += desired * step;
            pos.z += Mathf.Cos(angle) * radius * 0.04f;
        };
        SpiralHoming.OnInit = e =>
        {
            EnsureVelocity(e, 30f);
            EnsureTurnRate(e, 240f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Spiral,
                Radius = 0.7f,
                Frequency = 4f,
                RadiusDamping = 0.75f,
                HomingStrength = 0.7f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        SpiralHoming.AddSemantics(MotionTag.Spiral, MotionTag.Homing);
    }

    private static void SetupOrbitTarget()
    {
        OrbitTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var orbit = GetParams(e, TrajectoryKind.Orbit,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.Orbit,
                    StartRadius = 2.2f,
                    AngularSpeed = 480f,
                    ShrinkSpeed = 1.8f,
                    HomingStrength = 1f
                });
            var target = GetTargetPos(ref context);
            var radius = Mathf.Max(0f, orbit.StartRadius - orbit.ShrinkSpeed * state.Elapsed);
            var angle = state.Phase + orbit.AngularSpeed * Mathf.Deg2Rad * state.Elapsed;
            var desired = target + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            if (radius <= 0.2f)
            {
                desired = target;
            }

            var moveDir = DirectionTo(desired, pos.value, context.TargetDir);
            var speed = GetVelocity(e, 32f, dt) * Mathf.Lerp(1f, 1.35f, Mathf.Clamp01(orbit.HomingStrength));
            pos.value = Vector3.MoveTowards(pos.value, desired, speed * dt);
            rot.value = moveDir;
        };
        OrbitTarget.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Orbit,
                StartRadius = 2.2f,
                AngularSpeed = 480f,
                ShrinkSpeed = 1.8f,
                HomingStrength = 1f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        OrbitTarget.AddSemantics(MotionTag.Orbit, MotionTag.Homing);
    }

    private static void SetupBoomerang()
    {
        Boomerang.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var boomerang = GetParams(e, TrajectoryKind.Boomerang,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.Boomerang,
                    OutDistance = 5f,
                    ReturnTurnRate = 520f,
                    MaxLifetime = 1.2f
                });
            if (!state.Returning && Vector3.Distance(state.StartPosition, pos.value) >= boomerang.OutDistance)
            {
                state.Returning = true;
            }

            var step = GetVelocity(e, 35f, dt) * dt;
            var returnTarget = context.SourceObj.GetSimPos();
            if (state.Returning && Vector3.Distance(pos.value, returnTarget) <= step)
            {
                rot.value = DirectionTo(returnTarget, pos.value, rot.value);
                pos.value = returnTarget;
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
                return;
            }

            var targetDir = state.Returning
                ? DirectionTo(returnTarget, pos.value, context.TargetDir)
                : SafeNormalized(state.StartDirection + PerpendicularInPlane(state.StartDirection) * state.Phase * 0.15f,
                    state.StartDirection);
            var turnRate = state.Returning ? boomerang.ReturnTurnRate : GetTurnRate(e, 120f);
            rot.value = SmoothTurn(SafeNormalized(rot.value, targetDir), targetDir, turnRate * dt);
            pos.value += SafeNormalized(rot.value, targetDir) * step;

            if (state.Elapsed >= boomerang.MaxLifetime)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
            }
        };
        Boomerang.OnInit = e =>
        {
            EnsureVelocity(e, 35f);
            EnsureTurnRate(e, 120f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Boomerang,
                OutDistance = 5f,
                ReturnTurnRate = 520f,
                MaxLifetime = 1.2f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        Boomerang.AddSemantics(MotionTag.Return);
    }

    private static void SetupSlowVortex()
    {
        SlowVortex.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var vortex = GetParams(e, TrajectoryKind.Vortex,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.Vortex,
                    ForwardSpeed = 28f,
                    Radius = 0.9f,
                    AngularSpeed = 520f,
                    PulseAmplitude = 0.25f,
                    PulseFrequency = 2f
                });
            var baseDir = SafeNormalized(state.StartDirection, context.TargetDir);
            var side = PerpendicularInPlane(baseDir);
            var speed = GetVelocity(e, vortex.ForwardSpeed, dt);
            state.DistanceTravelled += speed * dt;
            var referenceTime = state.DistanceTravelled / Mathf.Max(1f, vortex.ForwardSpeed);
            var angle = state.Phase + vortex.AngularSpeed * Mathf.Deg2Rad * referenceTime;
            var radius = vortex.Radius
                         + Mathf.Sin(referenceTime * vortex.PulseFrequency * TwoPi + state.Phase)
                         * vortex.PulseAmplitude;
            var center = state.StartPosition + baseDir * state.DistanceTravelled;
            var next = center + side * (Mathf.Cos(angle) * radius) + baseDir * (Mathf.Sin(angle) * radius * 0.25f);

            rot.value = DirectionTo(next, pos.value, baseDir);
            pos.value = next;
        };
        SlowVortex.OnInit = e =>
        {
            EnsureVelocity(e, 28f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Vortex,
                ForwardSpeed = 28f,
                Radius = 0.9f,
                AngularSpeed = 520f,
                PulseAmplitude = 0.25f,
                PulseFrequency = 2f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        SlowVortex.AddSemantics(MotionTag.Vortex);
    }

    private static void SetupArcToPosition()
    {
        ArcToPosition.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var arc = GetParams(e, TrajectoryKind.Arc,
                new TrajectoryParams { Kind = TrajectoryKind.Arc, Duration = 0.5f, Height = 4f });
            var target = context.TargetPos;
            var speed = GetVelocity(e, 62f, dt);
            state.DistanceTravelled += speed * dt;
            var directDistance = Mathf.Max(0.1f, Vector3.Distance(state.StartPosition, target));
            var t = Mathf.Clamp01(state.DistanceTravelled / directDistance);
            if (state.Elapsed >= Mathf.Max(0.1f, arc.Duration)) t = 1f;
            var flat = Vector3.Lerp(state.StartPosition, target, t);
            flat.z = Mathf.Lerp(state.StartPosition.z, target.z, t) + Mathf.Sin(t * Mathf.PI) * arc.Height;
            SetHeightGate(e, target.z + 0.55f);

            rot.value = DirectionTo(flat, pos.value, context.TargetDir);
            pos.value = flat;

            if (t >= 1f)
            {
                if (!SkillImpactRuntime.RequestPositionImpact(e))
                {
                    ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
                }
            }
        };
        ArcToPosition.OnInit = e =>
        {
            EnsureVelocity(e, 30f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Arc,
                Duration = 0.5f,
                Height = 4f
            });
            SetHeightGate(e, 0.55f);
            ResetRuntimeState(e);
        };
        ArcToPosition.AddSemantics(MotionTag.Falling);
    }

    private static void SetupFallingStrike()
    {
        FallingStrike.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            var falling = GetParams(e, TrajectoryKind.Falling,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.Falling,
                    StartHeight = 7f,
                    FallSpeed = 28f,
                    DriftSpeed = 7f,
                    ImpactHeight = 0.35f
                });

            if (state.Elapsed <= 0f)
            {
                var target = GetTargetPos(ref context);
                pos.value = new Vector3(target.x, target.y, target.z + falling.StartHeight);
                state.StartPosition = pos.value;
            }

            state.Elapsed += dt;
            var targetPos = GetTargetPos(ref context);
            var fallSpeed = Mathf.Max(falling.FallSpeed, GetVelocity(e, 62f, dt));
            var targetFlat = new Vector3(targetPos.x, targetPos.y, pos.z);
            pos.value = Vector3.MoveTowards(pos.value, targetFlat, falling.DriftSpeed * dt);
            pos.z -= fallSpeed * dt;
            if (pos.z < targetPos.z)
            {
                pos.z = targetPos.z;
            }
            rot.value = SafeNormalized(new Vector3(0f, -0.2f, -1f), Vector3.down);

            if (pos.z <= targetPos.z + falling.ImpactHeight)
            {
                SetHeightGate(e, targetPos.z + falling.ImpactHeight);
            }
            if (state.Elapsed >= falling.StartHeight / Mathf.Max(0.01f, fallSpeed) + 0.12f)
            {
                if (!SkillImpactRuntime.RequestPositionImpact(e))
                {
                    ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
                }
            }
        };
        FallingStrike.OnInit = e =>
        {
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Falling,
                StartHeight = 7f,
                FallSpeed = 28f,
                DriftSpeed = 7f,
                ImpactHeight = 0.35f
            });
            SetHeightGate(e, 0.35f);
            ResetRuntimeState(e);
        };
        FallingStrike.WithOrientations(TrajectoryOrientation.Vertical)
            .AddSemantics(MotionTag.Falling);
    }

    private static void SetupGroundCrawl()
    {
        GroundCrawl.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            var target = GetTargetPos(ref context);
            var movementTarget = new Vector3(target.x, target.y, pos.z);
            var step = GetVelocity(e, 20f, dt) * dt;
            if (TryHandleTargetArrival(movementTarget, ref pos, ref rot, e, ref state, step))
            {
                pos.z = Mathf.Max(0f, target.z * 0.15f);
                return;
            }

            var targetDir = DirectionTo(movementTarget, pos.value, context.TargetDir);
            var side = PerpendicularInPlane(targetDir);
            var sway = Mathf.Sin(state.Elapsed * TwoPi * 2.2f + state.Phase) * 0.2f;
            var desired = SafeNormalized(targetDir + side * sway, targetDir);
            rot.value = SmoothTurn(SafeNormalized(rot.value, desired), desired, GetTurnRate(e, 120f) * dt);
            pos.value += SafeNormalized(rot.value, desired) * step;
            pos.z = Mathf.Max(0f, target.z * 0.15f);
        };
        GroundCrawl.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            EnsureTurnRate(e, 120f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        GroundCrawl.WithOrientations(TrajectoryOrientation.Ground)
            .AddSemantics(MotionTag.Ground);
    }

    private static void SetupLightningSnap()
    {
        LightningSnap.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            var speed = GetVelocity(e, 100f, dt);
            state.Timer -= dt;
            if (state.Timer > 0f) return;

            var snap = GetParams(e, TrajectoryKind.LightningSnap,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.LightningSnap,
                    StepInterval = 0.025f,
                    StepDistance = 2.5f,
                    JitterRadius = 0.45f,
                    HitDistance = 1.2f
                });
            var stepInterval = Mathf.Max(0.01f, snap.StepInterval);
            var stepDistance = Mathf.Max(snap.StepDistance, speed * stepInterval);
            state.Timer = stepInterval;

            var target = GetTargetPos(ref context);
            var toTarget = target - pos.value;
            var distance = toTarget.magnitude;
            var dir = SafeNormalized(toTarget, context.TargetDir);
            if (distance <= stepDistance + snap.HitDistance)
            {
                pos.value = target;
                rot.value = dir;
                return;
            }

            var side = PerpendicularInPlane(dir);
            var jitter = side * Randy.randomFloat(-snap.JitterRadius, snap.JitterRadius);
            var next = pos.value + dir * stepDistance + jitter;
            rot.value = DirectionTo(next, pos.value, dir);
            pos.value = next;
        };
        LightningSnap.OnInit = e =>
        {
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.LightningSnap,
                StepInterval = 0.025f,
                StepDistance = 2.5f,
                JitterRadius = 0.45f,
                HitDistance = 1.2f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        // 折线闪击以水平推进为主，但抵达后即停、视觉接近原地显现，故同时声明 Appear。
        LightningSnap.WithOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Snap);
    }

    private static void SetupRainFall()
    {
        RainFall.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            var rain = GetParams(e, TrajectoryKind.RainFall,
                new TrajectoryParams
                {
                    Kind = TrajectoryKind.RainFall,
                    StartHeight = 8f,
                    FallSpeed = 32f,
                    DriftSpeed = 1.4f,
                    ImpactHeight = 0.35f
                });

            if (state.Elapsed <= 0f)
            {
                var target = GetTargetPos(ref context);
                var randomOffset = RandomInCircle(rain.DriftSpeed);
                pos.value = new Vector3(target.x + randomOffset.x, target.y + randomOffset.y,
                    target.z + rain.StartHeight);
                state.StartPosition = pos.value;
            }

            state.Elapsed += dt;
            var targetPos = GetTargetPos(ref context);
            var fallSpeed = Mathf.Max(rain.FallSpeed, GetVelocity(e, 62f, dt));
            pos.z -= fallSpeed * dt;
            if (pos.z < targetPos.z)
            {
                pos.z = targetPos.z;
            }
            rot.value = SafeNormalized(new Vector3(0f, -0.1f, -1f), Vector3.down);

            if (pos.z <= targetPos.z + rain.ImpactHeight)
            {
                SetHeightGate(e, targetPos.z + rain.ImpactHeight);
            }
            if (state.Elapsed >= rain.StartHeight / Mathf.Max(0.01f, fallSpeed) + 0.1f)
            {
                if (!SkillImpactRuntime.RequestPositionImpact(e))
                {
                    ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
                }
            }
        };
        RainFall.OnInit = e =>
        {
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.RainFall,
                StartHeight = 8f,
                FallSpeed = 32f,
                DriftSpeed = 1.4f,
                ImpactHeight = 0.35f
            });
            SetHeightGate(e, 0.35f);
            ResetRuntimeState(e);
        };
        RainFall.WithOrientations(TrajectoryOrientation.Vertical)
            .AddSemantics(MotionTag.Rain, MotionTag.Falling);
    }

    private static void SetupAppearAtTarget()
    {
        // 原地显现：首帧直接把实体挪到目标位置，之后原地等待，直到动画播完/超时回收。
        // 适合动画本身即从上到下竖直播放、不应叠加额外位移的法术（例如落雷）。
        AppearAtTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            if (state.Elapsed <= 0f)
            {
                pos.value = GetTargetPos(ref context);
                state.StartPosition = pos.value;
                AnchorCollisionAtCurrentPosition(e, ref pos);
                SkillImpactRuntime.RequestPositionImpact(e);
            }

            state.Elapsed += dt;
            // 原地不动，方向保持朝下以贴合竖直播放动画。
            rot.value = SafeNormalized(new Vector3(0f, 0f, -1f), Vector3.down);
        };
        AppearAtTarget.OnInit = e =>
        {
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        AppearAtTarget.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear, MotionTag.Snap);
    }

    private static void SetupGroundEruptAtTarget()
    {
        GroundEruptAtTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e,
            float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            if (state.Elapsed <= 0f)
            {
                pos.value = GetTargetPos(ref context);
                state.StartPosition = pos.value;
                AnchorCollisionAtCurrentPosition(e, ref pos);
                SkillImpactRuntime.RequestPositionImpact(e);
            }
            state.Elapsed += dt;
            rot.value = Vector3.up;
        };
        GroundEruptAtTarget.OnInit = e =>
        {
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        GroundEruptAtTarget.WithOrientations(TrajectoryOrientation.Ground)
            .AddSemantics(MotionTag.GroundManifest, MotionTag.Ground, MotionTag.Appear);
    }

    private static void SetupFieldAtTarget()
    {
        FieldAtTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            if (state.Elapsed <= 0f)
            {
                pos.value = GetTargetPos(ref context);
                state.StartPosition = pos.value;
                AnchorCollisionAtCurrentPosition(e, ref pos);
            }
            state.Elapsed += dt;
            rot.value = Vector3.up;
        };
        FieldAtTarget.OnInit = e =>
        {
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        FieldAtTarget.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear);
    }

    private static void SetupFieldAdvance()
    {
        FieldAdvance.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            Vector3 direction = SafeNormalized(state.StartDirection, context.TargetDir);
            rot.value = direction;
            pos.value += direction * GetVelocity(e, 28f, dt) * 0.25f * dt;
        };
        FieldAdvance.OnInit = e =>
        {
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        FieldAdvance.WithOrientations(TrajectoryOrientation.Horizontal)
            .AddSemantics(MotionTag.Direct);
    }

    private static void SetupFieldFollowTarget()
    {
        FieldFollowTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e,
            float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;
            Vector3 target = GetTargetPos(ref context);
            Vector3 direction = DirectionTo(target, pos.value, context.TargetDir);
            pos.value = target;
            AnchorCollisionAtCurrentPosition(e, ref pos);
            rot.value = direction;
        };
        FieldFollowTarget.OnInit = e =>
        {
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        FieldFollowTarget.WithOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Homing);
    }

    private static void SetupInstantBeam()
    {
        InstantBeam.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref e.GetComponent<LinearTrajectoryState>();
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.Start = context.SourceObj.GetSimPos();
                state.End = GetTargetPos(ref context);
                state.BaseScale = e.GetComponent<Scale>().value;
            }
            PlaceLinear(e, state.Start, state.End, state.BaseScale, ref pos, ref rot);
        };
        InstantBeam.OnInit = e =>
        {
            SetOrAdd(e, new LinearTrajectoryState());
            ClearCollisionHeightGate(e);
        };
        InstantBeam.WithOrientations(TrajectoryOrientation.Horizontal)
            .AddSemantics(MotionTag.Snap, MotionTag.Direct);
    }

    private static void SetupTrackingBeam()
    {
        TrackingBeam.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref e.GetComponent<LinearTrajectoryState>();
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.BaseScale = e.GetComponent<Scale>().value;
            }
            state.Start = context.SourceObj.GetSimPos();
            state.End = GetTargetPos(ref context);
            PlaceLinear(e, state.Start, state.End, state.BaseScale, ref pos, ref rot);
        };
        TrackingBeam.OnInit = e =>
        {
            SetOrAdd(e, new LinearTrajectoryState());
            ClearCollisionHeightGate(e);
        };
        TrackingBeam.WithOrientations(TrajectoryOrientation.Horizontal)
            .AddSemantics(MotionTag.Homing);
    }

    private static void SetupChainTargets()
    {
        ChainTargets.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            if (state.Elapsed <= 0f)
            {
                pos.value = GetTargetPos(ref context);
                state.StartPosition = pos.value;
                AnchorCollisionAtCurrentPosition(e, ref pos);
            }
            state.Elapsed += dt;
            rot.value = SafeNormalized(context.TargetDir, Vector3.right);
        };
        ChainTargets.OnInit = e =>
        {
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        ChainTargets.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Chain, MotionTag.Snap);
    }

    private static void SetupWallBetweenCasterAndTarget()
    {
        WallBetweenCasterAndTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e,
            float dt) =>
        {
            ref var state = ref e.GetComponent<LinearTrajectoryState>();
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.Start = context.SourceObj.GetSimPos();
                state.End = GetTargetPos(ref context);
                state.BaseScale = e.GetComponent<Scale>().value;
            }

            Vector3 forward = DirectionTo(state.End, state.Start, context.TargetDir);
            Vector3 wallDirection = PerpendicularInPlane(forward);
            pos.value = Vector3.Lerp(state.Start, state.End, 0.6f);
            rot.value = wallDirection;
            float length = e.GetComponent<SkillPersistentState>().Length;
            StretchVisual(e, length, state.BaseScale);
        };
        WallBetweenCasterAndTarget.OnInit = e =>
        {
            SetOrAdd(e, new LinearTrajectoryState());
            ClearCollisionHeightGate(e);
        };
        WallBetweenCasterAndTarget.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear);
    }

    private static void SetupShieldOnCaster()
    {
        ShieldOnCaster.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            if (context.SourceObj.isRekt())
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
                return;
            }
            pos.value = context.SourceObj.GetSimPos();
            rot.value = Vector3.up;
        };
        ShieldOnCaster.OnInit = e => ClearCollisionHeightGate(e);
        ShieldOnCaster.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear);
    }

    private static void SetupMeleeSweep()
    {
        MeleeSweep.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            if (context.SourceObj.isRekt())
            {
                ref var collider = ref e.GetComponent<ColliderConfig>();
                collider.Enabled = false;
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
                return;
            }

            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            ref var sweep = ref e.GetComponent<TrajectoryParams>();
            state.Elapsed += dt;

            var duration = Mathf.Max(0.01f, sweep.Duration);
            var progress = Mathf.Clamp01(state.Elapsed / duration);
            var easedProgress = progress * progress * (3f - 2f * progress);
            var angle = Mathf.Lerp(sweep.StartAngle, sweep.EndAngle, easedProgress);
            var radialDirection = Quaternion.AngleAxis(angle, Vector3.forward) * state.StartDirection;
            radialDirection = SafeNormalized(radialDirection, state.StartDirection);

            float radius = SkillEffectRadius.Resolve(e, sweep.Radius);
            pos.value = context.SourceObj.GetSimPos() + radialDirection * radius;
            rot.value = radialDirection;

            if (e.HasComponent<AnimAfterimage>())
            {
                ref var afterimage = ref e.GetComponent<AnimAfterimage>();
                afterimage.ArcRadius = radius;
                afterimage.ArcDirection = Mathf.Sign(sweep.EndAngle - sweep.StartAngle);
            }

            if (progress >= 1f)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
            }
        };
        MeleeSweep.OnInit = e =>
        {
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.MeleeSweep,
                Radius = 1.35f,
                StartAngle = -65f,
                EndAngle = 65f,
                Duration = 0.24f
            });
            SetOrAdd(e, SkillHitMemory.Create());
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
        MeleeSweep.WithOrientations(TrajectoryOrientation.Melee)
            .AddSemantics(MotionTag.MeleeSweep);
    }

    private static void MoveSmoothlyTo(Vector3 target, ref Position pos, ref Rotation rot, Entity e, float dt,
        float defaultVelocity, float defaultTurnRate)
    {
        ref var state = ref GetRuntimeState(e, ref pos, ref rot);
        var step = GetVelocity(e, defaultVelocity, dt) * dt;
        if (TryHandleTargetArrival(target, ref pos, ref rot, e, ref state, step)) return;

        var delta = target - pos.value;
        var targetDir = SafeNormalized(delta, rot.value);
        var currentDir = SafeNormalized(rot.value, targetDir);

        if (Vector3.Dot(currentDir, targetDir) < 0.9999f && delta.sqrMagnitude > 0.01f)
        {
            rot.value = SmoothTurn(currentDir, targetDir, GetTurnRate(e, defaultTurnRate) * dt);
        }
        else if (delta.sqrMagnitude > 0.01f)
        {
            rot.value = targetDir;
        }

        pos.value += SafeNormalized(rot.value, targetDir) * step;
    }

    private static bool TryHandleTargetArrival(Vector3 target, ref Position pos, ref Rotation rot, Entity e,
        ref TrajectoryRuntimeState state, float step)
    {
        if (state.TargetPhase == TrajectoryTargetPhase.PassedTarget)
        {
            var continuationDirection = SafeNormalized(state.TargetExitDirection, rot.value);
            rot.value = continuationDirection;
            pos.value += continuationDirection * step;
            return true;
        }

        if (state.TargetPhase == TrajectoryTargetPhase.AwaitingImpact)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
            return true;
        }

        var delta = target - pos.value;
        var distance = delta.magnitude;
        if (distance > step) return false;

        var arrivalDirection = SafeNormalized(delta, rot.value);
        rot.value = arrivalDirection;
        state.TargetExitDirection = arrivalDirection;

        var impactProfile = e.GetComponent<SkillEntity>().Asset.ImpactProfile;
        if (impactProfile.ContinueAfterHit)
        {
            state.TargetPhase = TrajectoryTargetPhase.PassedTarget;
            pos.value = target + arrivalDirection * Mathf.Max(0f, step - distance);
        }
        else
        {
            state.TargetPhase = TrajectoryTargetPhase.AwaitingImpact;
            pos.value = target;
            SkillImpactRuntime.RequestPositionImpact(e);
        }
        return true;
    }

    private static void PlaceLinear(Entity entity, Vector3 start, Vector3 end, Vector3 baseScale,
        ref Position position, ref Rotation rotation)
    {
        Vector3 direction = DirectionTo(end, start, Vector3.right);
        float length = Mathf.Max(0.1f, Vector2.Distance(start, end));
        position.value = (start + end) * 0.5f;
        rotation.value = direction;
        ref var extent = ref entity.GetComponent<ColliderLinearExtent>();
        extent.Forward = length * 0.5f;
        extent.Backward = length * 0.5f;
        StretchVisual(entity, length, baseScale);
    }

    private static void StretchVisual(Entity entity, float length, Vector3 baseScale)
    {
        SkillLinearVisual.Apply(entity, length, baseScale);
    }

    private static void AnchorCollisionAtCurrentPosition(Entity entity, ref Position position)
    {
        entity.GetComponent<PrevPosition>().Value = position.v2;
    }

    private static ref TrajectoryRuntimeState GetRuntimeState(Entity e, ref Position pos, ref Rotation rot)
    {
        ref var state = ref e.GetComponent<TrajectoryRuntimeState>();
        if (!state.Initialized)
        {
            state.Initialized = true;
            state.Returning = false;
            state.StartPosition = pos.value;
            state.StartDirection = SafeNormalized(rot.value, Vector3.right);
            state.TargetPhase = TrajectoryTargetPhase.Seeking;
            state.TargetExitDirection = state.StartDirection;
            state.Elapsed = 0f;
            state.DistanceTravelled = 0f;
            state.Timer = 0f;
            state.StepIndex = 0;
            state.Phase = Randy.randomFloat(-TwoPi, TwoPi);
            if (Mathf.Abs(state.Phase) < 0.1f)
            {
                state.Phase = state.Phase < 0f ? -0.1f : 0.1f;
            }
        }

        return ref state;
    }

    private static void ResetRuntimeState(Entity e)
    {
        SetOrAdd(e, new TrajectoryRuntimeState());
    }

    private static Vector3 GetTargetPos(ref SkillContext context)
    {
        if (context.TargetObj != null && !context.TargetObj.isRekt())
        {
            return context.TargetObj.GetSimPos();
        }

        return context.TargetPos;
    }

    private static Vector3 DirectionTo(Vector3 target, Vector3 source, Vector3 fallback)
    {
        return SafeNormalized(target - source, fallback);
    }

    private static Vector3 SafeNormalized(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude >= 0.0001f)
        {
            return value.normalized;
        }
        if (fallback.sqrMagnitude >= 0.0001f)
        {
            return fallback.normalized;
        }

        return Vector3.right;
    }

    private static Vector3 PerpendicularInPlane(Vector3 dir)
    {
        var plane = new Vector2(dir.x, dir.y);
        if (plane.sqrMagnitude < 0.0001f)
        {
            plane = Vector2.right;
        }

        plane.Normalize();
        return new Vector3(-plane.y, plane.x, 0f);
    }

    private static Vector3 SmoothTurn(Vector3 currentDir, Vector3 targetDir, float maxAngleDegrees)
    {
        if (targetDir.sqrMagnitude < 0.0001f)
        {
            return currentDir;
        }
        if (currentDir.sqrMagnitude < 0.0001f)
        {
            currentDir = Vector3.right;
        }

        var current = currentDir.normalized;
        var target = targetDir.normalized;
        var dot = Mathf.Clamp(Vector3.Dot(current, target), -1f, 1f);
        var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
        if (angle <= maxAngleDegrees)
        {
            return target;
        }

        var axis = Vector3.Cross(current, target);
        if (axis.sqrMagnitude < 0.0001f)
        {
            return target;
        }

        return Quaternion.AngleAxis(maxAngleDegrees, axis.normalized) * current;
    }

    private static float GetVelocity(Entity e, float defaultValue, float dt)
    {
        if (!e.TryGetComponent(out MotionParams motion)) return defaultValue;
        var baseVel = motion.Velocity > 0f ? motion.Velocity : defaultValue;

        // 运动配置使用短促的起步冲量，随后迅速稳定到巡航速度。
        if (motion.HasRamp)
        {
            motion.RampElapsed += dt;
            return baseVel * motion.CurrentMultiplier;
        }

        return baseVel;
    }

    private static float GetTurnRate(Entity e, float defaultValue)
    {
        return e.TryGetComponent(out MotionParams motion) && motion.TurnRate > 0f ? motion.TurnRate : defaultValue;
    }

    private static void EnsureVelocity(Entity e, float value)
    {
        if (e.TryGetComponent(out MotionParams motion))
        {
            if (motion.Velocity <= 0f) motion.Velocity = value;
            return;
        }
        e.AddComponent(new MotionParams { Velocity = value });
    }

    private static void EnsureTurnRate(Entity e, float value)
    {
        if (e.TryGetComponent(out MotionParams motion))
        {
            if (motion.TurnRate <= 0f) motion.TurnRate = value;
            return;
        }
        e.AddComponent(new MotionParams { TurnRate = value });
    }

    private static TrajectoryParams GetParams(Entity e, TrajectoryKind kind, in TrajectoryParams defaults)
    {
        return e.TryGetComponent(out TrajectoryParams existing) && existing.Kind == kind ? existing : defaults;
    }

    private static void SetHeightGate(Entity e, float maxHeight)
    {
        if (e.TryGetComponent(out MotionParams motion))
        {
            motion.HasHeightGate = true;
            motion.HeightGateMax = maxHeight;
            return;
        }
        e.AddComponent(new MotionParams { HasHeightGate = true, HeightGateMax = maxHeight });
    }

    private static void SetOrAdd<TComponent>(Entity e, TComponent component) where TComponent : struct, IComponent
    {
        if (e.HasComponent<TComponent>())
        {
            ref var current = ref e.GetComponent<TComponent>();
            current = component;
            return;
        }

        e.AddComponent(component);
    }

    private static void ClearCollisionHeightGate(Entity e)
    {
        if (e.TryGetComponent(out MotionParams motion))
        {
            motion.HasHeightGate = false;
        }
    }

    private static Vector2 RandomInCircle(float radius)
    {
        var angle = Randy.randomFloat(0f, TwoPi);
        var distance = Mathf.Sqrt(Randy.randomFloat(0f, 1f)) * Mathf.Max(0f, radius);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }
}
