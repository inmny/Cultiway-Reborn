using System.Collections.Generic;
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
    private const float NirvanaDuration = 10f;
    private static bool initialized;

    /// <summary>登记后果处理器；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        CreatureConsequenceQueue.RegisterProcessor("yao.nirvana", EnterNirvana);
    }

    /// <summary>进入不可行动的涅槃体阶段：添加过程组件并让单位保持原地。</summary>
    private static void EnterNirvana(in CreatureConsequenceEntry entry)
    {
        ActorExtend actor = ResolveActor(entry.Owner);
        if (actor?.Base == null || actor.Base.isRekt()) return;
        if (actor.E.HasComponent<Nirvana>()) return;
        if (!actor.HasCultisys<Yao>()) return;

        float duration = entry.Value > 0f ? entry.Value : NirvanaDuration;
        actor.E.AddComponent(new Nirvana
        {
            StartedAt = YaoTime.Now,
            ExpiresAt = YaoTime.Now + duration,
            BodyIntegrity = 1f,
        });
        // 涅槃体不可行动：用眩晕状态让单位保持在原地。
        actor.Base.addStatusEffect("stunned", duration);
        YaoWorldLog.NirvanaStarted(actor);
    }

    /// <summary>推进全部涅槃过程；由低频系统调用。</summary>
    public static void Update(ActorExtend actor, ref Nirvana nirvana)
    {
        Actor actorBase = actor.Base;
        if (actorBase == null) return;
        float now = YaoTime.Now;

        // 涅槃期间保持不可行动。
        if (now < nirvana.ExpiresAt)
        {
            actor.Base.addStatusEffect("stunned", 1.5f);
        }

        // 涅槃体受到的持续伤害按时间与资源评估；这里用剩余生命比例近似完整度。
        nirvana.BodyIntegrity = Mathf.Clamp01(actorBase.getHealthRatio() + 0.35f);

        if (now < nirvana.ExpiresAt)
        {
            actor.E.GetComponent<Nirvana>() = nirvana;
            return;
        }

        actor.E.RemoveComponent<Nirvana>();
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
