using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>化神自愿承载、无身夺舍和确定性本相塑体的提交入口。</summary>
public static class YuanshenBodyRecoveryService
{
    /// <summary>化神夺舍必须保持的近距。</summary>
    public const float PossessionRange = 6f;

    /// <summary>化神夺舍引导时长。</summary>
    public const double PossessionChannelDuration = 2d * Cultiway.Const.TimeScales.SecPerMonth;

    /// <summary>夺舍失败后的冷却。</summary>
    public const double PossessionFailureCooldown = 2d * Cultiway.Const.TimeScales.SecPerYear;

    /// <summary>本相塑体需要推进的世界时间。</summary>
    public const double ReconstructionDuration = 3d * Cultiway.Const.TimeScales.SecPerYear;

    /// <summary>夺舍开始消耗的最大灵气比例。</summary>
    private const float PossessionWakanRatio = 0.1f;

    /// <summary>夺舍者采用新肉身后的生命比例。</summary>
    private const float PossessionHealthRatio = 0.2f;

    /// <summary>新肉身初始灵气比例。</summary>
    private const float NewBodyWakanRatio = 0.1f;

    /// <summary>自愿承载同意持续时长。</summary>
    private const double ConsentDuration = 6d * Cultiway.Const.TimeScales.SecPerMonth;

    /// <summary>普通人物向明确友方无身化神作出限时自愿承载声明。</summary>
    /// <param name="host">自愿提供肉身的人物。</param>
    /// <param name="recipient">唯一被授权的无身化神。</param>
    /// <returns>宿主资格、友方关系和接收者状态均有效时返回真。</returns>
    public static bool TryOfferBody(Actor host, Actor recipient)
    {
        if (!IsEligibleHost(host) || recipient == null || recipient.isRekt() ||
            host.canAttackTarget(recipient) || !YuanshenLifecycleService.IsBodiless(recipient.GetExtend()))
            return false;
        ActorExtend recipientExtend = recipient.GetExtend();
        if (!recipientExtend.TryGetComponent(out Yuanshen yuanshen) || !yuanshen.formation.IsValid) return false;
        host.GetExtend().GetOrAddComponent<YuanshenBodyConsent>() = new YuanshenBodyConsent
        {
            recipient_actor_id = recipient.data.id,
            expires_at = Now + ConsentDuration
        };
        return true;
    }

    /// <summary>开始一次只针对明确宿主的化神夺舍引导。</summary>
    /// <param name="source">无身元神本体和资源支付者。</param>
    /// <param name="target">明确宿主人物。</param>
    /// <returns>全部资格、距离、冷却、资源和排他锁均满足时返回真。</returns>
    public static bool TryStartPossession(ActorExtend source, Actor target)
    {
        if (!CanStartPossession(source, target, out PhysicalBodySnapshot body, out bool voluntary) ||
            !WakanResourceService.TrySpendMaximumRatio(source, PossessionWakanRatio)) return false;
        ref Yuanshen yuanshen = ref source.GetComponent<Yuanshen>();
        SoulContestResult contest = ResolveHuashenContest(source, target.GetExtend(), in yuanshen, voluntary);
        long token = CreateToken(source.Base.data.id, target.data.id);
        double now = Now;
        source.E.AddComponent(new YuanshenPossessionState
        {
            target_actor_id = target.data.id,
            token = token,
            completes_at = now + PossessionChannelDuration,
            body = body.DeepClone(),
            compatibility = contest.Compatibility,
            success_chance = contest.SuccessChance,
            voluntary = voluntary
        });
        target.GetExtend().E.AddComponent(new YuanshenBodyTransferLock
        {
            source_actor_id = source.Base.data.id,
            token = token
        });
        ref YuanshenBodyRecoveryRuntime runtime = ref source.GetOrAddComponent<YuanshenBodyRecoveryRuntime>();
        runtime.possession_ready_at = now + PossessionFailureCooldown;
        source.Base.cancelAllBeh();
        source.Base.beh_actor_target = target;
        source.Base.clearTileTarget();
        if (voluntary)
        {
            target.cancelAllBeh();
            target.clearAttackTarget();
            target.clearTileTarget();
        }
        return true;
    }

