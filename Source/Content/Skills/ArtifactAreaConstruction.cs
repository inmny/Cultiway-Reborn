using System;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>让法宝的一次圆面作用共用实际范围、可见形体和接触时刻。</summary>
internal static class ArtifactAreaConstruction
{
    /// <summary>原法宝入口负责费用。形体到达完整半径时在固定的位置结算一次。</summary>
    /// <param name="context">原法宝、主人和实际施法载体。</param>
    /// <param name="asset">原能力编号、名称和出手渠道。</param>
    /// <param name="target">原入口的此次调用资料。</param>
    /// <param name="origin">本次出手方式。</param>
    /// <param name="center">原先取敌的实际世界位置。</param>
    /// <param name="radius">这一击的原作用半径。</param>
    /// <param name="duration">本击的原持续时间。</param>
    /// <param name="contact">已冻结的接触结果参数。</param>
    /// <param name="form">完整作用面的显示与接触形体。</param>
    /// <param name="element">原伤害元素。</param><param name="damage">原伤害量。</param>
    /// <param name="matter">形体使用的已登记材料。</param><param name="strike">扩张到完整半径的时间。</param>
    /// <param name="selectedTargetOnly">圆面随已确认的目标移动，只接触这一人。</param>
    /// <param name="spin">圆面每秒绕中心旋转的角度。</param>
    /// <param name="commitEffect">共同施放确认受理时提交原来源的一次性消耗。</param>
    internal static bool Start(ArtifactAbilityExecutionContext context, ArtifactAbilityAsset asset,
        in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin, Vector2 center, float radius, float duration,
        ArtifactAreaContact contact, string form, ElementComposition element, float damage = 0f,
        string matter = "spirit", float strike = .16f, bool selectedTargetOnly = false, float spin = 0f,
        Func<bool> commitEffect = null)
    {
        const float dissolve = .25f;
        float life = Mathf.Max(duration, strike + dissolve + .06f);
        ActorExtend owner = context.controller.GetComponent<ActorBinder>().AE;
        var runtime = target.RuntimeData;
        runtime.Origin = origin;
        var aim = new ActiveAbilityTarget(selectedTargetOnly ? target.Object : null, center,
            runtimeData: runtime, worldScope: target.WorldScope);
        var growth = new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = strike };
        var fade = new ConstructionOperation
        {
            Kind = ConstructionOperationKind.Animate, Clip = "dissolve", At = life - dissolve, Seconds = dissolve
        };
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = asset.GetName(), Resource = "none", Cost = 0f, Cooldown = 0f, Lifetime = life,
            Use = new ConstructionUse
            {
                Channels = asset.active_use.channels,
                Target = selectedTargetOnly ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Point
            },
            Binding = new ConstructionBinding
            {
                Anchor = selectedTargetOnly ? AbilityAnchor.Target : AbilityAnchor.Source,
                Follow = selectedTargetOnly
            },
            Impact = new ConstructionImpact
            {
                Targets = contact.Disposition == CombatTargeting.TargetDisposition.Friendly
                    ? SkillUseTargetRelation.Friendly
                    : SkillUseTargetRelation.Hostile,
                DamageScale = 0f, SelectedTargetOnly = selectedTargetOnly
            },
            Root = new ConstructionNode
            {
                Id = "reach", Form = form, Matter = matter, Scale = radius, Volume = 0f,
                ContactRole = ConstructionContactRole.Attack, ContactActorCenter = true, IgnoreFormClashes = true,
                ActiveFrom = strike, ActiveUntil = strike + .01f,
                Contact = new ConstructionContactPolicy
                {
                    Mode = ConstructionContactMode.Pulse, Interval = strike, MaximumPerTarget = 1
                },
                Operations = spin == 0f ? [growth, fade] :
                    [growth, new ConstructionOperation { Kind = ConstructionOperationKind.Spin, Vector = [0f, 0f, spin] }, fade]
            }
        };
        var hit = CombatDamageEffects.PrepareDamage(owner.Base, damage, element);
        contact = contact.WithDamage(hit.Damage);
        // 圆面只依赖实际接触，无伤净化也会抵达实际目标。
        hit.Damage = Mathf.Max(1f, hit.Damage);
        Vector3? sourcePosition = selectedTargetOnly ? null : contact.Sector?.Mouth ?? (Vector3)center;
        Vector3? direction = contact.Sector is { } sector ? (Vector3)sector.Direction : null;
        return ConstructedAbilityService.StartPrepaid(owner, definition, aim, sourcePosition, damage: hit, direction: direction,
            sourceInstance: context.artifact.Id, spatialSource: context.ResolveCarrierActor(),
            selectionAlreadyChecked: true, artifactArea: contact, commitEffect: commitEffect);
    }
}

