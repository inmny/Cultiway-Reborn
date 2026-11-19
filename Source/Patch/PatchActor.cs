using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Content;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchActor
{
    private static readonly List<Func<Actor, bool>> HideHandItemPredicates = new();

    /// <summary>原版在合并模组属性前缓存了停顿动画标记，合并后同步同一字段。</summary>
    private static readonly AccessTools.FieldRef<Actor, bool> StopIdleAnimation =
        AccessTools.FieldRefAccess<Actor, bool>("_has_stop_idle_animation");

    /// <summary>注册一个可按角色运行状态临时隐藏手持物品的判定。</summary>
    public static void RegisterHideHandItemPredicate(Func<Actor, bool> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        HideHandItemPredicates.Add(predicate);
    }

    /// <summary>在本类型全部 Harmony 补丁安装后验证战斗接管入口。</summary>
    public static void SpecialPatch()
    {
        CombatWorldService.ValidateCriticalPatches();
    }

    /// <summary>把各运行时系统提供的长期上下文与即时动作合并为角色真实任务文本。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.getTaskText))]
    private static void getTaskText_postfix(Actor __instance, ref string __result)
    {
        if (World.world == null ||
            !ActorActivityPresentationRegistry.TryResolve(
                __instance,
                out ActorActivityPresentation presentation)) return;

        using var localizedParts = new ListPool<string>();
        for (var i = 0; i < presentation.LocaleKeys.Count; i++)
        {
            string localeKey = presentation.LocaleKeys[i];
            if (!string.IsNullOrEmpty(localeKey)) localizedParts.Add(localeKey.Localize());
        }

        if (localizedParts.Count == 0) return;
        string activityText = string.Join(" · ", localizedParts);
        string activityTime = Date.formatSeconds(
            World.world.getWorldTimeElapsedSince(presentation.StartedAt));
        __result = activityText + " " +
                   activityTime.ColorHex(ColorStyleLibrary.m.color_text_grey_dark);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.addChildren))]
    private static void addChildren_postfix(Actor __instance)
    {
        __instance.GetExtend().OnAddChildren();
    }

    /// <summary>
    /// 实现<see cref="ActorAssetExtend.hide_hand_item"/>
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.checkHasRenderedItem))]
    private static void getHandRendererAsset_postfix(Actor __instance, ref bool __result)
    {
        if (__result == false) return;
        if (__instance.asset.GetExtend<ActorAssetExtend>().hide_hand_item)
        {
            __result = false;
            return;
        }

        for (var i = 0; i < HideHandItemPredicates.Count; i++)
        {
            if (!HideHandItemPredicates[i](__instance)) continue;
            __result = false;
            return;
        }
    }

    /// <summary>
    /// 实现<see cref="ActorAssetExtend.sleep_standing_up"/>
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.isLying))]
    private static void isLying_postfix(Actor __instance, ref bool __result)
    {
        if (!__result) return;
        if (!__instance._has_status_sleeping) return;
        __result = !__instance.getActorAsset().GetExtend<ActorAssetExtend>().sleep_standing_up;
    }

    /// <summary>让 Core 控制标签同时约束原版法术入口。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.canUseSpells))]
    private static void canUseSpells_postfix(Actor __instance, ref bool __result)
    {
        if (__result && __instance.stats.hasTag(ActorControlTags.Silenced)) __result = false;
    }

    /// <summary>让隐匿标签参与原版目标获取和 Mod 共用的敌对目标判定。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(BaseSimObject), nameof(BaseSimObject.canAttackTarget))]
    private static void canAttackTarget_postfix(BaseSimObject pTarget, ref bool __result)
    {
        if (__result && pTarget.isActor() && pTarget.a.stats.hasTag(ActorControlTags.Concealed))
        {
            __result = false;
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.tryToAttack))]
    private static bool tryToAttack_prefix(Actor __instance, BaseSimObject pTarget, bool pDoChecks, Action pKillAction,
        float pBonusAreOfEffect, ref bool __result)
    {
        if (pTarget == null) return true;
        if (pTarget.isRekt()) return true;
        __result = __instance.GetExtend().TryToAttack(pTarget, pKillAction, pBonusAreOfEffect, pDoChecks);
        return false;
    }

    /// <summary>
    /// 用具体动作冷却替代原版所有高级战斗动作共享的 recovery_combat_action。
    /// 原版调用点仍负责立即调用选中动作的委托。
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.tryToUseAdvancedCombatAction))]
    private static bool tryToUseAdvancedCombatAction_prefix(
        Actor __instance,
        List<CombatActionAsset> pCombatActionAssetsCategory,
        BaseSimObject pAttackTarget,
        ref CombatActionAsset pResultCombatAsset,
        ref bool __result)
    {
        if (!TacticalCombatSettings.Enabled) return true;
        __result = CombatImmediateActionService.TrySelect(
            __instance,
            pCombatActionAssetsCategory,
            pAttackTarget,
            out pResultCombatAsset);
        return false;
    }

    /// <summary>
    /// 新战斗层启用时完全替换原版当前目标检查，只推进已经提交的动作计划。
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.b2_checkCurrentEnemyTarget))]
    private static bool b2_checkCurrentEnemyTarget_prefix(Actor __instance, float pElapsed)
    {
        return !CombatWorldService.TryUpdateEnemyTarget(__instance, pElapsed);
    }

    /// <summary>
    /// 将原版“是否在攻击范围内”的出手判定替换为 Mod 的综合战斗动作距离判定。
    /// </summary>
    [Hotfixable]
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), "checkCurrentEnemyTarget")]
    private static IEnumerable<CodeInstruction> checkCurrentEnemyTarget_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        var method = AccessTools.Method(typeof(Actor), nameof(Actor.isInAttackRange));
        var replacement = AccessTools.Method(typeof(PatchActor), nameof(isInCombatActionRange));
        var replaced = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Calls(method))
            {
                list[i].opcode = OpCodes.Call;
                list[i].operand = replacement;
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            ModClass.LogError("Failed to patch Actor.checkCurrentEnemyTarget combat range check");
        }

        return list;
    }

    /// <summary>
    /// 将原版不可达目标检查替换为“是否仍值得追击”的判定，避免可施法目标被提前清除。
    /// </summary>
    [Hotfixable]
    [HarmonyTranspiler,
     HarmonyPatch(typeof(ai.behaviours.BehFightCheckEnemyIsOk), nameof(ai.behaviours.BehFightCheckEnemyIsOk.execute))]
    private static IEnumerable<CodeInstruction> BehFightCheckEnemyIsOk_execute_transpiler(
        IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        var method = AccessTools.Method(typeof(Actor), nameof(Actor.isInAttackRange));
        var replacement = AccessTools.Method(typeof(PatchActor), nameof(canKeepCombatTarget));
        var replaced = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Calls(method))
            {
                list[i].opcode = OpCodes.Call;
                list[i].operand = replacement;
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            ModClass.LogError("Failed to patch BehFightCheckEnemyIsOk combat target check");
        }

        return list;
    }

    /// <summary>
    /// Harmony 替换用桥接函数：判断目标当前是否已进入任意战斗动作的出手距离。
    /// </summary>
    private static bool isInCombatActionRange(Actor actor, BaseSimObject target)
    {
        return actor.GetExtend().CanUseCombatActionAtCurrentDistance(target);
    }

    /// <summary>
    /// Harmony 替换用桥接函数：判断目标是否应继续作为战斗目标保留。
    /// </summary>
    private static bool canKeepCombatTarget(Actor actor, BaseSimObject target)
    {
        return actor.GetExtend().CanKeepCombatTarget(target);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.b3_findEnemyTarget))]
    private static bool b3_findEnemyTarget_prefix(Actor __instance, out bool __state)
    {
        if (CombatWorldService.TryUpdateEnemySearch(__instance))
        {
            __state = false;
            return false;
        }

        __state = ShouldBackoffEmptyEnemySearch(__instance);
        return true;
    }

    /// <summary>
    /// 战术任务已经接管角色时只屏蔽原版效用决策，保留后续 AI、路径与平滑移动更新。
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.b6_0_updateDecision))]
    private static bool b6_0_updateDecision_prefix(Actor __instance)
    {
        return !CombatWorldService.ShouldTakeOver(__instance) ||
               !CombatWorldService.IsEngaged(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.b3_findEnemyTarget))]
    private static void b3_findEnemyTarget_postfix(Actor __instance, bool __state)
    {
        if (!__state) return;
        if (__instance.has_attack_target &&
            !YuanshenTravelService.CanTargetSoulCarrier(__instance, __instance.attack_target))
        {
            BaseSimObject rejected = __instance.attack_target;
            __instance.clearAttackTarget();
            if (rejected != null && !rejected.isRekt()) __instance.ignoreTarget(rejected);
        }

        ApplyEnemySearchBackoff(__instance);
    }

    internal static bool ShouldBackoffEmptyEnemySearch(Actor actor)
    {
        if (actor == null) return false;
        if (actor.has_attack_target) return false;
        if (actor._timeout_targets > 0f) return false;
        if (actor.is_moving || actor.isUsingPath()) return false;
        if (!actor.isAllowedToLookForEnemies()) return false;
        if (actor.isInWaterAndCantAttack()) return false;
        return true;
    }

    internal static void ApplyEnemySearchBackoff(Actor actor)
    {
        if (actor == null || actor.has_attack_target) return;
        if (actor._timeout_targets <= 0f) return;

        var timeScale = Config.time_scale_asset?.multiplier ?? 1f;
        var scale = Mathf.Clamp(timeScale * 0.25f, 1f, 5f);
        if (scale <= 1f) return;

        actor._timeout_targets *= scale;
    }

    /// <summary>复用同一份原版受击方法，不启用二次减伤。</summary>
    /// <param name="actor">实际受击人物。</param><param name="damage">最终伤害。</param>
    /// <param name="pFlash">是否闪红。</param><param name="pAttackType">原版伤害种类。</param><param name="pAttacker">实际归属。</param>
    internal static int MeasureHit(Actor actor, float damage, bool pFlash, AttackType pAttackType, BaseSimObject pAttacker)
    {
        int lost = 0;
        GetHitWithMeasurement(actor, damage, pFlash, pAttackType, pAttacker, false, false, false, ref lost);
        return lost;
    }

    /// <summary>其他原版伤害入口仍复用原副本，调用方不需要结果时不保留记录。</summary>
    public static void getHit_snapshot(Actor __instance, float pDamage, bool pFlash = true,
        AttackType pAttackType = AttackType.Other, BaseSimObject pAttacker = null,
        bool pSkipIfShake = true, bool pMetallicWeapon = false, bool pCheckDamageReduction = true)
    {
        int ignored = 0;
        GetHitWithMeasurement(__instance, pDamage, pFlash, pAttackType, pAttacker, pSkipIfShake,
            pMetallicWeapon, pCheckDamageReduction, ref ignored);
    }

    /// <summary>原版受击的副本，只替换其中真正扣减生命的那一次调用。</summary>
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot), HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    private static void GetHitWithMeasurement(Actor actor, float damage, bool flash, AttackType attackType,
        BaseSimObject attacker, bool skipIfShake, bool metallicWeapon, bool checkDamageReduction, ref int lost)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var change = AccessTools.Method(typeof(BaseSimObject), nameof(BaseSimObject.changeHealth));
            var measure = AccessTools.Method(typeof(PatchActor), nameof(MeasureHealthChange));
            var originalSound = AccessTools.Method(typeof(MusicBox), nameof(MusicBox.playSound),
                new[] { typeof(string), typeof(WorldTile), typeof(bool), typeof(bool) });
            var hitSound = AccessTools.Method(typeof(PatchActor), nameof(PlayNativeHitSound));
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(originalSound))
                {
                    instruction.operand = hitSound;
                    yield return instruction;
                    continue;
                }
                if (!instruction.Calls(change)) { yield return instruction; continue; }
                // 原版的八个参数占 0 到 7；第九个是本副本追加的本次扣血输出。
                yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)8).MoveLabelsFrom(instruction);
                yield return new CodeInstruction(OpCodes.Call, measure).MoveBlocksFrom(instruction);
            }
        }

        _ = (Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>)Transpiler;
        throw new NotImplementedException();
    }

    /// <summary>在原版生命变化处读实际扣除量，后面的受击回调不能改写这个事实。</summary>
    /// <param name="target">当前受击对象。</param><param name="amount">原版取整后的变化。</param>
    /// <param name="lost">本次实际扣除。</param>
    private static void MeasureHealthChange(BaseSimObject target, int amount, ref int lost)
    {
        int before = target.getHealth();
        target.changeHealth(amount);
        lost = Math.Max(0, before - target.getHealth());
    }

    [Hotfixable]
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    public static IEnumerable<CodeInstruction> getHit_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        for (var i = 0; i < list.Count - 1; i++)
        {
            CodeInstruction ldc = list[i];
            CodeInstruction stfld = list[i + 1];
            if (ldc.opcode == OpCodes.Ldc_R4 && stfld.opcode == OpCodes.Stfld &&
                (stfld.operand as MemberInfo)?.Name == nameof(Actor.timer_action))
            {
                ldc.operand = 0.0f;
                break;
            }
        }

        return list;
    }

    /// <summary>战技和御器由扣血结果发声，原版攻击保持自己的声音。</summary>
    private static void PlayNativeHitSound(string sound, WorldTile tile, bool gameplay, bool visible)
    {
        if (DamageResolutionContext.CurrentSourceScopeId == 0)
            MusicBox.playSound(sound, tile, gameplay, visible);
    }

    [Hotfixable]
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    private static bool getHit_prefix(Actor __instance, float pDamage, bool pFlash = true,
        AttackType pAttackType = AttackType.Other, BaseSimObject pAttacker = null,
        bool pSkipIfShake = true, bool pMetallicWeapon = false, bool pCheckDamageReduction = false)
    {
        if (__instance == pAttacker) return false;
        if (pSkipIfShake && __instance._shake_active)
        {
            return true;
        }

        pDamage = AttackDamageScaleContext.Apply(pDamage);
        var element = EnumUtils.DamageCompositionFromDamageType(pAttackType);
        long sourceScopeId = DamageResolutionContext.CurrentSourceScopeId;
        bool controlledWeapon = sourceScopeId != 0 && pAttackType == AttackType.Weapon;
        if (sourceScopeId == 0) sourceScopeId = PatchLightning.CurrentSkyLightningScopeId;
        var evt = new GetHitEvent
        {
            TargetID = __instance.data.id,
            Damage = pDamage,
            Element = element,
            AttackType = pAttackType,
            MetallicWeapon = pAttackType == AttackType.Weapon ? pMetallicWeapon : null,
            DamageOrigin = DamageResolutionContext.CurrentOrigin,
            SourceScopeId = sourceScopeId,
            ContactSound = !controlledWeapon ? null : pMetallicWeapon ? CombatContactFeedback.Metal :
                pAttacker?.a?.getWeaponAsset()?.material == "wood" ? CombatContactFeedback.Wood : CombatContactFeedback.Physical,
            IgnoreDamageReduction = !pCheckDamageReduction
        };
        evt.BindAttacker(pAttacker);
        evt.Observation = DamageResolutionContext.ClaimObservation(evt.AttackerID, evt.TargetID);
        if (DamageResolutionContext.TryClaimImmediateHit(evt.TargetID) && !EventSystemHub.IsPaused) GetHitEventSystem.Resolve(evt);
        else if (evt.Observation == null) EventSystemHub.Publish(evt);
        else evt.Observation.Pending = EventSystemHub.TryPublish(evt);
        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.newCreature))]
    private static IEnumerable<CodeInstruction> newCreature_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var idx = list.FindIndex(x => x.opcode == OpCodes.Call &&
                                      (x.operand as MethodBase)?.Name == nameof(Actor.generatePersonality));
        list.InsertRange(idx + 1, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_new_creature)))
        ]);
        return list;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.createBabyActorFromData))]
    private static IEnumerable<CodeInstruction> spawnPopPoint_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var idx = list.FindIndex(x =>
            x.opcode == OpCodes.Call && (x.operand as MethodBase)?.Name == nameof(ActorManager.finalizeActor)) + 1;
        list.InsertRange(idx, [
            new(OpCodes.Ldloc_1),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_new_creature)))
        ]);

        return list;
    }

    private static void _extend_new_creature(Actor actor)
    {
        actor.GetExtend().ExtendNewCreature();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateStats))]
    private static IEnumerable<CodeInstruction> updateStats_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var idx_normal_update = list.FindIndex(x => x.opcode == OpCodes.Callvirt &&
                                                    (x.operand as MethodInfo)?.Name == nameof(BaseStats.normalize)) - 2;
        var old_inst = list[idx_normal_update];
        list.InsertRange(idx_normal_update, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_update_stats)))
        ]);
        var new_inst = list[idx_normal_update];
        old_inst.MoveLabelsTo(new_inst);

        var idx_post_update = list.FindIndex(idx_normal_update + 5, x => x.opcode == OpCodes.Callvirt &&
                                                                         (x.operand as MethodInfo)?.Name ==
                                                                         nameof(BaseStats.normalize)) - 2;
        old_inst = list[idx_post_update];
        list.InsertRange(idx_post_update, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_post_update_stats)))
        ]);
        new_inst = list[idx_post_update];
        old_inst.MoveLabelsTo(new_inst);

        return list;
    }

    private static void _post_update_stats(Actor actor)
    {
        actor.GetExtend().PostUpdateStats();
    }

    private static void _extend_update_stats(Actor actor)
    {
        actor.GetExtend().ExtendUpdateStats();
        // 原版已在较早位置读取这些标记；新增或解除共享控制状态时必须同步缓存。
        actor.is_immovable = actor.isImmovable();
        actor.is_ai_frozen = actor.isAiFrozen();
        StopIdleAnimation(actor) = actor.hasStopIdleAnimation();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.clearManagers))]
    private static void clearManagers_postfix(Actor __instance)
    {
        WorldboxGame.I.GeoRegions.SetDirtyUnitsForTile(__instance.current_tile);

        var ae = __instance.GetExtend();
        if (__instance.HasSect())
        {
            var sect = ae.sect;
            // 掌门死亡：在 LeaveSect 清空 LeaderActorID 之前写入死亡日志（对标原版国王死亡的 die() → logKingDead）
            if (sect.data.LeaderActorID == __instance.data.id)
            {
                if (!__instance.attackedBy.isRekt() && __instance.attackedBy.isActor())
                {
                    WorldLogUtils.LogSectLeaderKilled(sect, __instance, __instance.attackedBy.a);
                }
                else
                {
                    WorldLogUtils.LogSectLeaderDead(sect, __instance);
                }
            }

            sect.LeaveSect(__instance);
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.setCurrentTile))]
    private static void setCurrentTile_prefix(Actor __instance, WorldTile pTile)
    {
        WorldboxGame.I.GeoRegions.SetDirtyUnitsForTileChange(__instance.current_tile, pTile);
    }

    /// <summary>临时命魂完成击杀时，把原版战利品、关系和击杀记录全部交给唯一身份所有者。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.newKillAction))]
    private static bool newKillAction_prefix(
        Actor __instance,
        Actor pDeadUnit,
        Kingdom pPrevKingdom,
        AttackType pAttackType,
        out bool __state)
    {
        __state = false;
        if (__instance == null || __instance.isRekt() ||
            !__instance.TryGetExtend(out ActorExtend carrier)) return true;
        SkillCasterContext context = SkillCasterContextService.Resolve(carrier);
        if (!context.IsValid || context.Kind != SkillCarrierKind.Soul || context.Carrier != carrier ||
            context.Owner?.Base == null || context.Owner.Base == __instance || context.Owner.Base.isRekt())
            return true;

        __state = true;
        Actor owner = context.Owner.Base;
        if (pDeadUnit != null && !pDeadUnit.isRekt()) pDeadUnit.attackedBy = owner;
        owner.newKillAction(pDeadUnit, pPrevKingdom, pAttackType);
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.newKillAction))]
    private static void newKillAction_postfix(
        Actor __instance,
        Actor pDeadUnit,
        Kingdom pPrevKingdom,
        bool __state)
    {
        if (__state) return;
        __instance.GetExtend().NewKillAction(pDeadUnit, pPrevKingdom);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.die))]
    private static void killHimself_prefix(Actor __instance, bool pDestroy)
    {
        if (__instance.isAlive() || pDestroy)
        {
            var ae = __instance.GetExtend();
            ae.OnDeath();
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.Dispose))]
    private static void Dispose_prefix(Actor __instance)
    {
        if (!__instance.CheckExtend())
        {
            return;
        }

        var ae = __instance.GetExtend();
        // 主菜单世界还没有寻路器，清理时不能假定它已就绪
        if (PathFinder.Instance != null)
        {
            PathFinder.Instance.Cleanup(__instance.data.id);
        }

        CombatWorldService.RemoveActor(__instance);
        ae.Dispose();
    }
}
