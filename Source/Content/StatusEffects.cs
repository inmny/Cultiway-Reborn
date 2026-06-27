using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
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
    public static StatusEffectAsset Burn { get; private set; }
    public static StatusEffectAsset Poison { get; private set; }
    public static StatusEffectAsset Freeze { get; private set; }
    public static StatusEffectAsset Weaken { get; private set; }
    public static StatusEffectAsset ArmorBreak { get; private set; }
    public static StatusEffectAsset Daze { get; private set; }
    public static StatusEffectAsset Silence { get; private set; }
    public static StatusEffectAsset DeathSentence { get; private set; }
    public static StatusEffectAsset EternalCurse { get; private set; }
    private const float BurnTickInterval = 1f;
    private const float PoisonTickInterval = 1f;
    private const float CurseTickInterval = 1f;
    protected override bool AutoRegisterAssets() => false;
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
        Burn = StatusEffectAsset.StartBuild(nameof(Burn))
            .SetDuration(4f)
            .EnableParticle(new Color(1f, 0.4f, 0.1f), 1, 0.1f)
            .EnableTick(BurnTickInterval, OnBurnTick)
            .Build();
        Poison = StatusEffectAsset.StartBuild(nameof(Poison))
            .SetDuration(5f)
            .EnableParticle(new Color(0.35f, 0.85f, 0.35f), 1, 0.1f)
            .EnableTick(PoisonTickInterval, OnPoisonTick)
            .Build();
        Freeze = StatusEffectAsset.StartBuild(nameof(Freeze))
            .SetDuration(3f)
            .SetStats(CreateFreezeStats())
            .EnableParticle(new Color(0.5f, 0.8f, 1f), 1, 0.1f)
            .Build();
        Weaken = StatusEffectAsset.StartBuild(nameof(Weaken))
            .SetDuration(6f)
            .SetStats(new BaseStats
            {
                [S.damage] = -0.2f
            })
            .EnableParticle(new Color(0.55f, 0.55f, 0.6f), 1, 0.1f)
            .Build();
        ArmorBreak = StatusEffectAsset.StartBuild(nameof(ArmorBreak))
            .SetDuration(4f)
            .SetStats(new BaseStats
            {
                [S.armor] = -0.2f
            })
            .EnableParticle(new Color(1f, 0.75f, 0.25f), 1, 0.1f)
            .Build();
        Daze = StatusEffectAsset.StartBuild(nameof(Daze))
            .SetDuration(1f)
            .SetStats(CreateFreezeStats())
            .EnableParticle(new Color(0.95f, 0.95f, 0.45f), 1, 0.1f)
            .Build();
        Silence = StatusEffectAsset.StartBuild(nameof(Silence))
            .SetDuration(4f)
            .EnableParticle(new Color(0.55f, 0.35f, 0.85f), 1, 0.12f)
            .Build();
        DeathSentence = StatusEffectAsset.StartBuild(nameof(DeathSentence))
            .SetDuration(3f)
            .EnableParticle(new Color(0.45f, 0.05f, 0.05f), 1, 0.08f)
            .Build();
        EternalCurse = StatusEffectAsset.StartBuild(nameof(EternalCurse))
            .SetDuration(8f)
            .EnableParticle(new Color(0.25f, 0.05f, 0.45f), 1, 0.12f)
            .EnableTick(CurseTickInterval, OnCurseTick)
            .Build();
    }

    // 创建冰冻状态的BaseStats，包含三个tag
    private static BaseStats CreateFreezeStats()
    {
        var stats = new BaseStats();
        stats.addTag("frozen_ai");
        stats.addTag("immovable");
        stats.addTag("stop_idle_animation");
        return stats;
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
            ref var statusComp = ref statusEntity.GetComponent<StatusComponent>();
            EventSystemHub.Publish(new GetHitEvent()
            {
                TargetID = actor.data.id,
                Damage = damage,
                Element = tickState.Element,
                Attacker = statusComp.Source,
                AttackerPowerLevel = statusComp.SourcePowerLevel
            });
        }
    }

    // 灼烧状态定时掉血逻辑
    private static void OnBurnTick(Entity statusEntity, float deltaTime)
    {
        if (!statusEntity.TryGetComponent(out StatusTickState tickState)) return;
        var damage = tickState.Value * deltaTime;
        if (damage <= 0f) return;

        foreach (var owner in statusEntity.GetIncomingLinks<StatusRelation>().Entities)
        {
            if (!owner.HasComponent<ActorBinder>()) continue;
            var actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor == null || !actor.isAlive()) continue;
            ref var statusComp = ref statusEntity.GetComponent<StatusComponent>();
            EventSystemHub.Publish(new GetHitEvent()
            {
                TargetID = actor.data.id,
                Damage = damage,
                Element = tickState.Element,
                Attacker = statusComp.Source,
                AttackerPowerLevel = statusComp.SourcePowerLevel
            });
        }
    }

    // 诅咒状态定时掉血逻辑
    private static void OnCurseTick(Entity statusEntity, float deltaTime)
    {
        if (!statusEntity.TryGetComponent(out StatusTickState tickState)) return;
        var damage = tickState.Value * deltaTime;
        if (damage <= 0f) return;

        foreach (var owner in statusEntity.GetIncomingLinks<StatusRelation>().Entities)
        {
            if (!owner.HasComponent<ActorBinder>()) continue;
            var actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor == null || !actor.isAlive()) continue;
            ref var statusComp = ref statusEntity.GetComponent<StatusComponent>();
            EventSystemHub.Publish(new GetHitEvent()
            {
                TargetID = actor.data.id,
                Damage = damage,
                Element = tickState.Element,
                Attacker = statusComp.Source,
                AttackerPowerLevel = statusComp.SourcePowerLevel
            });
        }
    }
}
