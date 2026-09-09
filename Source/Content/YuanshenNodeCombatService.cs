using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>基础神念攻击、节点完整度和牵引切割的集中入口。</summary>
public static class YuanshenNodeCombatService
{
    /// <summary>基础神念攻击有效距离。</summary>
    public const float BasicStrikeRange = 12f;

    /// <summary>基础神念攻击冷却秒数。</summary>
    private const double BasicStrikeCooldown = 2d;

    /// <summary>基础神念攻击消耗的最大灵气比例。</summary>
    private const float BasicStrikeWakanRatio = 0.02f;

    /// <summary>返回基础神念攻击剩余冷却秒数。</summary>
    /// <param name="attacker">技能与冷却所属人物。</param>
    /// <returns>当前剩余秒数。</returns>
    public static float GetSoulStrikeCooldownRemaining(ActorExtend attacker)
    {
        if (attacker == null || !attacker.TryGetComponent(out YuanshenRuntimeState runtime)) return 0f;
        return Mathf.Max(0f, (float)(runtime.soul_strike_ready_at - ResolveNow()));
    }

    /// <summary>检查原人物是否能支付一次基础神念攻击。</summary>
    /// <param name="attacker">技能资源所属人物。</param>
    /// <returns>当前灵气不少于最大灵气的百分之二时返回真。</returns>
    public static bool CanPaySoulStrike(ActorExtend attacker)
    {
        if (attacker == null || !attacker.HasCultisys<Xian>()) return false;
        float cost = Mathf.Max(0f, attacker.Base.stats[BaseStatses.MaxWakan.id]) * BasicStrikeWakanRatio;
        return attacker.GetCultisys<Xian>().wakan + 0.0001f >= cost;
    }

    /// <summary>检查临时命魂是否能以基础神念攻击一个明确敌方人物。</summary>
    /// <param name="attacker">技能、冷却和资源所属人物。</param>
    /// <param name="target">准备攻击的人物。</param>
    /// <returns>魂体、敌我、距离、冷却和资源全部满足时返回真。</returns>
    public static bool CanSoulStrike(ActorExtend attacker, Actor target)
    {
        if (!CanUseSoulAbilities(attacker) || target == null || target.isRekt() || !target.isAlive() ||
            target == attacker.Base || !attacker.Base.canAttackTarget(target) ||
            GetSoulStrikeCooldownRemaining(attacker) > 0f || !CanPaySoulStrike(attacker)) return false;
        SkillCasterContext context = SkillCasterContextService.TryGetCurrent(attacker, out SkillCasterContext current)
            ? current
            : SkillCasterContextService.Resolve(attacker);
        return context.IsValid && context.Kind == SkillCarrierKind.Soul &&
               Vector2.Distance(context.Carrier.Base.current_position, target.current_position) <= BasicStrikeRange;
    }

    /// <summary>从临时命魂位置对一个明确敌方人物提交纯阴神念伤害。</summary>
    /// <param name="attacker">技能、冷却、资源和击杀归属所属人物。</param>
    /// <param name="target">实际承受神魂伤害的人物。</param>
    /// <returns>成功支付并提交伤害时返回真。</returns>
    public static bool TrySoulStrike(ActorExtend attacker, Actor target)
    {
        if (!CanSoulStrike(attacker, target) ||
            !WakanResourceService.TrySpendMaximumRatio(attacker, BasicStrikeWakanRatio)) return false;
        ref YuanshenRuntimeState runtime = ref attacker.GetOrAddComponent<YuanshenRuntimeState>();
        runtime.soul_strike_ready_at = ResolveNow() + BasicStrikeCooldown;
        float sourceStrength = attacker.TryGetComponent(out Yuanshen yuanshen)
            ? Mathf.Max(0.5f, yuanshen.strength)
            : 0.5f;
        float damage = SkillContext.DefaultStrength * sourceStrength;
        SkillCasterContext context = SkillCasterContextService.Resolve(attacker);
        BaseSimObject source = context.IsValid ? context.Carrier.Base : attacker.Base;
        return SoulDamageService.Deal(source, target, damage);
    }

