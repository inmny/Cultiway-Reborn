using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Editor;
using Friflo.Engine.ECS;
using UnityEngine;
using MotionTag = Cultiway.Core.Semantics.SkillSemantics.Motion;

namespace Cultiway.Content;

public class SkillTrajectories : ExtendLibrary<TrajectoryAsset, SkillTrajectories>
{
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
        TowardsDirection.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            EnsureTurnRate(e, 180f);
            ClearCollisionHeightGate(e);
        };
        TowardsDirection.AddSemantics(MotionTag.Direct);
    }

    private static void SetupTowardsDirectionNoRot()
    {
        TowardsDirectionNoRot.CanBeSelectedByModifier = false;
        TowardsDirectionNoRot.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            ClearCollisionHeightGate(e);
        };
        TowardsDirectionNoRot.AddSemantics(MotionTag.Direct);
    }

    private static void SetupTowardsPosition()
    {
        TowardsPosition.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            EnsureTurnRate(e, 180f);
            ClearCollisionHeightGate(e);
        };
        TowardsPosition.AddSemantics(MotionTag.Direct);
    }

    private static void SetupTowardsTarget()
    {
        TowardsTarget.OnInit = e =>
        {
            EnsureVelocity(e, 32f);
            EnsureTurnRate(e, 180f);
            ClearCollisionHeightGate(e);
        };
        TowardsTarget.AddSemantics(MotionTag.Homing);
    }

    private static void SetupDriftHoming()
    {
        DriftHoming.OnInit = e =>
        {
            EnsureVelocity(e, 35f);
            EnsureTurnRate(e, 220f);
            ClearCollisionHeightGate(e);
        };
        DriftHoming.AddSemantics(MotionTag.Homing);
    }

    private static void SetupSineWave()
    {
        SineWave.OnInit = e =>
        {
            EnsureVelocity(e, 30f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Wave,
                Amplitude = 0.65f,
                Frequency = 3.2f
            });
            ClearCollisionHeightGate(e);
        };
        SineWave.AddSemantics(MotionTag.Wave);
    }

    private static void SetupZigzag()
    {
        Zigzag.OnInit = e =>
        {
            EnsureVelocity(e, 34f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Zigzag,
                SideAmplitude = 0.75f,
                SegmentDuration = 0.12f
            });
            ClearCollisionHeightGate(e);
        };
        Zigzag.AddSemantics(MotionTag.Zigzag);
    }

    private static void SetupSpiralHoming()
    {
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
            ClearCollisionHeightGate(e);
        };
        SpiralHoming.AddSemantics(MotionTag.Spiral, MotionTag.Homing);
    }

    private static void SetupOrbitTarget()
    {
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
            ClearCollisionHeightGate(e);
        };
        OrbitTarget.AddSemantics(MotionTag.Orbit, MotionTag.Homing);
    }

    private static void SetupBoomerang()
    {
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
            ClearCollisionHeightGate(e);
        };
        Boomerang.AddSemantics(MotionTag.Return);
    }

    private static void SetupSlowVortex()
    {
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
            ClearCollisionHeightGate(e);
        };
        SlowVortex.AddSemantics(MotionTag.Vortex);
    }

    /// <summary>弧线攻击接近目标时允许接触原版建筑的高度余量。</summary>
    internal const float ArcCollisionHeight = .55f;

    /// <summary>垂直攻击原有的触地余量。</summary>
    internal const float SkyfallCollisionHeight = .35f;

    private static void SetupArcToPosition()
    {
        ArcToPosition.OnInit = e =>
        {
            EnsureVelocity(e, 30f);
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Arc,
                Duration = 0.5f,
                Height = 4f
            });
            SetHeightGate(e, ArcCollisionHeight);
        };
        ArcToPosition.AddSemantics(MotionTag.Falling);
    }

    private static void SetupFallingStrike()
    {
        FallingStrike.OnInit = e =>
        {
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.Falling,
                StartHeight = 7f,
                FallSpeed = 28f,
                DriftSpeed = 7f,
                ImpactHeight = SkyfallCollisionHeight
            });
            SetHeightGate(e, SkyfallCollisionHeight);
        };
        FallingStrike.WithOrientations(TrajectoryOrientation.Vertical)
            .AddSemantics(MotionTag.Falling);
    }

    private static void SetupGroundCrawl()
    {
        GroundCrawl.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            EnsureTurnRate(e, 120f);
            ClearCollisionHeightGate(e);
        };
        GroundCrawl.WithOrientations(TrajectoryOrientation.Ground)
            .AddSemantics(MotionTag.Ground);
    }

    private static void SetupLightningSnap()
    {
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
            ClearCollisionHeightGate(e);
        };
        // 折线闪击以水平推进为主，但抵达后即停、视觉接近原地显现，故同时声明 Appear。
        LightningSnap.WithOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Snap);
    }

    private static void SetupRainFall()
    {
        RainFall.OnInit = e =>
        {
            SetOrAdd(e, new TrajectoryParams
            {
                Kind = TrajectoryKind.RainFall,
                StartHeight = 8f,
                FallSpeed = 32f,
                DriftSpeed = 1.4f,
                ImpactHeight = SkyfallCollisionHeight
            });
            SetHeightGate(e, SkyfallCollisionHeight);
        };
        RainFall.WithOrientations(TrajectoryOrientation.Vertical)
            .AddSemantics(MotionTag.Rain, MotionTag.Falling);
    }

    private static void SetupAppearAtTarget()
    {
        // 直接在目标点显现的轨迹。
        AppearAtTarget.OnInit = e =>
        {
            ClearCollisionHeightGate(e);
        };
        AppearAtTarget.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear, MotionTag.Snap);
    }

    private static void SetupGroundEruptAtTarget()
    {
        GroundEruptAtTarget.OnInit = e =>
        {
            ClearCollisionHeightGate(e);
        };
        GroundEruptAtTarget.WithOrientations(TrajectoryOrientation.Ground)
            .AddSemantics(MotionTag.GroundManifest, MotionTag.Ground, MotionTag.Appear);
    }

    private static void SetupFieldAtTarget()
    {
        FieldAtTarget.OnInit = e =>
        {
            ClearCollisionHeightGate(e);
        };
        FieldAtTarget.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear);
    }

    private static void SetupFieldAdvance()
    {
        FieldAdvance.OnInit = e =>
        {
            ClearCollisionHeightGate(e);
        };
        FieldAdvance.WithOrientations(TrajectoryOrientation.Horizontal)
            .AddSemantics(MotionTag.Direct);
    }

    private static void SetupFieldFollowTarget()
    {
        FieldFollowTarget.OnInit = e =>
        {
            ClearCollisionHeightGate(e);
        };
        FieldFollowTarget.WithOrientations(TrajectoryOrientation.Horizontal | TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Homing);
    }

    private static void SetupInstantBeam()
    {
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
        ChainTargets.OnInit = e =>
        {
            ClearCollisionHeightGate(e);
        };
        ChainTargets.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Chain, MotionTag.Snap);
    }

    private static void SetupWallBetweenCasterAndTarget()
    {
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
        ShieldOnCaster.OnInit = e => ClearCollisionHeightGate(e);
        ShieldOnCaster.WithOrientations(TrajectoryOrientation.Appear)
            .AddSemantics(MotionTag.Appear);
    }

    private static void SetupMeleeSweep()
    {
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
            ClearCollisionHeightGate(e);
        };
        MeleeSweep.WithOrientations(TrajectoryOrientation.Melee)
            .AddSemantics(MotionTag.MeleeSweep);
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
}
