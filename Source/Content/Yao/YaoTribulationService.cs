using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Progression;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     妖丹天劫的唯一入口。劫云、落雷与承伤证据都在真实世界中结算；
///     只看真实发生的结果，不允许一次随机数代替整个天劫。
/// </summary>
public static class YaoTribulationService
{
    /// <summary>凝丹的原有劫雷波数。</summary>
    private const int TotalWaves = 5;
    /// <summary>同一渡劫者连续落雷的世界秒数。</summary>
    private const float StrikeInterval = 3f;
    /// <summary>凝丹劫的原最长时间。</summary>
    private const float TribulationTimeout = 240f;

    /// <summary>待成长流程领取的结果。</summary>
    private static readonly Dictionary<long, bool> succeededByActor = new();
    /// <summary>每波的固定劫心。</summary>
    private static readonly Dictionary<long, Vector2> strikeAnchors = new();
    /// <summary>正在落下的一记雷；进程内编号随世界重新建立。</summary>
    private static readonly Dictionary<long, PendingStrike> pendingStrikes = new();
    /// <summary>全局结算事件只订阅一次。</summary>
    private static bool hooksRegistered;

    /// <summary>保存真实落雷锚点与尚未被成长流程领取的成功证据。</summary>
    internal static WorldSaveState CaptureWorldSave() => new()
    {
        Succeeded = new Dictionary<long, bool>(succeededByActor), Anchors = new Dictionary<long, Vector2>(strikeAnchors)
    };

    /// <summary>读取不会重新开始渡劫，也不重复发放成功结果。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    internal static void RestoreWorldSave(WorldSaveState data)
    {
        ClearWorldState();
        foreach (var entry in data.Succeeded) succeededByActor.Add(entry.Key, entry.Value);
        foreach (var entry in data.Anchors) strikeAnchors.Add(entry.Key, entry.Value);
    }

    /// <summary>渡劫人物组件之外的真实世界记录。</summary>
    internal sealed class WorldSaveState
    {
        /// <summary>等待成长流程领取的成功证据。</summary>
        public Dictionary<long, bool> Succeeded = new();
        /// <summary>本波落雷的固定世界位置。</summary>
        public Dictionary<long, Vector2> Anchors = new();
    }

