using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>把实际身体动作改为显化投送时需要明确提供的资料，不是整招运行类型。</summary>
public sealed class BodyProjection
{
    /// <summary>显化形体；空值沿用实际借用器物的形体，空手使用掌形。</summary>
    public string Form;
    /// <summary>实际材料及可选物态，与显示染色分别保存。</summary>
    public string Matter = "physical", State;
    /// <summary>按实际体高缩放的显化大小。</summary>
    public float Scale = 3f;
    /// <summary>显化相对末端的三个转角；初速度仍沿真实出手方向，追踪时保留形体与速度的夹角。</summary>
    public float[] Rotation = { 0f, 25f, 0f };
    /// <summary>离手后的地图格速度。</summary>
    public float Speed = 8f;
    /// <summary>离手后最多继续的世界秒数。</summary>
    public float Seconds = 2f;
    /// <summary>追踪目标时每秒可转过的角度，零表示直行。</summary>
    public float TurnRate = 360f;
    /// <summary>相对施放的离手时点；负一沿用原动作的有效结束。</summary>
    public float ReleaseAt = -1f;
    /// <summary>明确采用的实际支付资源。</summary>
    public string Resource = "wakan";
    /// <summary>本次显化的整次实际消耗，不在离手时重复支付。</summary>
    public float Cost = 4f;
}

/// <summary>玩家改造及修士创作共用的资料变化，运行仍使用身体、连接和普通形体操作。</summary>
public static class BodyConstructionEdits
{
    /// <summary>保留实际发力与收势，为身体末端补齐显化、离手、投送、接触及消耗。</summary>
    /// <param name="source">已有身体动作；不能静默覆盖其原有生成主体。</param>
    /// <param name="projection">这次显化的明确选择。</param>
    public static AbilityDefinition Project(AbilityDefinition source, BodyProjection projection)
    {
        if (source?.Body == null || source.Root != null || projection == null)
            throw new InvalidDataException("显化投送需要实际身体动作，已有主体请直接修改其连接和投送");
        CompiledAbility original = AbilityCompiler.Compile(source);
        AbilityDefinition result = ConstructionEditing.Copy(source);
        BodyActionDefinition body = result.Body;
        float release = projection.ReleaseAt == -1f ? original.Body.ActiveUntil : projection.ReleaseAt;
        if (float.IsNaN(release) || float.IsInfinity(release) || release <= body.ActiveFrom || release > original.Body.Duration ||
            float.IsNaN(projection.Seconds) || float.IsInfinity(projection.Seconds) || projection.Seconds <= 0f)
            throw new InvalidDataException("离手必须在实际发力开始后、收势结束前，并提供有效的投送时长");
        if (projection.Rotation?.Length != 3 || projection.Rotation.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            throw new InvalidDataException("显化朝向需要三个有限转角");
        Quaternion orientation = Quaternion.Euler(projection.Rotation[0], projection.Rotation[1], projection.Rotation[2]);
        Vector3 velocity = Quaternion.Inverse(orientation) * Vector3.forward * projection.Speed;
        if (body.Hold != null && release < body.Hold.At + body.Hold.Seconds)
            throw new InvalidDataException("显化离手应在本次身体维持结束后");
        body.ActiveUntil = release - (body.Hold?.Seconds ?? 0f);
        result.Lifetime = Math.Max(original.Body.Duration, release + projection.Seconds);
        result.Resource = projection.Resource; result.Cost = projection.Cost;
        result.TargetRange = Math.Abs(projection.Speed) * projection.Seconds;
        result.Binding = new ConstructionBinding { Anchor = AbilityAnchor.Hand, Follow = true, ScaleByBody = true };
        result.Root = new ConstructionNode
        {
            Id = "manifestation", Form = projection.Form ?? body.WeaponForm ?? "hand", Matter = projection.Matter,
            State = projection.State, Scale = projection.Scale, Rotation = (float[])projection.Rotation.Clone(), ActiveFrom = release, ContactGroup = "body",
            OnContact = ConstructionContactResponse.End,
            Operations = new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.Grow, At = body.ActiveFrom, Seconds = Math.Max(.05f, body.ActiveUntil - body.ActiveFrom) },
                new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = release },
                new ConstructionOperation { Kind = ConstructionOperationKind.Fly, At = release, Vector = new[] { velocity.x, velocity.y, velocity.z }, TurnRate = projection.TurnRate, InheritVelocity = false }
            }
        };
        AbilityCompiler.Compile(result);
        return result;
    }
}
