using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>先取真实接触方向，再按当前质量换算，最后交给共同受力及落地恢复。</summary>
    /// <param name="running">实际施放及当前控制来源。</param><param name="node">实际作用部位。</param>
    /// <param name="target">真实接触人物。</param><param name="knockback">已编译的击退幅度。</param>
    /// <param name="result">实际攻击结果。</param><param name="applied">原受力规则是否受理。</param>
    /// <param name="origin">介质传播的实际作用中心。</param><param name="strength">传播后的作用倍率。</param>
    private static Vector3 ApplyContactKnockback(RunningCast running, int node, Actor target,
        CompiledConstructionKnockback knockback, ApplyAttackState result, out bool applied,
        Vector3? origin, float strength)
    {
        applied = false;
        if (knockback?.HasEffect != true || result != ApplyAttackState.Hit || !target.isAlive() ||
            !target.asset.can_be_moved_by_powers || target.position_height > 0f || strength <= 0f)
            return Vector3.zero;

        StartingPose(target, out Vector3 position, out _);
        Vector2 direction = position - (origin ?? running.Cast.ContactOrigin(node));
        if (direction.sqrMagnitude < .000001f) direction = running.Direction;
        direction = direction.sqrMagnitude > .000001f ? direction.normalized : Vector2.right;
        float distance = knockback.Distance * strength, height = knockback.Height * strength;
        float horizontal, lift, slide = 0f;
        if (height > 0f)
        {
            // 用共同运动的世界加速度反求初速度，参数才能对应真实平地落点和最高点。
            float massGravity = Mathf.Min(target.stats["mass"] * SimGlobals.m.gravity, 20f);
            float gravity = .3f * massGravity * massGravity;
            if (gravity <= 0f) return Vector3.zero;
            lift = Mathf.Sqrt(2f * gravity * height);
            horizontal = distance * gravity / (2f * lift);
        }
        else
        {
            slide = .25f;
            horizontal = distance / slide;
            lift = 0f;
        }
        return DeliverForce(running.Owner(node), target,
            new Vector3(direction.x * horizontal, direction.y * horizontal, lift), slide, null, out applied, out _);
    }
}
