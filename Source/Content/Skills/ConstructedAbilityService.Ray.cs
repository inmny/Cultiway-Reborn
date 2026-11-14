using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>射线沿真实起点先确认近处接触，其余结算继续共同通道。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>本次查询涉及射线的候选，用后清除人物及施放引用。</summary>
    private readonly List<ContactChoice> rayContacts = new();

    /// <summary>每条真实射线的最早时点及沿线距离，重复查询复用容器。</summary>
    private readonly Dictionary<(long Cast, int Node), (float Time, float Length)> rayFirst = new();

    /// <summary>先找每条射线的前方接触，再共同选取；同刻依赖成环时沿用稳定来源顺序。</summary>
    /// <param name="ordinary">其他作用的原最早候选。</param>
    private ContactChoice? SelectRayContact(ContactChoice? ordinary)
    {
        rayFirst.Clear();
        float earliest = float.PositiveInfinity;
        foreach (ContactChoice hit in rayContacts)
        {
            earliest = Mathf.Min(earliest, hit.Fraction);
            Remember(hit.Left, hit.LeftRayLength, hit.Fraction);
            if (hit.Right.HasValue) Remember(hit.Right.Value, hit.RightRayLength, hit.Fraction);
        }
        ContactChoice? selected = null, stable = null;
        foreach (ContactChoice hit in rayContacts)
        {
            if (hit.Fraction > earliest) continue;
            if (!stable.HasValue || ContactChoice.Compare(hit, stable.Value) < 0) stable = hit;
            if (!IsFirst(hit.Left, hit.LeftRayLength, hit.Fraction) ||
                hit.Right.HasValue && !IsFirst(hit.Right.Value, hit.RightRayLength, hit.Fraction)) continue;
            if (!selected.HasValue || ContactChoice.Compare(hit, selected.Value) < 0) selected = hit;
        }
        selected ??= stable;
        if (selected.HasValue && (!ordinary.HasValue || ContactChoice.Compare(selected.Value, ordinary.Value) < 0))
            ordinary = selected;
        rayContacts.Clear();
        rayFirst.Clear();
        return ordinary;

        /// <summary>同一射线仅记录前方第一处候选。</summary>
        /// <param name="shape">实际形状。</param><param name="length">沿线长度，非射线为负。</param><param name="time">共同进度。</param>
        void Remember(ShapeMotion shape, float length, float time)
        {
            if (length < 0f) return;
            var key = (shape.Running.Id, shape.To.Node); var value = (time, length);
            if (!rayFirst.TryGetValue(key, out var prior) || value.CompareTo(prior) < 0) rayFirst[key] = value;
        }

        /// <summary>只有不越过本射线前方候选的接触才可共同确认。</summary>
        /// <param name="shape">实际形状。</param><param name="length">沿线长度。</param><param name="time">共同进度。</param>
        bool IsFirst(ShapeMotion shape, float length, float time) => length < 0f ||
            (time, length).CompareTo(rayFirst[(shape.Running.Id, shape.To.Node)]) == 0;
    }

    /// <summary>把普通接触点收敛到射线沿线首先碰到的表面，返回整体世界长度。</summary>
    /// <param name="ray">真实射线。</param><param name="target">同一时点的阻挡。</param>
    /// <param name="point">共同作用点。</param><param name="normal">射线朝向阻挡。</param>
    private static float RayContact(in AbilityCapsule ray, in AbilityCapsule target, ref Vector3 point, ref Vector3 normal)
    {
        if (AbilityPairSweep.RayEntry(ray, target, out float length, out Vector3 entry, out Vector3 direction))
        {
            point = entry; normal = direction;
            return length;
        }
        return AbilityPairSweep.RayLengthAt(ray, point, normal);
    }
}
