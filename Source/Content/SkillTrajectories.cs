using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

public class SkillTrajectories : ExtendLibrary<TrajectoryAsset, SkillTrajectories>
{
    public static TrajectoryAsset TowardsDirection { get; private set; }
    public static TrajectoryAsset TowardsDirectionNoRot { get; private set; }
    public static TrajectoryAsset TowardsPosition { get; private set; }
    public static TrajectoryAsset TowardsTarget { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        TowardsDirection.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            // 计算目标方向
            var target_dir = context.TargetDir;
            var current_dir = rot.value.normalized;
            
            // 如果方向有偏离，进行平滑转向
            if (Vector3.Dot(current_dir, target_dir) < 0.9999f) // 避免浮点误差
            {
                var turn_rate = e.TryGetComponent(out TurnRate turnRate) ? turnRate.Value : 180f; // 默认每秒360度
                var max_angle_change = turn_rate * dt; // 本帧最大转向角度
                rot.value = SmoothTurn(current_dir, target_dir, max_angle_change);
            }
            else
            {
                rot.value = target_dir;
            }
            
            pos.value += rot.value.normalized * dt * e.GetComponent<Velocity>().Value;
        };
        TowardsDirection.OnInit = e =>
        {
            e.AddComponent(new Velocity()
            {
                Value = 20  // 基础速度提升2倍
            });
            e.AddComponent(new TurnRate()
            {
                Value = 180f
            });
        };
        TowardsDirectionNoRot.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            // TowardsDirectionNoRot 不更新旋转，只移动
            pos.value += rot.value.normalized * dt * e.GetComponent<Velocity>().Value;
        };
        TowardsDirectionNoRot.OnInit = e =>
        {
            e.AddComponent(new Velocity()
            {
                Value = 20
            });
        };
        TowardsPosition.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            var delta = context.TargetPos - pos.value;
            var target_dir = delta.normalized;
            var current_dir = rot.value.normalized;
            var move_distance = dt * e.GetComponent<Velocity>().Value;

            // 如果方向有偏离，进行平滑转向
            if (Vector3.Dot(current_dir, target_dir) < 0.9999f && delta.sqrMagnitude > 0.01f)
            {
                var turn_rate = e.TryGetComponent(out TurnRate turnRate) ? turnRate.Value : 180f;
                var max_angle_change = turn_rate * dt;
                rot.value = SmoothTurn(current_dir, target_dir, max_angle_change);
            }
            else if (delta.sqrMagnitude > 0.01f)
            {
                rot.value = target_dir;
            }

            var actual_to_move = move_distance * rot.value.normalized;

            pos.value += actual_to_move;
        };
        TowardsPosition.OnInit = e =>
        {
            e.AddComponent(new Velocity()
            {
                Value = 20
            });
            e.AddComponent(new TurnRate()
            {
                Value = 180f
            });
        };
        TowardsTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            var target_xy = context.TargetObj.current_position;
            var delta = new Vector3(target_xy.x, target_xy.y, context.TargetObj.getHeight()) - pos.value;
            var target_dir = delta.normalized;
            var current_dir = rot.value.normalized;
            var move_distance = dt * e.GetComponent<Velocity>().Value;

            // 如果方向有偏离，进行平滑转向
            if (Vector3.Dot(current_dir, target_dir) < 0.9999f && delta.sqrMagnitude > 0.01f)
            {
                var turn_rate = e.TryGetComponent(out TurnRate turnRate) ? turnRate.Value : 180f;
                var max_angle_change = turn_rate * dt;
                rot.value = SmoothTurn(current_dir, target_dir, max_angle_change);
            }
            else if (delta.sqrMagnitude > 0.01f)
            {
                rot.value = target_dir;
            }

            var actual_to_move = move_distance * rot.value.normalized;

            pos.value += actual_to_move;
        };
        TowardsTarget.OnInit = e =>
        {
            e.AddComponent(new Velocity()
            {
                Value = 20
            });
            e.AddComponent(new TurnRate()
            {
                Value = 180f
            });
        };
    }

    /// <summary>
    /// 平滑转向：从当前方向转向目标方向，限制最大转向角度
    /// </summary>
    private static Vector3 SmoothTurn(Vector3 current_dir, Vector3 target_dir, float max_angle_degrees)
    {
        if (target_dir.sqrMagnitude < 0.0001f)
        {
            return current_dir;
        }
        if (current_dir.sqrMagnitude < 0.0001f)
        {
            current_dir = Vector3.right;
        }
        var current_normalized = current_dir.normalized;
        var target_normalized = target_dir.normalized;
        
        // 计算当前角度差
        var dot = Vector3.Dot(current_normalized, target_normalized);
        dot = Mathf.Clamp(dot, -1f, 1f); // 防止数值误差
        var angle_rad = Mathf.Acos(dot);
        var angle_deg = angle_rad * Mathf.Rad2Deg;
        
        // 如果角度差小于最大转向角度，直接转向目标
        if (angle_deg <= max_angle_degrees)
        {
            return target_normalized;
        }
        
        // 否则，按最大角度转向
        var axis = Vector3.Cross(current_normalized, target_normalized);
        if (axis.sqrMagnitude < 0.0001f)
        {
            return current_normalized;
        }
        
        axis.Normalize();
        var rotation = Quaternion.AngleAxis(max_angle_degrees, axis);
        // 打印转向前后的角度差（单位：度）

        var result = rotation * current_normalized;

        var dot_after = Vector3.Dot(result, target_normalized);
        dot_after = Mathf.Clamp(dot_after, -1f, 1f);
        var after_angle_rad = Mathf.Acos(dot_after);
        var after_angle_deg = after_angle_rad * Mathf.Rad2Deg;
        return rotation * current_normalized;
    }
}