    /// <summary>从人物当前命魂位置向一枚已经锁定的节点释放基础神念攻击。</summary>
    /// <param name="attacker">能力所有者和资源支付者。</param>
    /// <param name="target">已经锁定的稳定节点句柄。</param>
    /// <returns>资源、冷却、距离和锁定均有效并完成命中时返回真。</returns>
    public static bool TryBasicStrike(ActorExtend attacker, YuanshenNodeHandle target)
    {
        if (!CanUseSoulAbilities(attacker) || !TryGetOrigin(attacker, out Vector2 origin) ||
            !YuanshenNodeLockService.TryResolve(target, out Entity targetNode) ||
            !targetNode.TryGetComponent(out Position targetPosition) ||
            Vector2.Distance(origin, targetPosition.v2) > BasicStrikeRange ||
            target.OwnerActorId == attacker.Base.data.id ||
            !YuanshenNodeLockService.HasLock(attacker.Base, target))
            return false;

        ref YuanshenRuntimeState runtime = ref attacker.GetOrAddComponent<YuanshenRuntimeState>();
        double now = ResolveNow();
        if (runtime.soul_strike_ready_at > now || !WakanResourceService.TrySpendMaximumRatio(attacker, BasicStrikeWakanRatio)) return false;

        runtime.soul_strike_ready_at = now + BasicStrikeCooldown;
        ref YuanshenNodeState targetState = ref targetNode.GetComponent<YuanshenNodeState>();
        float sourceStrength = attacker.TryGetComponent(out Yuanshen yuanshen)
            ? Mathf.Max(0.25f, yuanshen.strength)
            : 0.25f;
        float damage = targetState.integrity_maximum * Mathf.Clamp(0.08f + sourceStrength * 0.01f, 0.08f, 0.2f);
        return ApplyNodeHit(attacker, target, damage, false, true);
    }

    /// <summary>从合法魂法向元神节点提交完整度伤害。</summary>
    /// <param name="attacker">攻击归属人物；无人物来源的合法效果可以为空。</param>
    /// <param name="target">稳定节点句柄。</param>
    /// <param name="rawDamage">未经过节点魂防的伤害。</param>
    /// <param name="cutsTether">本次命中是否专门切割牵引。</param>
    /// <param name="requireLock">是否要求攻击者持有未过期锁定。</param>
    /// <returns>节点合法且伤害已提交时返回真。</returns>
    public static bool ApplyNodeHit(
        ActorExtend attacker,
        YuanshenNodeHandle target,
        float rawDamage,
        bool cutsTether,
        bool requireLock)
    {
        if (rawDamage <= 0f || !YuanshenNodeLockService.TryResolve(target, out Entity node) ||
            !node.TryGetComponent(out YuanshenNodeState state) ||
            state.action == YuanshenNodeAction.Broken ||
            requireLock && (attacker == null ||
                !YuanshenNodeLockService.HasLock(attacker.Base, target)))
            return false;

        Actor ownerBase = World.world?.units?.get(state.owner_actor_id);
        if (ownerBase == null || ownerBase.isRekt())
        {
            YuanshenTravelService.RecycleInvalidNode(node);
            return false;
        }
        ActorExtend owner = ownerBase.GetExtend();
        float defenseSense = Mathf.Max(0f, ownerBase.stats[nameof(WorldboxGame.BaseStats.DivineSense)]);
        float damage = Mathf.Max(0.1f, rawDamage / (1f + defenseSense * 0.004f));
        float before = state.integrity_current;

        state.integrity_current = Mathf.Max(0f, state.integrity_current - damage);
        float actualDamage = before - state.integrity_current;
        if (actualDamage <= 0f) return false;

        float intendedLocked = state.allocated_share * (1f - state.IntegrityRatio);
        float newlyLocked = Mathf.Max(0f, intendedLocked - state.locked_share);
        if (newlyLocked > 0f)
        {
            state.locked_share += newlyLocked;
            state.mind_share = Mathf.Max(0f, state.mind_share - newlyLocked);
            LockInjuryShare(owner, newlyLocked);
        }

        state.tether_interference_seconds += cutsTether ? 1.25f : 0.35f;
        state.tether_last_interference_at = ResolveNow();
        state.tether_condition = ResolveTetherCondition(state.tether_interference_seconds);
        LockCounterparty(attacker, ownerBase, node);

        if (state.integrity_current > 0f) return true;
        ResolveBrokenNode(owner, node);
        return true;
    }

