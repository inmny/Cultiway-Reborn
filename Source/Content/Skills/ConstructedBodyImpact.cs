using System;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实环境撞击的确认记录；基础撞伤仍经过正式防御，不代表最终扣血。</summary>
public readonly struct ConstructedBodyImpact
{
    /// <summary>原施放、来源与实际被撞身体。</summary>
    public readonly long CastId, SourceId, TargetId;
    /// <summary>实际环境身份；地面为零。</summary>
    public readonly long EnvironmentOwner;
    /// <summary>实际接触的共同时间。</summary>
    public readonly double WorldTime;
    /// <summary>身体上的接触点、环境点、环境朝向身体的法线及身体接触点速度。</summary>
    public readonly Vector3 BodyPoint, EnvironmentPoint, Normal, Velocity;
    /// <summary>朝向环境的速度和未减伤的撞伤。</summary>
    public readonly float ClosingSpeed, Damage;
    /// <summary>抓持取得时冻结的来源层次，不依赖来源招式继续存在。</summary>
    public readonly float? SourcePowerLevel;

    /// <summary>只从实际接触与冻结来源复制。</summary>
    /// <param name="source">实际抓持来源。</param><param name="hit">真实环境接触。</param>
    /// <param name="kinetics">真实运动作用。</param><param name="time">共同时间。</param>
    internal ConstructedBodyImpact(in BodyImpactSource source, in BodySurfaceHit hit,
        in BodyImpactKinetics kinetics, double time)
    {
        CastId = source.CastId; SourceId = source.SourceId; TargetId = source.TargetId;
        EnvironmentOwner = hit.Owner; WorldTime = time; BodyPoint = hit.BodyPoint;
        EnvironmentPoint = hit.Point; Normal = hit.Normal; Velocity = kinetics.Velocity;
        ClosingSpeed = kinetics.ClosingSpeed; Damage = kinetics.Damage;
        SourcePowerLevel = source.Damage.AttackerPowerLevel;
    }
}

/// <summary>抓持实际成立时冻结归属和撞伤参数，不延长人物或整次施放的寿命。</summary>
internal readonly struct BodyImpactSource
{
    /// <summary>原施放、来源、被控制身体及所属世界。</summary>
    internal readonly long CastId, SourceId, TargetId, Generation;
    /// <summary>同一正式伤害来源模板。</summary>
    internal readonly GetHitEvent Damage;
    /// <summary>当次实际质量及明确撞伤倍率。</summary>
    internal readonly float Mass, Scale;

    /// <summary>只在真实抓住后建立。</summary>
    /// <param name="cast">施放。</param><param name="source">来源。</param><param name="target">身体。</param>
    /// <param name="generation">所属世界。</param><param name="damage">正式伤害归属。</param>
    /// <param name="mass">实际质量。</param><param name="scale">构造撞伤倍率。</param>
    internal BodyImpactSource(long cast, long source, long target, long generation, GetHitEvent damage,
        float mass, float scale)
    {
        CastId = cast; SourceId = source; TargetId = target; Generation = generation;
        Damage = damage; Mass = mass; Scale = scale;
    }
}

/// <summary>抓持撞击和离手撞击共用正式受击规则。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>实际接触完成标准受击处理后发布，包括零撞伤的接触。</summary>
    public static event Action<ConstructedBodyImpact> BodyImpactConfirmed;

    /// <summary>供受力归还识别回调中真正发生的换世界。</summary>
    internal static long BodyWorldGeneration => worldGeneration;

    /// <summary>先用真实速度取得作用量，再走标准伤害，最后报告当前世界中的确认记录。</summary>
    /// <param name="source">实际抓持冻结的来源。</param><param name="hit">真实环境接触。</param>
    /// <param name="center">身体实际中心。</param><param name="linear">中心速度。</param>
    /// <param name="angular">世界角速度。</param><param name="time">共同时间。</param>
    /// <returns>外部回调后是否仍为同一世界。</returns>
    internal static bool ResolveBodyImpact(in BodyImpactSource source, in BodySurfaceHit hit, Vector3 center,
        Vector3 linear, Vector3 angular, double time)
    {
        if (source.Generation != worldGeneration) return false;
        var kinetics = new BodyImpactKinetics(center, linear, angular, hit.BodyPoint, hit.Normal, source.Mass, source.Scale);
        GetHitEvent damage = source.Damage;
        damage.TargetID = source.TargetId;
        damage.Damage = kinetics.Damage;
        damage.Element = ElementComposition.Static.empty;
        damage.IgnoreDamageReduction = false;
        damage.AttackType = AttackType.Gravity;
        damage.DamageOrigin = DamageOrigin.Reaction;
        GetHitEventSystem.Resolve(damage);
        if (source.Generation != worldGeneration) return false;
        BodyImpactConfirmed?.Invoke(new ConstructedBodyImpact(source, hit, kinetics, time));
        return source.Generation == worldGeneration;
    }
}