    /// <summary>取消人物当前身体转移引导并清除双方锁。</summary>
    /// <param name="source">正在引导的无身元神。</param>
    /// <returns>原本存在有效引导状态时返回真。</returns>
    public static bool CancelPossession(ActorExtend source)
    {
        if (source == null || !source.TryGetComponent(out YuanshenPossessionState state)) return false;
        ClearTargetLock(state.target_actor_id, source.Base.data.id, state.token);
        source.E.RemoveComponent<YuanshenPossessionState>();
        source.Base.beh_actor_target = null;
        return true;
    }

    /// <summary>开始以元神初成本相确定性重塑肉身。</summary>
    /// <param name="actor">无身元神本体。</param>
    /// <returns>九层元神、稳定法器锚点、创伤、战斗和灵气门槛均满足时返回真。</returns>
    public static bool TryStartReconstruction(ActorExtend actor)
    {
        if (actor == null || !YuanshenLifecycleService.IsBodiless(actor) ||
            actor.HasComponent<YuanshenPossessionState>() || actor.HasComponent<YuanshenReconstructionState>() ||
            actor.HasComponent<YuanshenBodilessTransitState>() ||
            !actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 9 ||
            !yuanshen.original_body.IsValid ||
            !PhysicalBodyService.TryResolve(yuanshen.original_body, out _, out _, out _) ||
            YuanshenTravelService.HasDetachedNodes(actor) ||
            !YuanshenArtifactAnchorService.TryResolve(actor, out Entity anchorArtifact, out Vector3 anchorPosition) ||
            Vector2.Distance(actor.Base.current_position, anchorPosition) > PossessionRange ||
            actor.Base.has_attack_target || actor.Base.isJustAttacked()) return false;
        if (actor.TryGetComponent(out YuanshenRuntimeState mind) && mind.injury_locked_share > 15f) return false;
        ref YuanshenBodyRecoveryRuntime runtime = ref actor.GetOrAddComponent<YuanshenBodyRecoveryRuntime>();
        double now = Now;
        if (runtime.reconstruction_ready_at > now || !actor.HasCultisys<Xian>()) return false;
        float maximumWakan = Mathf.Max(1f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        ref Xian xian = ref actor.GetCultisys<Xian>();
        if (xian.wakan < maximumWakan * 0.9f) return false;
        actor.E.AddComponent(new YuanshenReconstructionState
        {
            body = yuanshen.original_body.DeepClone(),
            formation = yuanshen.formation.DeepClone(),
            anchor_artifact_entity_id = anchorArtifact.Id,
            anchor_token = actor.GetComponent<YuanshenArtifactAnchorState>().generation,
            last_updated_at = now,
            last_interrupted_at = now - Cultiway.Const.TimeScales.SecPerMonth,
            required_wakan = maximumWakan * 3f
        });
        runtime.reconstruction_ready_at = now + Cultiway.Const.TimeScales.SecPerYear;
        actor.Base.cancelAllBeh();
        actor.Base.clearAttackTarget();
        actor.Base.clearTileTarget();
        return true;
    }

    /// <summary>主动取消本相塑体并承担有限进度损失。</summary>
    /// <param name="actor">正在塑体的无身元神。</param>
    /// <param name="penalize">是否施加神魂创伤。</param>
    /// <returns>原本存在塑体状态时返回真。</returns>
    public static bool CancelReconstruction(ActorExtend actor, bool penalize)
    {
        if (actor == null || !actor.HasComponent<YuanshenReconstructionState>()) return false;
        actor.E.RemoveComponent<YuanshenReconstructionState>();
        if (penalize)
            CombatStatusEffects.ApplyStatus(
                actor.Base,
                StatusEffects.SoulTrauma,
                3f * Cultiway.Const.TimeScales.SecPerMonth,
                actor.Base);
        return true;
    }

    /// <summary>检查一次进行中的夺舍是否仍能继续引导。</summary>
    /// <param name="source">无身元神。</param>
    /// <param name="state">冻结引导状态。</param>
    /// <param name="target">返回仍有效宿主。</param>
    /// <returns>双方身份、排他锁、资格与距离仍有效时返回真。</returns>
    public static bool TryValidatePossession(
        ActorExtend source,
        in YuanshenPossessionState state,
        out Actor target)
    {
        target = World.world?.units?.get(state.target_actor_id);
        if (source == null || !YuanshenLifecycleService.IsBodiless(source) || target == null || target.isRekt() ||
            !IsEligibleHost(target, source.Base.data.id, state.token) ||
            Vector2.Distance(source.Base.current_position, target.current_position) > PossessionRange ||
            !target.GetExtend().TryGetComponent(out YuanshenBodyTransferLock targetLock) ||
            targetLock.source_actor_id != source.Base.data.id || targetLock.token != state.token)
            return false;
        if (!state.voluntary) return true;
        return HasConsent(target, source.Base.data.id);
    }

    /// <summary>在引导完成时结算成功或反噬。</summary>
    /// <param name="source">无身元神。</param>
    /// <param name="target">仍有效宿主。</param>
    /// <param name="state">冻结引导状态。</param>
    /// <returns>成功采用肉身时返回真。</returns>
    public static bool ResolvePossession(
        ActorExtend source,
        Actor target,
        in YuanshenPossessionState state)
    {
        if (!TryValidatePossession(source, state, out Actor current) || current != target)
        {
            CancelPossession(source);
            return false;
        }
        bool succeeded = state.voluntary || Randy.randomChance(Mathf.Clamp01(state.success_chance));
        if (!succeeded)
        {
            FailPossession(source, target, state.token);
            return false;
        }
        if (!PhysicalBodyService.TryResolve(state.body, out _, out _, out _) ||
            !PhysicalBodyService.MatchesSnapshot(target, state.body) ||
            !PhysicalBodyService.TryApply(source.Base, state.body) ||
            !PhysicalBodyService.TerminateHostForTransfer(target, source.Base))
        {
            CancelPossession(source);
            return false;
        }
        if (source.HasComponent<BodylessYuanshenState>()) source.E.RemoveComponent<BodylessYuanshenState>();
        if (source.HasComponent<YuanshenPossessionState>()) source.E.RemoveComponent<YuanshenPossessionState>();
        source.Base.beh_actor_target = null;
        source.Base.setHealth(Mathf.Max(1, Mathf.RoundToInt(source.Base.getMaxHealth() * PossessionHealthRatio)));
        SetWakanRatio(source, NewBodyWakanRatio);
        float disharmony = Cultiway.Const.TimeScales.SecPerYear * Mathf.Lerp(2f, 0.5f, state.compatibility);
        CombatStatusEffects.ApplyStatus(
            source.Base,
            StatusEffects.BodyDisharmony,
            disharmony,
            source.Base);
        CombatStatusEffects.ApplyStatus(
            source.Base,
            StatusEffects.SoulTrauma,
            Cultiway.Const.TimeScales.SecPerYear,
            source.Base);
        ModClass.LogInfo($"化神夺舍完成: source={source.Base.data.id}, host={state.target_actor_id}");
        return true;
    }

    /// <summary>完成已经支付并推进到期的本相肉身重塑。</summary>
    /// <param name="actor">无身元神。</param>
    /// <param name="state">冻结塑体状态。</param>
    /// <returns>本相严格解析并采用成功时返回真。</returns>
    public static bool CompleteReconstruction(
        ActorExtend actor,
        in YuanshenReconstructionState state)
    {
        if (actor == null || !YuanshenLifecycleService.IsBodiless(actor) ||
            !actor.TryGetComponent(out Yuanshen current) ||
            current.formation.source_signature != state.formation.source_signature ||
            !PhysicalBodyService.TryApply(actor.Base, state.body)) return false;
        actor.E.RemoveComponent<BodylessYuanshenState>();
        actor.E.RemoveComponent<YuanshenReconstructionState>();
        actor.Base.setHealth(Mathf.Max(1, Mathf.RoundToInt(actor.Base.getMaxHealth() * 0.25f)));
        SetWakanRatio(actor, NewBodyWakanRatio);
        CombatStatusEffects.ApplyStatus(
            actor.Base,
            StatusEffects.BodyReconstructionWeakness,
            Cultiway.Const.TimeScales.SecPerYear,
            actor.Base);
        ModClass.LogInfo($"元神本相塑体完成: actor={actor.Base.data.id}");
        return true;
    }

    /// <summary>判断一名人物能否作为化神身体转移宿主。</summary>
    /// <param name="target">候选宿主人物。</param>
    /// <returns>目标有实际智慧肉身且严格低于化神时返回真。</returns>
    public static bool IsEligibleHost(Actor target)
    {
        return IsEligibleHost(target, 0L, 0L);
    }

    /// <summary>判断候选宿主是否适合指定提交。</summary>
    private static bool IsEligibleHost(Actor target, long sourceActorId, long token)
    {
        if (target == null || target.isRekt() || !target.isAlive() || target.current_tile == null ||
            target.asset == null || !target.asset.has_soul || !target.isSapient() || target.isInsideSomething())
            return false;
        ActorExtend extend = target.GetExtend();
        if (!extend.HasElementRoot() || extend.HasComponent<BodylessYuanshenState>() ||
            extend.HasComponent<YuanyingSoulState>() ||
            extend.TryGetComponent(out Yuanshen targetYuanshen) && targetYuanshen.formation.IsValid ||
            extend.TryGetComponent(out Xian xian) && xian.CurrLevel >= Cultiway.Content.Const.XianLevels.Huashen)
            return false;
        if (!extend.TryGetComponent(out YuanshenBodyTransferLock currentLock)) return true;
        return currentLock.source_actor_id == sourceActorId && currentLock.token == token;
    }

    /// <summary>检查开始夺舍的全部不变量并冻结宿主肉身。</summary>
    private static bool CanStartPossession(
        ActorExtend source,
        Actor target,
        out PhysicalBodySnapshot body,
        out bool voluntary)
    {
        body = default;
        voluntary = false;
        if (source == null || !YuanshenLifecycleService.IsBodiless(source) ||
            source.HasComponent<YuanshenPossessionState>() || source.HasComponent<YuanshenReconstructionState>() ||
            source.HasComponent<YuanshenBodilessTransitState>() ||
            !source.TryGetComponent(out Yuanshen yuanshen) || !yuanshen.formation.IsValid ||
            !IsEligibleHost(target) || target == source.Base ||
            Vector2.Distance(source.Base.current_position, target.current_position) > PossessionRange ||
            !PhysicalBodyService.TryCapture(target, out body) ||
            !PhysicalBodyService.TryResolve(body, out _, out _, out _)) return false;
        ref YuanshenBodyRecoveryRuntime runtime = ref source.GetOrAddComponent<YuanshenBodyRecoveryRuntime>();
        if (runtime.possession_ready_at > Now) return false;
        voluntary = HasConsent(target, source.Base.data.id);
        return voluntary || source.Base.canAttackTarget(target);
    }

    /// <summary>计算化神夺舍的相性、攻防和成功率。</summary>
    private static SoulContestResult ResolveHuashenContest(
        ActorExtend source,
        ActorExtend target,
        in Yuanshen yuanshen,
        bool voluntary)
    {
        float compatibility = SoulContestResolver.CalculateCompatibility(yuanshen.formation, target.GetElementRoot());
        float attack = Mathf.Max(1f, yuanshen.strength) * (1f + yuanshen.stage * 0.12f) +
                       Mathf.Max(0f, source.Base.stats[nameof(WorldboxGame.BaseStats.DivineSense)]) * 0.8f +
                       Mathf.Max(0f, source.Base.stats[WorldboxGame.BaseStats.MaxSoul.id]) * 0.04f;
        float defense = ResolveSoulDefense(target);
        float healthRatio = target.Base.getMaxHealth() > 0f
            ? Mathf.Clamp01(target.Base.getHealth() / target.Base.getMaxHealth())
            : 0f;
        if (target.Base.is_unconscious) defense *= 0.45f;
        if (CombatStatusEffects.HasStatus(target.Base, StatusEffects.SoulTrauma)) defense *= 0.65f;
        defense *= Mathf.Lerp(0.6f, 1.25f, healthRatio);
        float ratio = attack / Mathf.Max(1f, attack + defense);
        float chance = voluntary
            ? 1f
            : Mathf.Clamp(ratio * (0.5f + compatibility * 0.75f), 0.05f, 0.95f);
        return new SoulContestResult(attack, defense, compatibility, chance);
    }

    /// <summary>按人物当前神识、魂魄、境界和元神计算被动神魂防御。</summary>
    /// <param name="target">需要抵抗夺舍的人物。</param>
    /// <returns>不低于一的神魂防御强度。</returns>
    private static float ResolveSoulDefense(ActorExtend target)
    {
        if (target == null || target.Base == null || target.Base.isRekt()) return 1f;
        float divineSense = Mathf.Max(0f, target.Base.stats[nameof(WorldboxGame.BaseStats.DivineSense)]);
        float maxSoul = Mathf.Max(0f, target.Base.stats[WorldboxGame.BaseStats.MaxSoul.id]);
        float strength = divineSense * 0.55f + maxSoul * 0.03f + target.GetPowerLevel() * 8f;
        if (target.TryGetComponent(out Yuanshen targetYuanshen))
            strength += targetYuanshen.strength * (1f + targetYuanshen.stage * 0.08f);
        if (target.TryGetComponent(out YuanshenRuntimeState runtime))
            strength += Mathf.Max(runtime.main_soul_share, runtime.body_residual_share) * 0.35f;
        return Mathf.Max(1f, strength);
    }

    /// <summary>夺舍失败时清理锁、锁定心神份额并造成真实神魂伤害。</summary>
    private static void FailPossession(ActorExtend source, Actor target, long token)
    {
        ClearTargetLock(target.data.id, source.Base.data.id, token);
        if (source.HasComponent<YuanshenPossessionState>()) source.E.RemoveComponent<YuanshenPossessionState>();
        float locked = YuanshenTravelService.LockMainMindShare(source, 25f);
        CombatStatusEffects.ApplyStatus(
            source.Base,
            StatusEffects.SoulTrauma,
            2f * Cultiway.Const.TimeScales.SecPerYear,
            source.Base);
        float damage = Mathf.Max(1f, source.Base.getMaxHealth() * 0.3f);
        SoulDamageService.Deal(target, source.Base, damage);
    }

    /// <summary>清除仍与指定提交一致的宿主锁。</summary>
    private static void ClearTargetLock(long targetId, long sourceId, long token)
    {
        Actor target = World.world?.units?.get(targetId);
        if (target == null || target.isRekt()) return;
        ActorExtend extend = target.GetExtend();
        if (extend.TryGetComponent(out YuanshenBodyTransferLock current) &&
            current.source_actor_id == sourceId && current.token == token)
            extend.E.RemoveComponent<YuanshenBodyTransferLock>();
    }

    /// <summary>判断宿主仍持有对指定元神的有效自愿承载声明。</summary>
    private static bool HasConsent(Actor host, long recipientActorId)
    {
        if (host == null || !host.GetExtend().TryGetComponent(out YuanshenBodyConsent consent)) return false;
        if (consent.expires_at <= Now)
        {
            host.GetExtend().E.RemoveComponent<YuanshenBodyConsent>();
            return false;
        }
        return consent.recipient_actor_id == recipientActorId;
    }

    /// <summary>把人物当前灵气设为上限的确定比例。</summary>
    private static void SetWakanRatio(ActorExtend actor, float ratio)
    {
        if (!actor.HasCultisys<Xian>()) return;
        ref Xian xian = ref actor.GetCultisys<Xian>();
        float amount = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]) * Mathf.Clamp01(ratio);
        WakanResourceService.Set(actor, ref xian, amount);
    }

    /// <summary>根据双方编号和世界时间创建当前世界内提交编号。</summary>
    private static long CreateToken(long sourceId, long targetId)
    {
        return unchecked(sourceId * 6364136223846793005L ^ targetId * 1442695040888963407L ^
                         (long)(Now * 1000d));
    }

    /// <summary>当前世界时间。</summary>
    private static double Now => World.world?.getCurWorldTime() ?? 0d;
}