/// <summary>在真实圆面接触时兑现原伤害、震退或净化；参数在本次作用开始时冻结。</summary>
internal readonly struct ArtifactAreaContact
{
    /// <summary>一次净化解除的最大数量；伤害作用不需要此参数。</summary>
    internal readonly int? CleanseCount;
    /// <summary>每位敌人的原眩晕时长。</summary>
    internal readonly float Daze;
    /// <summary>出手时冻结的水平力度，负值表示朝实际作用中心牵引。</summary>
    internal readonly float Force;
    /// <summary>取到实际施法载体倍率后的原伤害。</summary>
    internal readonly float Damage;
    /// <summary>沿用原能力先伤害、再施加控制的顺序。</summary>
    internal readonly bool DamageFirst;
    /// <summary>本次确实接触人物时沿用来源的状态和资源结算。</summary>
    internal readonly Action<Actor, Actor> ApplyEffect;
    /// <summary>同一轮已确认的接触全部结算后，只提交一次的来源收益。</summary>
    internal readonly Action AfterPulse;
    /// <summary>接触继续按照原法宝的敌我关系筛选。</summary>
    internal readonly CombatTargeting.TargetDisposition Disposition;
    /// <summary>照影可接触友方与有敌意的人物，包括被隐身挡住的敌人。</summary>
    internal readonly bool RevealsConcealment;
    /// <summary>扇面额外约束实际方向，圆形粗筛仍按原半径。</summary>
    internal readonly ArtifactAreaSector? Sector;
    /// <summary>本次是否净化。</summary>
    internal bool Purifies => CleanseCount.HasValue;

    /// <summary>冻结原地面打击的眩晕和震退。</summary>
    /// <param name="daze">实际眩晕时长。</param><param name="force">原震退力度。</param>
    /// <param name="damageFirst">原能力是否先处理伤害。</param>
    internal ArtifactAreaContact(float daze, float force, bool damageFirst = false)
        : this(null, daze, force, 0f, damageFirst, null) { }

    /// <summary>冻结清音对单名目标的解除上限。</summary>
    /// <param name="count">原解除上限。</param>
    internal ArtifactAreaContact(int count) : this(count, 0f, 0f, 0f, false, null) { }

    /// <summary>保存原能力已经冻结的作用参数，由真正接触的人物触发。</summary>
    internal ArtifactAreaContact(Action<Actor, Actor> applyEffect,
        CombatTargeting.TargetDisposition disposition = CombatTargeting.TargetDisposition.Hostile,
        bool revealsConcealment = false, bool damageFirst = true, float force = 0f, ArtifactAreaSector? sector = null,
        Action afterPulse = null)
        : this(null, 0f, force, 0f, damageFirst, applyEffect, disposition, revealsConcealment, sector, afterPulse) { }

    /// <summary>保存本次准备完毕的原伤害和接触资料。</summary>
    /// <param name="count">清音的解除上限。</param><param name="daze">原眩晕时长。</param>
    /// <param name="force">实际震退力度。</param><param name="damage">实际来源伤害。</param>
    /// <param name="damageFirst">原能力是否先处理伤害。</param>
    private ArtifactAreaContact(int? count, float daze, float force, float damage, bool damageFirst,
        Action<Actor, Actor> applyEffect, CombatTargeting.TargetDisposition disposition = CombatTargeting.TargetDisposition.Hostile,
        bool revealsConcealment = false, ArtifactAreaSector? sector = null, Action afterPulse = null) =>
        (CleanseCount, Daze, Force, Damage, DamageFirst, ApplyEffect, Disposition, RevealsConcealment, Sector, AfterPulse) =
        (count, daze, force, damage, damageFirst, applyEffect, disposition, revealsConcealment, sector, afterPulse);

    /// <summary>保留本击实际作用，在人物中心的定时接触上增加扇角。</summary>
    internal ArtifactAreaContact WithSector(ArtifactAreaSector sector) =>
        new(CleanseCount, Daze, Force, Damage, DamageFirst, ApplyEffect, Disposition, RevealsConcealment, sector, AfterPulse);

    /// <summary>保存按原施法来源折算的本击伤害。</summary>
    /// <param name="damage">已准备的伤害。</param>
    internal ArtifactAreaContact WithDamage(float damage) =>
        new(CleanseCount, Daze, Force, damage, DamageFirst, ApplyEffect, Disposition, RevealsConcealment, Sector, AfterPulse);
}
