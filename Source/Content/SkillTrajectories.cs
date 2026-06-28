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

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        SetupTowardsDirection();
        SetupTowardsDirectionNoRot();
        SetupTowardsPosition();
        SetupTowardsTarget();
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
