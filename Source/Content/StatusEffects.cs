using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Cultiway.Core.Components;
using strings;
using UnityEngine;
using Cultiway.Core;
using Friflo.Engine.ECS;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public class StatusEffects : ExtendLibrary<StatusEffectAsset, StatusEffects>
{
    public static StatusEffectAsset Enlighten { get; private set; }
    public static StatusEffectAsset Slow { get; private set; }
    public static StatusEffectAsset Poison { get; private set; }
    protected override void OnInit()
    {
        Enlighten = StatusEffectAsset.StartBuild(nameof(Enlighten))
            .SetDuration(60)
            .EnableParticle(new Color(1f, 0.85f, 0.35f), 1, 0.1f)
            .Build();
        Slow = StatusEffectAsset.StartBuild(nameof(Slow))
            .SetDuration(3f)
            .SetStats(new BaseStats
            {
                [S.multiplier_speed] = -1f
            })
            .EnableParticle(new Color(0.4f, 0.6f, 1f), 1, 0.1f)
            .Build();
        Poison = StatusEffectAsset.StartBuild(nameof(Poison))
            .SetDuration(5f)
            .EnableParticle(new Color(0.35f, 0.85f, 0.35f), 1, 0.1f)
            .EnableTick(1f, OnPoisonTick)
            .Build();
    }

    // 中毒状态定时掉血逻辑
    private static void OnPoisonTick(Entity statusEntity, float deltaTime)
    {
        if (!statusEntity.TryGetComponent(out StatusTickState tickState)) return;
        var damage = tickState.Value * deltaTime;
        if (damage <= 0f) return;

        foreach (var owner in statusEntity.GetIncomingLinks<StatusRelation>().Entities)
        {
            if (!owner.HasComponent<ActorBinder>()) continue;
            var actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor == null || !actor.isAlive()) continue;
            ref var element = ref tickState.Element;
            actor.GetExtend().GetHit(damage, ref element, tickState.Source);
        }
    }
}
