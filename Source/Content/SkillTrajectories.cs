using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

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
    }

    private static void SetupTowardsDirection()
    {
        TowardsDirection.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            var targetDir = SafeNormalized(context.TargetDir, rot.value);
            var currentDir = SafeNormalized(rot.value, targetDir);

            if (Vector3.Dot(currentDir, targetDir) < 0.9999f)
            {
                var turnRate = e.TryGetComponent(out TurnRate turnRateComponent) ? turnRateComponent.Value : 180f;
                rot.value = SmoothTurn(currentDir, targetDir, turnRate * dt);
            }
            else
            {
                rot.value = targetDir;
            }

            pos.value += SafeNormalized(rot.value, targetDir) * dt * GetVelocity(e, 20f);
        };
        TowardsDirection.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            EnsureTurnRate(e, 180f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupTowardsDirectionNoRot()
    {
        TowardsDirectionNoRot.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e,
            float dt) =>
        {
            pos.value += SafeNormalized(rot.value, context.TargetDir) * dt * GetVelocity(e, 20f);
        };
        TowardsDirectionNoRot.CanBeSelectedByModifier = false;
        TowardsDirectionNoRot.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupTowardsPosition()
    {
        TowardsPosition.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            MoveSmoothlyTo(context.TargetPos, ref pos, ref rot, e, dt, 20f, 180f);
        };
        TowardsPosition.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            EnsureTurnRate(e, 180f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupTowardsTarget()
    {
        TowardsTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            MoveSmoothlyTo(GetTargetPos(ref context), ref pos, ref rot, e, dt, 20f, 180f);
        };
        TowardsTarget.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            EnsureTurnRate(e, 180f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupDriftHoming()
    {
        DriftHoming.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var targetDir = DirectionTo(GetTargetPos(ref context), pos.value, context.TargetDir);
            var side = PerpendicularInPlane(targetDir);
            var drift = Mathf.Lerp(0.65f, 0f, Mathf.Clamp01(state.Elapsed / 0.75f));
            var desired = SafeNormalized(targetDir + side * Mathf.Sign(state.Phase) * drift, targetDir);
            var current = SafeNormalized(rot.value, desired);
            var turnRate = e.TryGetComponent(out TurnRate turnRateComponent) ? turnRateComponent.Value : 220f;

            rot.value = SmoothTurn(current, desired, turnRate * dt);
            pos.value += SafeNormalized(rot.value, desired) * GetVelocity(e, 22f) * dt;
        };
        DriftHoming.OnInit = e =>
        {
            EnsureVelocity(e, 22f);
            EnsureTurnRate(e, 220f);
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupSineWave()
    {
        SineWave.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var wave = e.TryGetComponent(out WaveTrajectoryParams waveComponent)
                ? waveComponent
                : new WaveTrajectoryParams { Amplitude = 0.6f, Frequency = 3.5f, Phase = state.Phase };
            var baseDir = SafeNormalized(state.StartDirection, context.TargetDir);
            var side = PerpendicularInPlane(baseDir);
            var forward = baseDir * GetVelocity(e, 18f) * state.Elapsed;
            var sideOffset = side * (Mathf.Sin(state.Elapsed * wave.Frequency * TwoPi + wave.Phase + state.Phase)
                                     * wave.Amplitude);
            var next = state.StartPosition + forward + sideOffset;

            rot.value = DirectionTo(next, pos.value, baseDir);
            pos.value = next;
        };
        SineWave.OnInit = e =>
        {
            EnsureVelocity(e, 18f);
            SetOrAdd(e, new WaveTrajectoryParams
            {
                Amplitude = 0.65f,
                Frequency = 3.2f,
                Phase = 0f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupZigzag()
    {
        Zigzag.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var zigzag = e.TryGetComponent(out ZigzagTrajectoryParams zigzagComponent)
                ? zigzagComponent
                : new ZigzagTrajectoryParams { SideAmplitude = 0.75f, SegmentDuration = 0.12f };
            var baseDir = SafeNormalized(state.StartDirection, context.TargetDir);
            var side = PerpendicularInPlane(baseDir);
            var segmentDuration = Mathf.Max(0.03f, zigzag.SegmentDuration);
            var sideT = Mathf.PingPong(state.Elapsed / segmentDuration, 1f) * 2f - 1f;
            var next = state.StartPosition
                       + baseDir * (GetVelocity(e, 21f) * state.Elapsed)
                       + side * (sideT * zigzag.SideAmplitude);

            rot.value = DirectionTo(next, pos.value, baseDir);
            pos.value = next;
        };
        Zigzag.OnInit = e =>
        {
            EnsureVelocity(e, 21f);
            SetOrAdd(e, new ZigzagTrajectoryParams
            {
                SideAmplitude = 0.75f,
                SegmentDuration = 0.12f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupSpiralHoming()
    {
        SpiralHoming.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var spiral = e.TryGetComponent(out SpiralTrajectoryParams spiralComponent)
                ? spiralComponent
                : new SpiralTrajectoryParams
                {
                    Radius = 0.7f,
                    Frequency = 4f,
                    RadiusDamping = 0.75f,
                    HomingStrength = 0.7f
                };
            var targetDir = DirectionTo(GetTargetPos(ref context), pos.value, context.TargetDir);
            var current = SafeNormalized(rot.value, targetDir);
            var homing = Mathf.Clamp01(spiral.HomingStrength);
            var baseDir = SmoothTurn(current, targetDir, GetTurnRate(e, 240f) * homing * dt);
            var side = PerpendicularInPlane(baseDir);
            var radius = spiral.Radius * Mathf.Exp(-Mathf.Max(0f, spiral.RadiusDamping) * state.Elapsed);
            var angle = state.Elapsed * spiral.Frequency * TwoPi + state.Phase;
            var swirl = side * (Mathf.Sin(angle) * radius);
            var desired = SafeNormalized(baseDir + swirl, baseDir);

            rot.value = desired;
            pos.value += desired * GetVelocity(e, 19f) * dt;
            pos.z += Mathf.Cos(angle) * radius * 0.04f;
        };
        SpiralHoming.OnInit = e =>
        {
            EnsureVelocity(e, 19f);
            EnsureTurnRate(e, 240f);
            SetOrAdd(e, new SpiralTrajectoryParams
            {
                Radius = 0.7f,
                Frequency = 4f,
                RadiusDamping = 0.75f,
                HomingStrength = 0.7f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void SetupOrbitTarget()
    {
        OrbitTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            ref var state = ref GetRuntimeState(e, ref pos, ref rot);
            state.Elapsed += dt;

            var orbit = e.TryGetComponent(out OrbitTrajectoryParams orbitComponent)
                ? orbitComponent
                : new OrbitTrajectoryParams
                {
                    StartRadius = 2.2f,
                    AngularSpeed = 480f,
                    ShrinkSpeed = 1.8f,
                    HomingStrength = 1f
                };
            var target = GetTargetPos(ref context);
            var radius = Mathf.Max(0f, orbit.StartRadius - orbit.ShrinkSpeed * state.Elapsed);
            var angle = state.Phase + orbit.AngularSpeed * Mathf.Deg2Rad * state.Elapsed;
            var desired = target + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            if (radius <= 0.2f)
            {
                desired = target;
            }

            var moveDir = DirectionTo(desired, pos.value, context.TargetDir);
            var speed = GetVelocity(e, 20f) * Mathf.Lerp(1f, 1.35f, Mathf.Clamp01(orbit.HomingStrength));
            pos.value = Vector3.MoveTowards(pos.value, desired, speed * dt);
            rot.value = moveDir;
        };
        OrbitTarget.OnInit = e =>
        {
            EnsureVelocity(e, 20f);
            SetOrAdd(e, new OrbitTrajectoryParams
            {
                StartRadius = 2.2f,
                AngularSpeed = 480f,
                ShrinkSpeed = 1.8f,
                HomingStrength = 1f
            });
            ResetRuntimeState(e);
            ClearCollisionHeightGate(e);
        };
    }

    private static void MoveSmoothlyTo(Vector3 target, ref Position pos, ref Rotation rot, Entity e, float dt,
        float defaultVelocity, float defaultTurnRate)
    {
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

        pos.value += SafeNormalized(rot.value, targetDir) * GetVelocity(e, defaultVelocity) * dt;
    }

    private static ref TrajectoryRuntimeState GetRuntimeState(Entity e, ref Position pos, ref Rotation rot)
    {
        if (!e.HasComponent<TrajectoryRuntimeState>())
        {
            e.AddComponent(new TrajectoryRuntimeState());
        }

        ref var state = ref e.GetComponent<TrajectoryRuntimeState>();
        if (!state.Initialized)
        {
            state.Initialized = true;
            state.Returning = false;
            state.StartPosition = pos.value;
            state.StartDirection = SafeNormalized(rot.value, Vector3.right);
            state.Elapsed = 0f;
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

    private static float GetVelocity(Entity e, float defaultValue)
    {
        return e.TryGetComponent(out Velocity velocity) ? velocity.Value : defaultValue;
    }

    private static float GetTurnRate(Entity e, float defaultValue)
    {
        return e.TryGetComponent(out TurnRate turnRate) ? turnRate.Value : defaultValue;
    }

    private static void EnsureVelocity(Entity e, float value)
    {
        if (e.HasComponent<Velocity>()) return;
        e.AddComponent(new Velocity
        {
            Value = value
        });
    }

    private static void EnsureTurnRate(Entity e, float value)
    {
        if (e.HasComponent<TurnRate>()) return;
        e.AddComponent(new TurnRate
        {
            Value = value
        });
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
        if (e.HasComponent<CollisionHeightGate>())
        {
            e.RemoveComponent<CollisionHeightGate>();
        }
    }

    private static Vector2 RandomInCircle(float radius)
    {
        var angle = Randy.randomFloat(0f, TwoPi);
        var distance = Mathf.Sqrt(Randy.randomFloat(0f, 1f)) * Mathf.Max(0f, radius);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }
}
