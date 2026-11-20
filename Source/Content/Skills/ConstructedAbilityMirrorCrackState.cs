using System.Collections.Generic;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>从真实撞点到圆缘的裂势，保存最先到达各许可表面的点和时刻。</summary>
    private sealed class MirrorCrack
    {
        /// <summary>共同素材的起点及走向，画面和接触读取同一份。</summary>
        internal readonly Vector2 Origin;
        internal readonly Vector2[] Ends = new Vector2[12];
        internal readonly double Started;

        /// <summary>施放归属、实际消耗及接收关系。</summary>
        internal readonly CastAuthority Authority;
        internal readonly GetHitEvent Damage;
        internal readonly float Spent, DamageScale;
        internal readonly SkillUseTargetRelation Targets;

        /// <summary>已排序的接收点，每一处最多提交一次。</summary>
        internal readonly MirrorArrival[] Arrivals;
        internal int Next;
        internal bool Stopped;

        /// <summary>已经确认的裂片面积和碎裂前最后一个真实镜口。</summary>
        internal readonly MirrorCrackGlass Glass = new();
        internal CompiledMirrorImage Rule;
        internal FormResource Resource;
        internal Matrix4x4 LastPlane;
        internal float Travelled;
        internal double? BrokenAt;

        internal MirrorCrack(Vector2 origin, MirrorReflection mirror, CompiledMirrorImage rule, double time,
            CastAuthority authority, GetHitEvent damage, float spent, float damageScale, SkillUseTargetRelation targets)
        {
            Origin = origin;
            Started = time;
            Authority = authority;
            Damage = damage;
            Spent = spent;
            DamageScale = damageScale;
            Targets = targets;
            for (int i = 0; i < Ends.Length; i++)
            {
                float angle = (i + .16f * Mathf.Sin((i + 1) * 2.32f)) * Mathf.PI * 2f / Ends.Length;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float along = Vector2.Dot(direction, origin);
                Ends[i] = origin + direction *
                    (Mathf.Sqrt(along * along + rule.Radius * rule.Radius - origin.sqrMagnitude) - along);
            }

            var arrivals = new List<MirrorArrival>(rule.Fracture.Parts.Length);
            foreach (string part in rule.Fracture.Parts)
            {
                if (!mirror.FindFirstCrackedArea(rule, origin, part, out Vector2 position)) continue;
                float distance = Vector2.Distance(origin, position);
                arrivals.Add(new MirrorArrival(part, position,
                    time + rule.Fracture.TravelSeconds * (.17f + .83f * distance / (rule.Radius * 2f))));
            }

            arrivals.Sort((a, b) => a.At.CompareTo(b.At));
            Arrivals = arrivals.ToArray();
        }

        /// <summary>将裂纹平面位置恢复到资源镜面的局部三维位置。</summary>
        internal Vector3 OnPane(Vector2 point, CompiledMirrorImage rule) =>
            rule.Centre + new Vector3(point.x, 0f, point.y);
    }

    /// <summary>裂纹首次到达实际部位，位置与时刻留给确认时重新核对。</summary>
    private readonly struct MirrorArrival
    {
        internal readonly string Part;
        internal readonly Vector2 Point;
        internal readonly double At;

        internal MirrorArrival(string part, Vector2 point, double at)
        {
            Part = part;
            Point = point;
            At = at;
        }
    }
}
