using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>已削去的耐久或真实承伤留下的短余痕；不参与碰撞及存档。</summary>
    private static readonly List<WindScar> windScars = new(24);

    /// <summary>记录一次确认后的接触，贴身偏移跟原人物一起移动。</summary>
    /// <param name="running">实际受理的施放。</param><param name="node">碰到人物的部位。</param>
    /// <param name="target">实际承伤人物。</param><param name="point">已经确认的相交点。</param>
    /// <param name="armor">是否真正削去了护甲耐久。</param>
    /// <param name="poison">来自毒液腐蚀时使用蚀孔。</param>
    private static void AddWindScar(RunningCast running, int node, Actor target, Vector3 point, bool armor, bool poison = false)
    {
        if (windScars.Count >= 24) windScars.RemoveAt(0);
        Vector3 foot = SpatialActorPose.Foot(target);
        foot.z += simulation.terrain?.Sample(foot.x, foot.y) ?? 0f;
        Vector2 direction = running.Cast.Velocity(node);
        if (direction.sqrMagnitude < .0001f) direction = foot - running.Cast.ContactOrigin(node);
        if (direction.sqrMagnitude < .0001f) direction = Vector2.up;
        Vector3 offset = point - foot;
        if (armor) offset.z = Mathf.Max(offset.z, SpatialActorPose.Height(target) * .35f);
        windScars.Add(new WindScar(target, target.id, offset, direction.normalized, simulation.contactTime, armor, poison));
    }

    /// <summary>风沿来向留下短剪面；毒在接触处留下短暂的蚀孔。</summary>
    /// <param name="now">已经确认的共同世界时间。</param>
    private void AppendWindScars(double now)
    {
        if (windScars.Count == 0) return;
        for (int i = windScars.Count - 1; i >= 0; i--)
        {
            WindScar mark = windScars[i];
            float age = (float)(now - mark.At);
            float duration = mark.Poison ? .48f : .34f;
            if (age >= duration || mark.Target.data?.id != mark.TargetId)
            {
                windScars.RemoveAt(i);
                continue;
            }
            Vector3 foot = SpatialActorPose.Foot(mark.Target);
            foot.z += terrain?.Sample(foot.x, foot.y) ?? 0f;
            float travel = Mathf.SmoothStep(0f, 1f, age / duration);
            Vector3 position = foot + mark.Offset + (Vector3)mark.Direction * (mark.Poison ? 0f : travel * .38f);
            float scale = (mark.Armor ? .95f : .68f) + travel * .22f;
            float turn = Mathf.Atan2(-mark.Direction.x, mark.Direction.y) * Mathf.Rad2Deg;
            Quaternion rotation = mark.Poison ? Quaternion.Euler(24f, 0f, turn) : Quaternion.Euler(0f, 18f, turn + 22f * travel);
            Matrix4x4 pose = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mark.Poison ? .17f : .035f, duration, age));
            FormResource model = FormResources.Load(mark.Poison ? "poison_pit" : "baleful_wind_mark");
            visuals.Add(new FormVisualInstance(model, pose, dye: new Color(1f, 1f, 1f, opacity), terrain: terrain));
        }
    }

    /// <summary>单个短余痕的真实来源和相对姿态。</summary>
    private readonly struct WindScar
    {
        /// <summary>原受击人物和用于识别对象复用的稳定编号。</summary>
        internal readonly Actor Target;
        internal readonly long TargetId;
        /// <summary>相交位置相对人物脚下的偏移。</summary>
        internal readonly Vector3 Offset;
        /// <summary>相交时真实风向。</summary>
        internal readonly Vector2 Direction;
        /// <summary>记录余痕的世界时点。</summary>
        internal readonly double At;
        /// <summary>确实减少了原护甲的耐久。</summary>
        internal readonly bool Armor;
        /// <summary>是否由毒液腐蚀而非风切留下。</summary>
        internal readonly bool Poison;

        /// <summary>保留到期前绘制所需的已确认资料。</summary>
        /// <param name="target">原人物。</param><param name="targetId">原编号。</param>
        /// <param name="offset">接触偏移。</param><param name="direction">风向。</param>
        /// <param name="at">接触时点。</param><param name="armor">护甲是否真正受损。</param>
        /// <param name="poison">是否显示蚀孔。</param>
        internal WindScar(Actor target, long targetId, Vector3 offset, Vector2 direction, double at, bool armor, bool poison)
        {
            Target = target;
            TargetId = targetId;
            Offset = offset;
            Direction = direction;
            At = at;
            Armor = armor;
            Poison = poison;
        }
    }
}
