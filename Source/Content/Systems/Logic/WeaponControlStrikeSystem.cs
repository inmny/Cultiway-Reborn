using System;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Skills;
using Cultiway.Content.WeaponControl;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>一次已受理的近战御器，持有原件和该次动作的进度。</summary>
internal struct WeaponControlStrike : IComponent
{
    /// <summary>已从实际持有者借出的同一件兵器。</summary>
    internal WeaponControlFlight Flight;

    /// <summary>当前动作的参数、朝向和已推进时间。</summary>
    internal EquippedWeaponMotionState Motion;
}

/// <summary>推动原兵器的刃端并用共用扫掠确认实际攻击。</summary>
internal sealed class WeaponControlStrikeSystem : QuerySystem<WeaponControlStrike>
{
    /// <summary>供实机演示读取已建立的真实出击。</summary>
    internal static event Action<Entity> Started;

    /// <summary>退出查询后推进，实际攻击可以触发原版对象变化。</summary>
    private readonly List<Entity> strikes = new();

    /// <summary>复用人物、建筑及刃端的扫掠检查。</summary>
    private readonly WorldObjectContactSweep sweep = new();

    /// <summary>本次刃端扫过的对象。</summary>
    private readonly List<BaseSimObject> targets = new();

    /// <summary>完成本轮几何采集后再结算。</summary>
    private readonly List<(Entity Strike, BaseSimObject Target)> contacts = new();

    /// <summary>只推进尚未收回的动作。</summary>
    internal WeaponControlStrikeSystem() =>
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());

    /// <summary>从真实兵器的当前刃端启动一次出击。</summary>
    /// <param name="context">正式发射的来源、方向和归属。</param>
    /// <param name="flight">已经借出且可以出击的原兵器。</param>
    /// <param name="motion">本次器形对应的动作。</param>
    internal static void Begin(in SkillContext context, WeaponControlFlight flight, in EquippedWeaponMotionState motion)
    {
        flight.BeginStrike();
        Vector3 tip = flight.ContactPoint;
        Entity strike = ModClass.I.W.CreateEntity(context, new WeaponControlStrike { Flight = flight, Motion = motion },
            new Position { value = tip }, new PrevPosition { Value = tip, Height = tip.z },
            new ColliderSphere { Radius = flight.ContactRadius },
            new ColliderConfig { Actor = true, Building = true, Enemy = true },
            new EffectRadiusScale { Value = 1f }, SkillHitMemory.Create());
        Started?.Invoke(strike);
    }

    /// <summary>推进实际刃端，再按已确认的相交位置结算。</summary>
    protected override void OnUpdate()
    {
        if (ModClass.I.Game.IsPaused()) return;
        strikes.Clear();
        contacts.Clear();
        sweep.Begin();
        Query.ForEachEntity((ref WeaponControlStrike _, Entity strike) => strikes.Add(strike));
        foreach (Entity strike in strikes)
        {
            if (!Advance(strike, Tick.deltaTime)) continue;
            sweep.Collect(strike, targets);
            foreach (BaseSimObject target in targets) contacts.Add((strike, target));
        }

        foreach (var contact in contacts) Confirm(contact.Strike, contact.Target);
    }

    /// <summary>更新同一兵器的姿态，并返回本时段能否接触。</summary>
    /// <param name="strike">正在推进的出击。</param>
    /// <param name="seconds">本次推进的世界秒数。</param>
    private static bool Advance(Entity strike, float seconds)
    {
        ref WeaponControlStrike state = ref strike.GetComponent<WeaponControlStrike>();
        ref Position position = ref strike.GetComponent<Position>();
        ref ColliderConfig collider = ref strike.GetComponent<ColliderConfig>();
        if (state.Flight.IsCurrent && !strike.GetComponent<SkillContext>().SourceObj.a.isFlying())
        {
            strike.GetComponent<PrevPosition>() = new PrevPosition { Value = position.v2, Height = position.z };
            state.Motion.Elapsed += seconds;
            collider.Enabled = state.Flight.MoveStrike(in state.Motion,
                Mathf.Clamp01(state.Motion.Elapsed / state.Motion.Duration));
            position.value = state.Flight.ContactPoint;
            if (state.Motion.Elapsed < state.Motion.Duration) return collider.Enabled;
        }

        state.Flight.FinishStrike();
        collider.Enabled = false;
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(strike.Id);
        return false;
    }

    /// <summary>按原版实际攻击结算，保留格挡、装备作用和本次伤害归属。</summary>
    /// <param name="strike">刚刚扫到目标的实际出击。</param>
    /// <param name="target">已确认接触的人物或建筑。</param>
    private static void Confirm(Entity strike, BaseSimObject target)
    {
        if (strike.IsNull || target.isRekt()) return;
        SkillContext context = strike.GetComponent<SkillContext>();
        WeaponControlStrike state = strike.GetComponent<WeaponControlStrike>();
        if (!state.Flight.IsCurrent) return;
        Actor owner = context.SourceObj.a;
        if (!owner.canAttackTarget(target)) return;

        long targetKey = unchecked((target.getID() << 1) | (target.isActor() ? 0L : 1L));
        if (!strike.GetComponent<SkillHitMemory>().TargetIds.Add(targetKey)) return;
        Vector3 point = new(target.current_position.x, target.current_position.y, target.getHeight());
        var attack = new AttackData(owner, target.current_tile, point, owner.current_position, target,
            context.ResolveAttackKingdom() ?? owner.kingdom, AttackType.Weapon, owner.haveMetallicWeapon(), true);
        float previous = AttackDamageScaleContext.Enter(state.Motion.DamageMultiplier);
        try
        {
            using (DamageResolutionContext.Enter(DamageOrigin.Primary, context.RuntimeData.CorrelationId))
            {
                AttackDataResult result = MapBox.checkAttackFor(attack, target);
                if (result.state is ApplyAttackState.Block or ApplyAttackState.Deflect)
                    CombatImpactVisuals.OnDefended(target, owner);
            }
        }
        finally
        {
            AttackDamageScaleContext.Restore(previous);
        }

        SkillHitResolver.NotifyContact(strike, target);
    }
}