    /// <summary>完整度损失同步转为全局创伤锁定，保持心神总账恒等。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="share">本次锁定份额。</param>
    private static void LockInjuryShare(ActorExtend owner, float share)
    {
        ref YuanshenRuntimeState runtime = ref owner.GetOrAddComponent<YuanshenRuntimeState>();
        runtime.injury_locked_share = Mathf.Clamp(runtime.injury_locked_share + share, 0f, 100f);
        CombatStatusEffects.ApplyStatus(
            owner.Base,
            StatusEffects.SoulTrauma,
            Mathf.Max(TimeScales.SecPerMonth, share * TimeScales.SecPerMonth),
            owner.Base);
        YuanshenTravelService.NotifyMindStateChanged(owner);
    }

    /// <summary>节点击破后立即消散，剩余份额全部转为创伤锁定。</summary>
    /// <param name="owner">节点所属人物。</param>
    /// <param name="node">刚被击破的节点实体。</param>
    private static void ResolveBrokenNode(
        ActorExtend owner,
        Entity node)
    {
        YuanshenAdvancedNodeService.Disperse(owner, node, 1f);
    }

    /// <summary>一次神魂接触让交战双方取得对方节点的短时锁定。</summary>
    /// <param name="attacker">攻击归属人物。</param>
    /// <param name="targetOwner">受击节点所属人物。</param>
    /// <param name="targetNode">受击节点。</param>
    private static void LockCounterparty(ActorExtend attacker, Actor targetOwner, Entity targetNode)
    {
        if (attacker == null || attacker.Base == null || attacker.Base.isRekt()) return;
        if (targetNode.TryGetComponent(out YuanshenNodeState targetState))
            YuanshenNodeLockService.GrantLock(attacker.Base, targetState.GetHandle());
    }

    /// <summary>检查人物是否具备元神魂系能力的基本条件。</summary>
    /// <param name="actor">能力所有者。</param>
    /// <returns>有效化神或无身元神返回真。</returns>
    public static bool CanUseSoulAbilities(ActorExtend actor)
    {
        return actor != null && actor.Base != null && !actor.Base.isRekt() && actor.Base.isAlive() &&
               actor.HasCultisys<Xian>() && actor.GetCultisys<Xian>().CurrLevel >= Const.XianLevels.Huashen &&
               actor.TryGetComponent(out Yuanshen yuanshen) && yuanshen.formation.IsValid;
    }

    /// <summary>读取能力实际出现位置：离体命魂优先，否则使用原人物当前位置。</summary>
    /// <param name="actor">能力所有者。</param>
    /// <param name="origin">返回能力起点。</param>
    /// <returns>人物或命魂位置有效时返回真。</returns>
    private static bool TryGetOrigin(ActorExtend actor, out Vector2 origin)
    {
        if (YuanshenTravelService.TryGetMainSoulPosition(actor, out Vector3 position))
        {
            origin = position;
            return true;
        }
        origin = default;
        return false;
    }

    /// <summary>按累计干扰秒数取得牵引状态。</summary>
    /// <param name="seconds">累计干扰秒数。</param>
    /// <returns>对应牵引状态。</returns>
    private static YuanshenTetherCondition ResolveTetherCondition(float seconds)
    {
        return seconds switch
        {
            >= 3f => YuanshenTetherCondition.Severed,
            >= 2f => YuanshenTetherCondition.Obstructed,
            >= 0.75f => YuanshenTetherCondition.Fluctuating,
            _ => YuanshenTetherCondition.Stable
        };
    }

    /// <summary>取得当前世界时间。</summary>
    /// <returns>没有世界时返回零。</returns>
    private static double ResolveNow()
    {
        return World.world?.getCurWorldTime() ?? 0d;
    }
}
