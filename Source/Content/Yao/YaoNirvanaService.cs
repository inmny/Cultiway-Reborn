using System.Linq;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Combat;
using Friflo.Engine.ECS;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     涅槃体阶段的唯一入口。保命机会已经在伤害阶段当场消费；
///     这里只负责延后执行的涅槃体过程与原地重生。
/// </summary>
public static class YaoNirvanaService
{
    internal const string EffectId = "yao.nirvana";
    internal const float StillnessDuration = 1.5f;
    private const float NirvanaDuration = 10f;
    private static bool initialized;

    /// <summary>登记后果处理器；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        CreatureConsequenceQueue.RegisterProcessor(EffectId, EnterNirvana);
    }

    /// <summary>进入不可行动的涅槃体阶段：添加过程组件并让单位保持原地。</summary>
    private static void EnterNirvana(in CreatureConsequenceEntry entry)
    {
        ActorExtend actor = ResolveActor(entry.Owner);
        if (actor?.Base == null || actor.Base.isRekt() || !actor.Base.isAlive()) return;
        if (actor.E.HasComponent<Nirvana>()) return;
        if (!actor.HasCultisys<Yao>()) return;

        actor.E.AddComponent(CreatePhase(entry));
        HoldStill(actor.Base);
        YaoWorldLog.NirvanaStarted(actor);
    }

    /// <summary>已扣除机会的等待记录保存为同一个可继续的涅槃过程。</summary>
    internal static Nirvana? CapturePhase(ActorExtend actor)
    {
        if (actor.E.TryGetComponent(out Nirvana active)) return active;
        return CreatureConsequenceQueue.TryGetPending(actor.E, EffectId, out var pending)
            ? CreatePhase(pending) : null;
    }

    /// <summary>全部档案关系已接回后，为还在涅槃体的角色补上本过程的限制。</summary>
    internal static void RestoreStillness()
    {
        foreach (var entity in ModClass.I.W.Query<ActorBinder, Nirvana>().Entities.ToArray())
        {
            Actor actor = entity.GetComponent<ActorBinder>().Actor;
            if (actor == null || actor.isRekt() || !actor.isAlive() ||
                CombatStatusEffects.HasStatus(actor, StatusEffects.NirvanaStillness, actor)) continue;
            HoldStill(actor);
        }
    }

    private static Nirvana CreatePhase(in CreatureConsequenceEntry entry)
    {
        float now = YaoTime.Now;
        return new Nirvana
        {
            StartedAt = now,
            ExpiresAt = now + (entry.Value > 0f ? entry.Value : NirvanaDuration),
            BodyIntegrity = 1f
        };
    }

    /// <summary>推进全部涅槃过程；由低频系统调用。</summary>
    public static void Update(ActorExtend actor, ref Nirvana nirvana)
    {
        Actor actorBase = actor.Base;
        if (actorBase == null) return;
        float now = YaoTime.Now;

        // 涅槃体受到的持续伤害按时间与资源评估；这里用剩余生命比例近似完整度。
        nirvana.BodyIntegrity = Mathf.Clamp01(actorBase.getHealthRatio() + 0.35f);

        if (now < nirvana.ExpiresAt)
        {
            HoldStill(actorBase);
            actor.E.GetComponent<Nirvana>() = nirvana;
            return;
        }

        actor.E.RemoveComponent<Nirvana>();
        CombatStatusEffects.RemoveStatus(actorBase, StatusEffects.NirvanaStillness, actorBase);
        if (nirvana.BodyIntegrity >= 0.5f && YaoResourceService.TrySpend(actor, 10f))
        {
            // 原地恢复身体与能力。
            actorBase.restoreHealth(actorBase.getMaxHealth());
            YaoWorldLog.NirvanaReborn(actor);
        }
        else
        {
            // 资源或完整度不满足要求：真正死亡。
            actorBase.dieSimpleNone();
        }
    }

    private static void HoldStill(Actor actor) =>
        CombatStatusEffects.ApplyStatus(actor, StatusEffects.NirvanaStillness, StillnessDuration, actor);

    private static ActorExtend ResolveActor(Entity owner)
    {
        if (owner.IsNull || !owner.HasComponent<ActorBinder>()) return null;
        Actor actor = owner.GetComponent<ActorBinder>().Actor;
        return actor == null || actor.isRekt() ? null : actor.GetExtend();
    }

    /// <summary>清理世界时由后果队列统一清空，无需额外状态。</summary>
    public static void ClearWorldState()
    {
    }
}
