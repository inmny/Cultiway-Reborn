using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     妖丹天劫的唯一入口。劫云、落雷与承伤证据都在真实世界中结算；
///     只看真实发生的结果，不允许一次随机数代替整个天劫。
/// </summary>
public static class YaoTribulationService
{
    private const int TotalWaves = 5;
    private const float StrikeInterval = 3f;
    private const float StrikeRadius = 4f;
    private const float TribulationTimeout = 240f;

    private static readonly Dictionary<long, bool> succeededByActor = new();
    private static readonly Dictionary<long, Vector2> strikeAnchors = new();
    private static bool hooksRegistered;

    /// <summary>注册死亡与清理钩子；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        if (hooksRegistered) return;
        hooksRegistered = true;
        ActorExtend.RegisterActionOnDeath(self =>
        {
            succeededByActor.Remove(self.Base.data.id);
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
        if (actor?.Base == null || actor.Base.isRekt() || !actor.HasCultisys<Yao>()) return false;
        if (actor.E.HasComponent<YaoTribulation>()) return false;
        ref Yao yao = ref actor.GetCultisys<Yao>();
        if (string.IsNullOrEmpty(yao.CorePreparationPatternId)) return false;

        YaoResourceService.Spend(actor, ref yao, yao.CorePreparationRequiredYaoPower);

        actor.E.AddComponent(new YaoTribulation
        {
            TotalWaves = TotalWaves,
            CurrentWave = 1,
            StartedAt = YaoTime.Now,
            ExpiresAt = YaoTime.Now + TribulationTimeout,
            NextStrikeAt = YaoTime.Now + StrikeInterval,
            RequiredDamageEvidence = WaveEvidenceRequirement(actor, 1),
            CoreIntegrity = 1f,
        });
        strikeAnchors[actor.Base.data.id] = new Vector2(actor.Base.current_position.x, actor.Base.current_position.y);
        YaoWorldLog.TribulationStarted(actor);
        return true;
    }

    /// <summary>清理世界时丢弃全部劫程锚点与成败记录。</summary>
    public static void ClearWorldState()
    {
        succeededByActor.Clear();
        strikeAnchors.Clear();
    }

    /// <summary>推进全部劫程；由每帧系统调用。</summary>
    public static void Update(ActorExtend actor, ref YaoTribulation tribulation)
    {
        Actor actorBase = actor.Base;
        if (actorBase == null || actorBase.isRekt()) return;
        float now = YaoTime.Now;

        if (now > tribulation.ExpiresAt)
        {
            // 超时退劫：大量准备进度损失，冷却后才能重试。
            EndTribulation(actor, ref tribulation, TribulationOutcome.Retreating);
            return;
        }

        if (now < tribulation.NextStrikeAt) return;
        tribulation.NextStrikeAt = now + StrikeInterval;

        // 一波内按固定间隔落雷；劫伤按妖丹方向的弱点元素结算。
        // 落雷落在该波开始时的锚点上：妖兽真实移动躲避就能避开劫伤。
        float damage = StrikeDamage(actor);
        Vector2 anchor = strikeAnchors.TryGetValue(actor.Base.data.id, out Vector2 stored)
            ? stored
            : new Vector2(actorBase.current_position.x, actorBase.current_position.y);
        Combat.CombatDamageEffects.DealDamage(null, actorBase, damage, StrikeElement(actor),
            damageOrigin: DamageOrigin.Primary, attackType: AttackType.Other);

        // 只有真实命中妖兽的劫伤才计入承伤证据。
        float distanceSquared = (new Vector2(actorBase.current_position.x, actorBase.current_position.y) - anchor).sqrMagnitude;
        bool hit = distanceSquared <= StrikeRadius * StrikeRadius;
        if (hit)
        {
            tribulation.ReceivedDamageEvidence += damage;
            actor.Base.addStatusEffect("burning", 2f, pColorEffect: false);
        }

        // 波次推进：证据足够进入下一波，不足则损耗妖丹完整度。
        if (tribulation.ReceivedDamageEvidence >= tribulation.RequiredDamageEvidence)
        {
            tribulation.CurrentWave++;
            tribulation.ReceivedDamageEvidence = 0f;
            if (tribulation.CurrentWave > tribulation.TotalWaves)
            {
                EndTribulation(actor, ref tribulation, TribulationOutcome.Success);
                return;
            }

            tribulation.RequiredDamageEvidence = WaveEvidenceRequirement(actor, tribulation.CurrentWave);
            strikeAnchors[actor.Base.data.id] = new Vector2(actorBase.current_position.x, actorBase.current_position.y);
        }
        else if (hit && !actor.E.HasComponent<YaoCore>())
        {
            // 尚未凝丹的妖兽在劫中承伤不足会先裂开准备中的丹胚。
            tribulation.CoreIntegrity -= 1f / tribulation.TotalWaves;
            if (tribulation.CoreIntegrity <= 0f)
            {
                EndTribulation(actor, ref tribulation, TribulationOutcome.Cracked);
                return;
            }
        }

        actor.E.GetComponent<YaoTribulation>() = tribulation;
    }

    private enum TribulationOutcome : byte
    {
        Success,
        Retreating,
        Cracked
    }

    private static void EndTribulation(ActorExtend actor, ref YaoTribulation tribulation, TribulationOutcome outcome)
    {
        actor.E.RemoveComponent<YaoTribulation>();
        strikeAnchors.Remove(actor.Base.data.id);
        ref Yao yao = ref actor.GetCultisys<Yao>();

        switch (outcome)
        {
            case TribulationOutcome.Success:
                float evidenceRatio = Mathf.Clamp01(
                    tribulation.ReceivedDamageEvidence / Mathf.Max(1f, tribulation.RequiredDamageEvidence));
                float quality = Mathf.Clamp(
                    40f + yao.BodyStability * 0.3f + evidenceRatio * 20f + tribulation.CurrentWave * 2f,
                    10f, 100f);
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
