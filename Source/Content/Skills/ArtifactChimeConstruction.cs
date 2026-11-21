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

/// <summary>用本击实际的圆形作用面播放钟声和确认人物接触。</summary>
internal static class ArtifactChimeConstruction
{
    /// <summary>由原法钟入口预付并固定作用点，形体到达完整半径时只结算一次。</summary>
    /// <param name="context">原法宝、主人和实际施法载体。</param>
    /// <param name="asset">原钟声编号、名称和出手渠道。</param>
    /// <param name="target">原入口的此次调用资料。</param>
    /// <param name="origin">本次出手方式。</param>
    /// <param name="center">原先取敌的实际世界位置。</param>
    /// <param name="radius">这一击的原作用半径。</param>
    /// <param name="duration">本击的原持续时间。</param>
    /// <param name="contact">已冻结的接触结果参数。</param>
    /// <param name="element">震魂钟鸣的原伤害元素。</param><param name="damage">原伤害量。</param>
    internal static bool Start(ArtifactAbilityExecutionContext context, ArtifactAbilityAsset asset,
        in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin, Vector2 center, float radius, float duration,
        ArtifactChimeContact contact, ElementComposition element, float damage = 0f)
    {
        const float strike = .16f;
        const float dissolve = .25f;
        float life = Mathf.Max(duration, strike + dissolve + .06f);
        ActorExtend owner = context.controller.GetComponent<ActorBinder>().AE;
        var runtime = target.RuntimeData;
        runtime.Origin = origin;
        var aim = new ActiveAbilityTarget(null, center, runtimeData: runtime, worldScope: target.WorldScope);
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = asset.GetName(), Resource = "none", Cost = 0f, Cooldown = 0f, Lifetime = life,
            Use = new ConstructionUse { Channels = asset.active_use.channels, Target = ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Hostile, DamageScale = 0f },
            Root = new ConstructionNode
            {
                Id = "chime", Form = contact.Purifies ? "purifying_curtain" : "pressure_wave",
                Matter = "spirit", Scale = radius, Volume = 0f,
                ContactRole = ConstructionContactRole.Attack, ContactActorCenter = true, IgnoreFormClashes = true,
                ActiveFrom = strike, ActiveUntil = strike + .01f,
                Contact = new ConstructionContactPolicy
                {
                    Mode = ConstructionContactMode.Pulse, Interval = strike, MaximumPerTarget = 1
                },
                Operations =
                [
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = strike },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "dissolve",
                        At = life - dissolve, Seconds = dissolve }
                ]
            }
        };
        var hit = CombatDamageEffects.PrepareDamage(owner.Base, damage, element);
        contact = contact.WithDamage(hit.Damage);
        // 接触需要可持续一击的载体。无伤净化或零伤害钟声也会抵达实际目标。
        hit.Damage = Mathf.Max(1f, hit.Damage);
        return ConstructedAbilityService.StartPrepaid(owner, definition, aim, center, damage: hit,
            sourceInstance: context.artifact.Id, spatialSource: context.ResolveCarrierActor(),
            selectionAlreadyChecked: true, artifactChime: contact);
    }
}

/// <summary>在真实钟声接触时兑现原伤害、状态及净化，本次参数与物件后续更换无关。</summary>
internal readonly struct ArtifactChimeContact
{
    /// <summary>清音解除的最大数量，震魂钟鸣不需要此参数。</summary>
    internal readonly int? CleanseCount;
    /// <summary>每位敌人的原眩晕时长。</summary>
    internal readonly float Daze;
    /// <summary>出手时冻结的原震退力度。</summary>
    internal readonly float Force;
    /// <summary>取到实际施法载体倍率后的原伤害。</summary>
    internal readonly float Damage;
    /// <summary>本次是否为清音。</summary>
    internal bool Purifies => CleanseCount.HasValue;

    /// <summary>冻结震魂钟鸣的原作用参数。</summary>
    /// <param name="daze">实际眩晕时长。</param><param name="force">原震退力度。</param>
    internal ArtifactChimeContact(float daze, float force) : this(null, daze, force, 0f) { }

    /// <summary>冻结清音对单名目标的解除上限。</summary>
    /// <param name="count">原解除上限。</param>
    internal ArtifactChimeContact(int count) : this(count, 0f, 0f, 0f) { }

    /// <summary>保存本次准备完毕的原伤害和接触资料。</summary>
    /// <param name="count">清音的解除上限。</param><param name="daze">原眩晕时长。</param>
    /// <param name="force">实际震退力度。</param><param name="damage">实际来源伤害。</param>
    private ArtifactChimeContact(int? count, float daze, float force, float damage) =>
        (CleanseCount, Daze, Force, Damage) = (count, daze, force, damage);

    /// <summary>保存按原施法来源折算的本击伤害。</summary>
    /// <param name="damage">已准备的伤害。</param>
    internal ArtifactChimeContact WithDamage(float damage) => new(CleanseCount, Daze, Force, damage);
}