    /// <summary>注册死亡与清理钩子；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        if (hooksRegistered) return;
        hooksRegistered = true;
        ConstructedAbilityService.ContactConfirmed += OnStrikeContact;
        ConstructedAbilityService.CastEnded += OnStrikeEnded;
        ActorExtend.RegisterActionOnDeath(self =>
        {
            long actorId = self.Base.data.id;
            succeededByActor.Remove(actorId);
            strikeAnchors.Remove(actorId);
            CancelStrike(actorId);
        });
    }

    /// <summary>挑战阶段状态：没有劫程且准备完成时请求开始；劫程结束时给出成败。</summary>
    public static ProgressionGateResult Evaluate(ActorExtend actor)
    {
        if (actor.E.HasComponent<YaoTribulation>()) return ProgressionGateResult.InProgress("yao.tribulation_running");
        if (succeededByActor.Remove(actor.Base.data.id)) return ProgressionGateResult.Satisfied;
        return ProgressionGateResult.NeedsStart("yao.tribulation_ready");
    }

    /// <summary>开始一场天劫：消耗预留的凝丹资源，并挂上过程组件。</summary>
    public static bool TryStart(ActorExtend actor)
    {
        if (!CanStart(actor)) return false;
        ref Yao yao = ref actor.GetCultisys<Yao>();
        if (string.IsNullOrEmpty(yao.CorePreparationPatternId)) return false;

        YaoResourceService.Spend(actor, ref yao, yao.CorePreparationRequiredYaoPower);
        Begin(actor, TotalWaves, TribulationTimeout);
        YaoWorldLog.TribulationStarted(actor);
        return true;
    }

    /// <summary>两种天劫都只允许活着的妖兽开始一个未结束的劫程。</summary>
    /// <param name="actor">渡劫者。</param>
    internal static bool CanStart(ActorExtend actor) => actor?.Base != null && !actor.Base.isRekt() &&
        actor.Base.isAlive() && actor.Base.hasHealth() && actor.HasCultisys<Yao>() && !actor.E.HasComponent<YaoTribulation>();

    /// <summary>从两种实际成长入口登记原波数、时限和开始所在位置。</summary>
    /// <param name="actor">已经通过资格判断的妖兽。</param>
    /// <param name="waves">该劫需要完成的波数。</param>
    /// <param name="timeout">该劫的最长世界时间。</param>
    internal static void Begin(ActorExtend actor, int waves, float timeout)
    {
        float now = YaoTime.Now;
        actor.E.AddComponent(new YaoTribulation
        {
            TotalWaves = waves,
            CurrentWave = 1,
            StartedAt = now,
            ExpiresAt = now + timeout,
            NextStrikeAt = now + StrikeInterval,
            RequiredDamageEvidence = WaveEvidenceRequirement(actor, 1),
            CoreIntegrity = 1f,
        });
        strikeAnchors[actor.Base.data.id] = actor.Base.current_position;
    }

    /// <summary>清理世界时丢弃全部劫程锚点与成败记录。</summary>
    public static void ClearWorldState()
    {
        succeededByActor.Clear();
        strikeAnchors.Clear();
        pendingStrikes.Clear();
    }

    /// <summary>推进全部劫程；由每帧系统调用。</summary>
    /// <param name="actor">当前渡劫者。</param>
    /// <param name="tribulation">原劫程进度。</param>
    public static void Update(ActorExtend actor, ref YaoTribulation tribulation)
    {
        Actor actorBase = actor.Base;
        if (actorBase == null || actorBase.isRekt() || !actorBase.isAlive() || !actor.HasCultisys<Yao>()) return;
        float now = YaoTime.Now;

        if (now > tribulation.ExpiresAt)
        {
            // 超时退劫：大量准备进度损失，冷却后才能重试。
            EndTribulation(actor, TribulationOutcome.Retreating);
            return;
        }

        long actorId = actorBase.id;
        if (now < tribulation.NextStrikeAt || pendingStrikes.ContainsKey(actorId)) return;

        // 早期化形档可能没有波次锚点。首次使用当时位置补齐该波，读回仍保留其固定位置。
        if (!strikeAnchors.TryGetValue(actorId, out Vector2 anchor))
            strikeAnchors[actorId] = anchor = actorBase.current_position;
        float damage = StrikeDamage(actor);
        ElementComposition element = StrikeElement(actor);
        var target = new ActiveAbilityTarget(actorBase, anchor,
            runtimeData: new SkillCastRuntimeData { CorrelationId = actorId });
        var hit = CombatDamageEffects.PrepareDamage(null, damage, element, damageOrigin: DamageOrigin.Primary);
        long castId = ConstructedAbilityService.StartWorld(YaoTribulationConstruction.Strike(element), target, anchor, hit);
        if (castId != 0) pendingStrikes.Add(actorId, new PendingStrike(castId, damage, now + StrikeInterval));
        else tribulation.NextStrikeAt = now + StrikeInterval;
    }

    /// <summary>只向当前原劫程递交这记落雷已经结算的承伤结果。</summary>
    /// <param name="contact">共同空间规则确认的实际接触。</param>
    private static void OnStrikeContact(ConstructedContact contact)
    {
        if (contact.AbilityId != YaoTribulationConstruction.StrikeId || contact.Damage.SettledCount == 0 ||
            !TakeStrike(contact.TargetId, contact.CastId, out ActorExtend actor, out PendingStrike strike)) return;
        ref YaoTribulation tribulation = ref actor.E.GetComponent<YaoTribulation>();
        tribulation.NextStrikeAt = strike.NextAt;
        // 原阈值按亲自承接的劫力计算；境界、抗性和护盾仍独立决定实际扣血。
        tribulation.ReceivedDamageEvidence += strike.Evidence;
        actor.Base.addStatusEffect("burning", 2f, pColorEffect: false);

        if (tribulation.ReceivedDamageEvidence >= tribulation.RequiredDamageEvidence)
        {
            tribulation.CurrentWave++;
            if (tribulation.CurrentWave > tribulation.TotalWaves)
            {
                EndTribulation(actor, TribulationOutcome.Success);
                return;
            }

            tribulation.ReceivedDamageEvidence = 0f;
            tribulation.RequiredDamageEvidence = WaveEvidenceRequirement(actor, tribulation.CurrentWave);
            strikeAnchors[contact.TargetId] = actor.Base.current_position;
        }
        else if (!actor.E.HasComponent<YaoCore>())
        {
            // 原丹胚在劫雷确实接触、承伤证据仍不足时裂损。
            tribulation.CoreIntegrity -= 1f / tribulation.TotalWaves;
            if (tribulation.CoreIntegrity <= 0f)
                EndTribulation(actor, TribulationOutcome.Cracked);
        }
    }

    /// <summary>范围外的落雷正常收束，只安排下次落雷。</summary>
    /// <param name="ended">共同执行真正发出的结束记录。</param>
    private static void OnStrikeEnded(ConstructedCastEnded ended)
    {
        if (ended.AbilityId == YaoTribulationConstruction.StrikeId &&
            TakeStrike(ended.CorrelationId, ended.CastId, out ActorExtend actor, out PendingStrike strike))
            actor.E.GetComponent<YaoTribulation>().NextStrikeAt = strike.NextAt;
    }

    /// <summary>同一记雷只可兑现一次，并核对渡劫者尚在原流程。</summary>
    /// <param name="actorId">渡劫者编号。</param>
    /// <param name="castId">共同执行本次落雷的编号。</param>
    /// <param name="actor">仍在原流程的渡劫者。</param>
    /// <param name="strike">出手时冻结的劫力和下一次时间。</param>
    private static bool TakeStrike(long actorId, long castId, out ActorExtend actor, out PendingStrike strike)
    {
        actor = null;
        if (!pendingStrikes.TryGetValue(actorId, out strike) || strike.Cast != castId) return false;
        pendingStrikes.Remove(actorId);
        Actor current = World.world.units.get(actorId);
        if (current == null || !current.isAlive() || !current.TryGetExtend(out actor)) return false;
        return actor.HasCultisys<Yao>() && actor.E.HasComponent<YaoTribulation>();
    }

    /// <summary>退劫或死亡后撤销尚未兑现的落雷。</summary>
    /// <param name="actorId">结束原劫程的人物。</param>
    private static void CancelStrike(long actorId)
    {
        if (!pendingStrikes.TryGetValue(actorId, out PendingStrike strike)) return;
        pendingStrikes.Remove(actorId);
        ConstructedAbilityService.Cancel(strike.Cast);
    }

    /// <summary>保存尚待接触的落雷；正式档仍保留已经结算的进度和下次应开始的时间。</summary>
    /// <param name="cast">当前进程实际施放编号。</param>
    /// <param name="evidence">实际承伤后应计入的原单雷证据量。</param>
    /// <param name="nextAt">本次落雷结算后下一次可开始的世界时间。</param>
    private readonly struct PendingStrike(long cast, float evidence, float nextAt)
    {
        /// <summary>本记落雷的真实施放。</summary>
        internal readonly long Cast = cast;
        /// <summary>有效承接并完成伤害结算时兑现的劫力。</summary>
        internal readonly float Evidence = evidence;
        /// <summary>下次落雷的原计划时点。</summary>
        internal readonly float NextAt = nextAt;
    }

    /// <summary>使用原成长流程解释劫程的真实结束。</summary>
    private enum TribulationOutcome : byte
    {
        Success,
        Retreating,
        Cracked
    }

    /// <summary>结束原劫程并兑现原有奖励或退劫结果。</summary>
    /// <param name="actor">当前渡劫者。</param>
    /// <param name="outcome">实际结束原因。</param>
    private static void EndTribulation(ActorExtend actor, TribulationOutcome outcome)
    {
        actor.E.RemoveComponent<YaoTribulation>();
        strikeAnchors.Remove(actor.Base.data.id);
        CancelStrike(actor.Base.data.id);
        ref Yao yao = ref actor.GetCultisys<Yao>();

        switch (outcome)
        {
            case TribulationOutcome.Success:
                succeededByActor[actor.Base.data.id] = true;
                YaoWorldLog.TribulationSucceeded(actor);
                break;
            case TribulationOutcome.Retreating:
                YaoCoreService.CancelPreparation(actor, penalize: true);
                YaoWorldLog.TribulationRetreated(actor);
                break;
            case TribulationOutcome.Cracked:
                YaoCoreService.ApplyCrackedCore(actor, ref yao, 1);
                YaoWorldLog.TribulationCracked(actor);
                break;
        }
    }

    /// <summary>每波要求的承伤证据随波次递增。</summary>
    private static float WaveEvidenceRequirement(ActorExtend actor, int wave)
    {
        return actor.Base.getMaxHealth() * (0.25f + wave * 0.1f);
    }

    /// <summary>单次落雷伤害：按最大生命的固定比例，随波次递增。</summary>
    private static float StrikeDamage(ActorExtend actor)
    {
        return actor.Base.getMaxHealth() * (0.08f + 0.02f * actor.GetCultisys<Yao>().CurrLevel);
    }

    /// <summary>按妖丹方向的弱点元素决定劫伤元素。</summary>
    private static ElementComposition StrikeElement(ActorExtend actor)
    {
        string patternId = YaoCoreService.ResolveBloodlineId(actor) != null &&
                           actor.E.TryGetComponent(out YaoCore core)
            ? core.CorePatternId
            : actor.GetCultisys<Yao>().CorePreparationPatternId;
        YaoCorePatternAsset pattern = YaoCorePatterns.Get(patternId);
        string weakness = pattern?.WeakAgainstSemantics is { Length: > 0 }
            ? pattern.WeakAgainstSemantics[0]
            : "semantic.element.lightning";

        return weakness switch
        {
            "semantic.element.water" => new ElementComposition(water: 1f, normalize: true),
            "semantic.element.ice" => new ElementComposition(water: 0.6f, pos: 0.4f, normalize: true),
            "semantic.element.fire" => new ElementComposition(fire: 1f, normalize: true),
            "semantic.element.wind" => new ElementComposition(neg: 1f, normalize: true),
            _ => new ElementComposition(fire: 0.6f, entropy: 0.4f, normalize: true),
        };
    }
}

/// <summary>凝丹天劫的挑战阶段。</summary>
public sealed class YaoTribulationStage : IProgressionStage
{
    /// <summary>无副作用地读取劫程状态。</summary>
    public ProgressionGateResult Evaluate(ProgressionStageContext context)
    {
        return YaoTribulationService.Evaluate(context.Actor);
    }

    /// <summary>开始一场天劫。</summary>
    public void Start(ProgressionStageContext context)
    {
        YaoTribulationService.TryStart(context.Actor);
    }
}
