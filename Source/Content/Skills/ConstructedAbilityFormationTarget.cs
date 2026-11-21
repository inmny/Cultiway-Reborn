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
    /// <param name="contact">本次形成反应和已经确定的倍率。</param>
    private static ConstructedTargetResponse ApplyFormationTargetContact(CastAuthority owner, Actor target,
        FormationTargetContact contact)
    {
        if (!owner.IsLive) return default;
        Actor source = owner.Actor;
        float strength = owner.Damage.Damage;
        switch (contact.Effect)
        {
            case FormationTargetEffect.ArmorBreak:
                bool alreadyBroken = CombatStatusEffects.HasStatus(target, StatusEffects.ArmorBreak, source);
                CombatStatusEffects.ApplyStatus(target, StatusEffects.ArmorBreak, 4f, source);
                if (!alreadyBroken) break;
                var damage = CombatDamageEffects.PrepareDamage(source, strength * .25f, ElementComposition.Static.Iron,
                    attackerPowerLevel: owner.Damage.AttackerPowerLevel, damageOrigin: DamageOrigin.Reaction);
                damage.TargetID = target.id;
                var result = GetHitEventSystem.Resolve(damage);
                return new ConstructedTargetResponse(damage.TargetID, Vector3.zero, false, 0f, 0f,
                    additionalDamage: damage.Damage > 0f, damage: result);
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

        return default;
    }
}
