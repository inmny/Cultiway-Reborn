using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using UnityEngine;
using strings;

namespace Cultiway.Content.Skills;

/// <summary>在点名对象真实接触时沿用原修炼反应。</summary>
internal enum FormationTargetEffect
{
    /// <summary>沿用普通接触结算。</summary>
    None,
    /// <summary>同源破甲再次接触时追加伤害。</summary>
    ArmorBreak,
    /// <summary>施加原木行周期伤害。</summary>
    Poison,
    /// <summary>首次接触减速，已有同源减速时冻结。</summary>
    FrostBind,
    /// <summary>施加原火行周期伤害。</summary>
    Burn,
    /// <summary>清除点名目标的同源灼烧并展开平面爆发。</summary>
    EmberBurst,
    /// <summary>点名接触展开平面凝元爆发。</summary>
    CondensedRelease,
    /// <summary>在反打伤害后推开近身攻击者。</summary>
    BodyCounter,
    /// <summary>随实际飞行命中原攻击目标。</summary>
    SwordChase,
    /// <summary>飞行命中后汲灵，余额不足时施加衰弱与阴行伤害。</summary>
    YinDrain,
    /// <summary>回补木毒击杀者的生命和灵气。</summary>
    LifeReturn,
    /// <summary>移除一项负面状态并恢复生命。</summary>
    YangCleanse
}

/// <summary>点名反应和本次发射冻结的修炼倍率。</summary>
internal readonly struct FormationTargetContact
{
    /// <summary>实际接触提交的修炼反应。</summary>
    internal readonly FormationTargetEffect Effect;
    /// <summary>本次原修炼来源和载体合计的作用倍率。</summary>
    internal readonly float EffectScale;

    /// <summary>原派生按地面半径判断，不用三维球体改变圈外或空中的受击规则。</summary>
    internal bool UsesPlanarSpread => Effect is FormationTargetEffect.EmberBurst or FormationTargetEffect.CondensedRelease;

    /// <summary>只有旧追踪投射在目标中途失效后取消，不改变已显形或其他自由构造。</summary>
    internal bool TracksTarget => Effect is FormationTargetEffect.SwordChase or FormationTargetEffect.YinDrain;

    /// <summary>保存实际发射的作用和倍率，结算时不重新计算。</summary>
    /// <param name="effect">点名反应。</param>
    /// <param name="effectScale">本次实际作用倍率。</param>
    internal FormationTargetContact(FormationTargetEffect effect, float effectScale)
    {
        Effect = effect;
        EffectScale = effectScale;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>对真实接触者施加状态，同源条件继续使用共享状态的来源规则。</summary>
    /// <param name="owner">点名出手的修炼者及本次强度。</param>
    /// <param name="target">共同作用面已经确认的人物。</param>
    /// <param name="targetId">接触时确认的人物身份。</param>
    /// <param name="contact">本次形成反应和已经确定的倍率。</param>
    /// <param name="settled">点名接触已经完成的原伤害。</param>
    private static ConstructedTargetResponse ApplyFormationTargetContact(CastAuthority owner, Actor target, long targetId,
        FormationTargetContact contact, DamageSettlement settled)
    {
        if (!owner.IsLive || target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
            return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, damage: settled);
        Actor source = owner.Actor;
        float strength = owner.Damage.Damage;
        switch (contact.Effect)
        {
            case FormationTargetEffect.ArmorBreak:
                bool alreadyBroken = CombatStatusEffects.HasStatus(target, StatusEffects.ArmorBreak, source);
                CombatStatusEffects.ApplyStatus(target, StatusEffects.ArmorBreak, 4f, source);
                if (!alreadyBroken) break;
                return ResolveFormationReactionDamage(owner, target, strength * .25f, ElementComposition.Static.Iron, settled);
            case FormationTargetEffect.Poison:
                CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Poison, 5f, strength * .35f / 5f,
                    ElementComposition.Static.Wood, source);
                break;
            case FormationTargetEffect.FrostBind:
                bool slowed = CombatStatusEffects.HasStatus(target, StatusEffects.Slow, source);
                CombatStatusEffects.ApplyStatus(target, slowed ? StatusEffects.Freeze : StatusEffects.Slow,
                    slowed ? .75f : 3f, source);
                break;
            case FormationTargetEffect.Burn:
                CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Burn, 4f, strength * .2f / 4f,
                    ElementComposition.Static.Fire, source);
                break;
            case FormationTargetEffect.EmberBurst:
                CombatStatusEffects.RemoveStatus(target, StatusEffects.Burn, source);
                break;
            case FormationTargetEffect.BodyCounter:
                Vector2 direction = target.current_position - source.current_position;
                if (direction.sqrMagnitude < .0001f) direction = Vector2.up;
                Vector2 force = direction.normalized * (2f * contact.EffectScale);
                Vector3 before = ConstructedActorImpulse.Velocity(target);
                bool pushed = ApplyLegacyGroundSlide(target, source, force.x, force.y);
                return new ConstructedTargetResponse(targetId, ConstructedActorImpulse.Velocity(target) - before,
                    pushed, 0f, 0f, damage: settled);
            case FormationTargetEffect.YinDrain:
                long generation = worldGeneration;
                float drained = DrainFormationWakan(source, target, 8f * contact.EffectScale);
                if (drained > 0f || generation != worldGeneration) break;
                CombatStatusEffects.ApplyStatus(target, StatusEffects.Weaken, 4f, source);
                return ResolveFormationReactionDamage(owner, target, strength * .25f, owner.Damage.Element, settled);
            case FormationTargetEffect.LifeReturn:
                CombatResourceEffects.RestoreHealth(source, source.stats[S.health] * .025f * contact.EffectScale);
                CombatResourceEffects.RestoreWakan(source, 6f * contact.EffectScale);
                break;
            case FormationTargetEffect.YangCleanse:
                CombatStatusEffects.CleanseNegativeStatuses(source, 1);
                float maximum = Mathf.Max(1f, source.stats[S.health]);
                CombatResourceEffects.RestoreHealth(source, Mathf.Min(maximum * .075f,
                    maximum * .03f * contact.EffectScale));
                break;
        }

        return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, damage: settled);
    }

    /// <summary>点名反应继续使用相同受击、境界和反应归属。</summary>
    /// <param name="owner">当前有效控制来源。</param><param name="target">真实接触者。</param>
    /// <param name="amount">原额外伤害。</param><param name="element">实际伤害组成。</param>
    /// <param name="settled">已由本体结算的伤害。</param>
    private static ConstructedTargetResponse ResolveFormationReactionDamage(CastAuthority owner, Actor target, float amount,
        ElementComposition element, DamageSettlement settled)
    {
        var damage = CombatDamageEffects.PrepareDamage(owner.Actor, amount, element,
            attackerPowerLevel: owner.Damage.AttackerPowerLevel, damageOrigin: DamageOrigin.Reaction);
        damage.TargetID = target.id;
        var result = GetHitEventSystem.Resolve(damage);
        return new ConstructedTargetResponse(damage.TargetID, Vector3.zero, false, 0f, 0f,
            additionalDamage: damage.Damage > 0f, damage: settled + result);
    }
}
