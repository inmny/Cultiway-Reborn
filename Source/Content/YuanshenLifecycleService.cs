using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>化神肉身死亡、无身免伤和元神真死之间的唯一生命循环入口。</summary>
public static class YuanshenLifecycleService
{
    /// <summary>防止最终伤害规则重复注册。</summary>
    private static bool initialized;

    /// <summary>注册无身免伤和化神致死延续规则。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Avoidance, FilterBodilessDamage);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Survival, PreserveYuanshenOnBodyDeath);
    }

    /// <summary>判断原人物当前是否已经失去肉身。</summary>
    /// <param name="actor">需要检查的人物。</param>
    /// <returns>人物使用无身元神状态时返回真。</returns>
    public static bool IsBodiless(ActorExtend actor)
    {
        return actor != null && actor.HasComponent<BodylessYuanshenState>();
    }

    /// <summary>由命魂生命周期入口提交一次可归因的真正神魂死亡。</summary>
    /// <param name="actor">命魂已经不可恢复的人物。</param>
    /// <param name="attacker">导致命魂毁灭的攻击者；可为空。</param>
    public static void SubmitTrueSoulDeath(ActorExtend actor, BaseSimObject attacker)
    {
        if (actor == null || actor.Base == null || actor.Base.isRekt() || !actor.Base.isAlive() ||
            actor.HasComponent<YuanshenTrueDeathState>()) return;
        actor.GetOrAddComponent<YuanshenTrueDeathState>() = new YuanshenTrueDeathState
        {
            submitted_at = World.world?.getCurWorldTime() ?? 0d,
            attacker_actor_id = attacker != null && attacker.isActor() && !attacker.isRekt()
                ? attacker.a.data.id
                : 0L
        };
        Actor target = actor.Base;
        target.cancelAllBeh();
        target.clearAttackTarget();
        float lethalDamage = Mathf.Max(target.data.health, target.getMaxHealth()) + 1f;
        PatchActor.getHit_snapshot(
            target,
            lethalDamage,
            pFlash: false,
            pAttackType: AttackType.Other,
            pAttacker: attacker,
            pSkipIfShake: false,
            pMetallicWeapon: false,
            pCheckDamageReduction: false);
        if (target.isAlive())
        {
            target.setHealth(0);
            target.checkDeath();
        }
        if (target.isAlive()) target.dieAndDestroy(AttackType.Other);
    }

    /// <summary>临时命魂人物毁灭时尝试消耗备用法器，把唯一身份转为无身元神。</summary>
    /// <param name="actor">命魂所属人物。</param>
    /// <param name="soulCarrier">完整度刚刚归零的临时命魂人物。</param>
    /// <returns>备用锚点有效且完成唯一转移时返回真。</returns>
    public static bool TryRescueBrokenSoulCarrier(ActorExtend actor, Actor soulCarrier)
    {
        if (actor == null || soulCarrier == null ||
            !YuanshenTravelService.TryGetSoulCarrier(actor, out Actor current) || current != soulCarrier ||
            !YuanshenArtifactAnchorService.TryConsumeFatalRescue(actor, out Vector3 anchorPosition)) return false;
        if (!YuanshenTravelService.CompleteReturn(actor, soulCarrier)) return false;
        if (!TryBecomeBodiless(actor, anchorPosition, false)) return false;
        YuanshenTravelService.LockMainMindShare(actor, 10f);
        return true;
    }

    /// <summary>无身元神只接收明确阴性攻击，毒、疾病、肉体和环境伤害均无效。</summary>
    /// <param name="actor">受击人物。</param>
    /// <param name="attacker">伤害来源。</param>
    /// <param name="composition">伤害元素构成。</param>
    /// <param name="attackType">原版攻击类别。</param>
    /// <param name="damage">可修改的最终伤害。</param>
    private static void FilterBodilessDamage(
        ActorExtend actor,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        if (!IsBodiless(actor)) return;
        if (!YuanshenTravelService.CanDamageSoul(composition, attackType))
        {
            damage = 0f;
            return;
        }
        if (actor.HasComponent<YuanshenTrueDeathState>() ||
            Mathf.Floor(damage) < actor.Base.data.health) return;
        if (YuanshenArtifactAnchorService.TryConsumeFatalRescue(actor, out Vector3 anchorPosition))
        {
            if (actor.HasComponent<YuanshenBodilessTransitState>())
                actor.E.RemoveComponent<YuanshenBodilessTransitState>();
            YuanshenTravelService.LockMainMindShare(actor, 25f);
            MoveActorTo(actor.Base, anchorPosition);
            RestoreBodilessHealth(actor);
            damage = 0f;
            return;
        }
        damage = 0f;
        SubmitTrueSoulDeath(actor, attacker);
    }

    /// <summary>肉身受致死伤害时由仍存活的元神延续原人物。</summary>
    /// <param name="actor">受击人物。</param>
    /// <param name="attacker">伤害来源。</param>
    /// <param name="composition">伤害元素构成。</param>
    /// <param name="attackType">原版攻击类别。</param>
    /// <param name="damage">可修改的最终伤害。</param>
    private static void PreserveYuanshenOnBodyDeath(
        ActorExtend actor,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        Actor body = actor?.Base;
        if (body == null || body.data == null || body.current_tile == null || body.isRekt() ||
            IsBodiless(actor) || actor.HasComponent<YuanshenTrueDeathState>() ||
            Mathf.Floor(damage) < body.data.health ||
            !HasLivingYuanshen(actor))
            return;

        if (!TryBecomeBodiless(actor, null, true)) return;
        damage = 0f;
    }

    /// <summary>把原人物的物质肉身原子切换为唯一无身元神形态。</summary>
    /// <param name="actor">保留身份的人物。</param>
    /// <param name="forcedPosition">需要转移到的备用锚点；为空时留在命魂位置。</param>
    /// <param name="mergeSoulCarrier">转换前是否先归一当前临时命魂人物。</param>
    /// <returns>无身资产和亚种均有效并完成转换时返回真。</returns>
    private static bool TryBecomeBodiless(
        ActorExtend actor,
        Vector3? forcedPosition,
        bool mergeSoulCarrier)
    {
        Actor body = actor?.Base;
        if (body == null || body.isRekt() || Actors.BodilessYuanshen == null ||
            World.world?.subspecies == null) return false;
        WorldTile targetTile = forcedPosition.HasValue
            ? World.world.GetTileSimple(
                Mathf.RoundToInt(forcedPosition.Value.x),
                Mathf.RoundToInt(forcedPosition.Value.y))
            : body.current_tile;
        if (targetTile == null) return false;
        Subspecies soulSubspecies = World.world.subspecies.getNearbySpecies(
            Actors.BodilessYuanshen, targetTile, out _, false)
            ?? World.world.subspecies.newSpecies(Actors.BodilessYuanshen, targetTile);
        if (soulSubspecies == null || soulSubspecies.isRekt()) return false;

        try
        {
            if (mergeSoulCarrier) MoveIdentityToSoulCarrier(actor);
            body.cancelAllBeh();
            body.beh_actor_target = null;
            body.clearAttackTarget();
            body.clearTileTarget();
            PhysicalBodyService.ReleaseAndStrip(body);
            actor.GetOrAddComponent<BodylessYuanshenState>() = new BodylessYuanshenState
            {
                body_lost_at = World.world.getCurWorldTime()
            };
            if (actor.HasComponent<YuanyingSeed>()) actor.E.RemoveComponent<YuanyingSeed>();
            if (actor.HasComponent<YuanyingSoulState>()) actor.E.RemoveComponent<YuanyingSoulState>();
            body.setAsset(Actors.BodilessYuanshen);
            body.setSubspecies(soulSubspecies);
            body.data.head = -1;
            body.setFlying(true);
            body.setShowShadow(Actors.BodilessYuanshen.shadow);
            body.clearGraphicsFully();
            if (forcedPosition.HasValue) MoveActorTo(body, forcedPosition.Value);
            actor.MarkCultiwayStatsDirty(false);
            CoreFormationEffectResolver.Synchronize(actor);
            body.setStatsDirty();
            body.updateStats();
            RestoreBodilessHealth(actor);
            body.city?.setCitizensDirty();
            return true;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"元神延续失败: actor={body.data.id}\n{exception}");
            return false;
        }
    }

    /// <summary>把人物移动到已经校验的备用锚点地块。</summary>
    /// <param name="actor">需要移动的人物。</param>
    /// <param name="position">备用锚点坐标。</param>
    private static void MoveActorTo(Actor actor, Vector3 position)
    {
        if (actor == null || actor.isRekt()) return;
        WorldTile tile = World.world?.GetTileSimple(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        if (tile == null) return;
        actor.cancelAllBeh();
        actor.clearAttackTarget();
        actor.clearTileTarget();
        actor.spawnOn(tile, 0f);
    }

    /// <summary>按当前未锁定心神比例恢复无身元神的显示生命。</summary>
    /// <param name="actor">无身元神人物。</param>
    private static void RestoreBodilessHealth(ActorExtend actor)
    {
        if (actor?.Base == null || actor.Base.isRekt()) return;
        YuanshenTravelService.EnsureMindLedger(actor);
        float ratio = actor.TryGetComponent(out YuanshenRuntimeState runtime)
            ? Mathf.Clamp(runtime.AvailableShare / 100f, 0.05f, 1f)
            : 1f;
        actor.Base.setHealth(Mathf.Max(1, Mathf.RoundToInt(actor.Base.getMaxHealth() * ratio)));
    }

    /// <summary>检查人物当前是否持有化神境界与有效元神成果。</summary>
    /// <param name="actor">需要检查的人物。</param>
    /// <returns>满足全部生命延续条件时返回真。</returns>
    private static bool HasLivingYuanshen(ActorExtend actor)
    {
        return actor.HasCultisys<Xian>() && actor.GetCultisys<Xian>().CurrLevel >= Const.XianLevels.Huashen &&
               actor.TryGetComponent(out Yuanshen yuanshen) && yuanshen.formation.IsValid &&
               yuanshen.formation.realm == CoreFormationRealm.Yuanshen;
    }

    /// <summary>命魂在外时把原人物移动到临时命魂人物的位置，并销毁代理。</summary>
    /// <param name="actor">需要延续身份的人物。</param>
    private static void MoveIdentityToSoulCarrier(ActorExtend actor)
    {
        if (!YuanshenTravelService.TryGetSoulCarrier(actor, out Actor carrier)) return;
        Vector3 position = carrier.GetSimPos();
        WorldTile tile = World.world.GetTileSimple(
            Mathf.RoundToInt(position.x),
            Mathf.RoundToInt(position.y));
        YuanshenTravelService.CompleteReturn(actor, carrier);
        if (tile != null) actor.Base.spawnOn(tile, 0f);
    }
}
