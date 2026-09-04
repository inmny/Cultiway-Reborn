using System;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.ActiveAbilities;
using Cultiway.Content.ActiveAbilities;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>临时命魂人物的出窍、移动、归返、伤害和所有权唯一入口。</summary>
public static class YuanshenTravelService
{
    /// <summary>仅有肉身锚点时允许的最大牵引距离。</summary>
    public const float MaximumTetherDistance = 160f;

    /// <summary>命魂抵达肉身后完成归一的距离。</summary>
    public const float ReturnCompletionDistance = 0.75f;

    /// <summary>分念与高阶元神节点每秒移动的世界格数。</summary>
    public const float NodeMoveSpeed = 8f;

    /// <summary>开始一次出窍需要消耗的最大灵气比例。</summary>
    private const float StartupWakanRatio = 0.05f;

    /// <summary>维持出窍每秒需要消耗的最大灵气比例。</summary>
    private const float UpkeepWakanRatioPerSecond = 0.0015f;

    /// <summary>防止运行入口重复注册。</summary>
    private static bool initialized;

    /// <summary>注册载体解析、神魂受击和异常死亡清理。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        SkillCasterContextService.RegisterResolver(ResolveCasterContext);
        SkillCastPlanner.RegisterCastValidator(CanCastFromCarrier);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Avoidance, ScaleOutsideBodyWeaponDamage);
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Avoidance, FilterSoulCarrierDamage);
        ActorExtend.RegisterActionOnDeath(HandleActorDeath);
    }

    /// <summary>检查人物是否拥有能够出窍的有效化神元神。</summary>
    public static bool CanTravel(ActorExtend actor)
    {
        return actor != null && actor.Base != null && !actor.Base.isRekt() && actor.Base.isAlive() &&
               !YuanshenLifecycleService.IsBodiless(actor) &&
               actor.HasCultisys<Xian>() && actor.GetCultisys<Xian>().CurrLevel >= XianLevels.Huashen &&
               actor.TryGetComponent(out Yuanshen yuanshen) &&
               yuanshen.formation.IsValid && yuanshen.formation.realm == CoreFormationRealm.Yuanshen;
    }

    /// <summary>取得经过所有者与临时人物双向校验的当前命魂载体。</summary>
    /// <param name="actor">技能、资源和身份所属人物。</param>
    /// <param name="carrier">返回当前唯一临时命魂人物。</param>
    /// <returns>双方编号、会话和代次完全一致时返回真。</returns>
    public static bool TryGetSoulCarrier(ActorExtend actor, out Actor carrier)
    {
        carrier = null;
        if (actor?.Base == null || actor.Base.isRekt() ||
            !actor.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.soul_carrier_actor_id <= 0L || World.world?.units == null) return false;

        Actor candidate = World.world.units.get(runtime.soul_carrier_actor_id);
        if (candidate == null || candidate.isRekt() || !candidate.isAlive() ||
            !candidate.TryGetExtend(out ActorExtend carrierExtend) ||
            !carrierExtend.TryGetComponent(out YuanshenSoulCarrierState state) ||
            state.owner_actor_id != actor.Base.data.id ||
            state.session_id != runtime.session_id ||
            state.generation != runtime.soul_carrier_generation)
            return false;
        carrier = candidate;
        return true;
    }

    /// <summary>判断一个人物是否有当前可用的明确魂系战斗手段。</summary>
    /// <param name="attacker">准备选择目标的人物。</param>
    /// <param name="target">需要判断的目标。</param>
    /// <returns>目标不是临时命魂，或攻击者当前能使用含阴技能、魂系法器或神念攻击时返回真。</returns>
    public static bool CanTargetSoulCarrier(Actor attacker, BaseSimObject target)
    {
        if (target == null || !target.isActor() ||
            !target.a.GetExtend().HasComponent<YuanshenSoulCarrierState>()) return true;
        if (target.a.GetExtend().TryGetComponent(out YuanshenSoulCarrierState targetState) &&
            targetState.action == YuanshenSoulCarrierAction.Broken) return false;
        if (attacker == null || attacker.isRekt()) return false;

        ActorExtend caster = attacker.GetExtend();
        using var handles = new ListPool<ActiveAbilityHandle>();
        ActiveAbilityService.Collect(caster, handles);
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            if (!CanAbilityDamageSoul(handle) ||
                !ActiveAbilityService.ResolveControlState(caster, handle).CanUse ||
                !ActiveAbilityService.CanPrepare(caster, handle, target)) continue;
            return true;
        }
        return false;
    }

    /// <summary>按技能元素和法器语义判断一个主动能力是否明确能够伤害魂体。</summary>
    private static bool CanAbilityDamageSoul(ActiveAbilityHandle handle)
    {
        if (handle.ProviderId == YuanshenSoulActiveAbilityProvider.ProviderId) return true;
        if (handle.Source.TryGetComponent(out SkillContainer skill))
            return skill.Asset != null && CanDamageSoul(skill.Asset.Element, AttackType.Other);
        if (handle.ProviderId != ArtifactActiveAbilityProvider.ProviderId ||
            !handle.Source.TryGetComponent(out ArtifactAbilitySet abilitySet)) return false;
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            if (ability.instance_id != handle.EntryId) continue;
            Libraries.ArtifactAbilityAsset asset =
                Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            return asset != null && asset.use_profile.offensive > 0f &&
                   asset.semantics.ContainsExpanded(
                       ModClass.L.SemanticLibrary,
                       ArtifactSemantics.Element.Neg);
        }
        return false;
    }

    /// <summary>读取主命魂当前所在位置；命魂在体时返回原人物位置。</summary>
    public static bool TryGetMainSoulPosition(ActorExtend actor, out Vector3 position)
    {
        if (TryGetSoulCarrier(actor, out Actor carrier))
        {
            position = carrier.GetSimPos();
            return true;
        }
        if (actor?.Base != null && !actor.Base.isRekt())
        {
            position = actor.Base.GetSimPos();
            return true;
        }
        position = default;
        return false;
    }

    /// <summary>创建临时命魂人物，或修改现有载体的目标与心神姿态。</summary>
    public static bool TryTravelTo(ActorExtend actor, Vector3 target, YuanshenTravelStance stance)
    {
        if (!CanTravel(actor) || !TryNormalizeTarget(actor, target, out Vector2 normalizedTarget)) return false;
        WorldTile destinationTile = ResolveTile(normalizedTarget);
        if (destinationTile == null) return false;

        if (TryGetSoulCarrier(actor, out Actor existingCarrier))
        {
            if (!TryApplyStance(actor, existingCarrier, stance)) return false;
            SetDestination(existingCarrier, normalizedTarget, YuanshenSoulCarrierAction.Moving);
            return true;
        }

        NormalizeStaleRuntime(actor);
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        ResolveShares(stance, ResolveMainAvailableShare(runtime), out float soulShare, out float bodyShare);
        DivineSenseBudget currentBudget = DivineSenseBudgetService.Resolve(actor);
        if (soulShare < 10f || currentBudget.TotalLoadCapacity <= 0f ||
            currentBudget.TotalThreadCapacity < 1) return false;

        ref Xian xian = ref actor.GetCultisys<Xian>();
        float maximumWakan = Mathf.Max(0f, actor.Base.stats[BaseStatses.MaxWakan.id]);
        float startupCost = maximumWakan * StartupWakanRatio;
        if (xian.wakan + 0.001f < startupCost) return false;

        runtime.generation = runtime.generation == int.MaxValue ? 1 : Mathf.Max(0, runtime.generation) + 1;
        if (runtime.session_id == 0L)
            runtime.session_id = CreateSessionId(actor.Base.data.id, runtime.generation);
        runtime.soul_carrier_generation = runtime.generation;
        runtime.next_logical_id = Mathf.Max(2, runtime.next_logical_id);
        runtime.stance = stance;
        runtime.main_soul_share = soulShare;
        runtime.body_residual_share = bodyShare;
        runtime.upkeep_elapsed = 0f;
        runtime.travel_elapsed = 0f;

        Actor carrier = CreateSoulCarrier(
            actor,
            runtime.session_id,
            runtime.soul_carrier_generation,
            soulShare,
            normalizedTarget);
        if (carrier == null)
        {
            runtime.main_soul_share = ResolveMainAvailableShare(runtime);
            runtime.body_residual_share = 0f;
            return false;
        }
        runtime.soul_carrier_actor_id = carrier.data.id;
        WakanResourceService.Spend(actor, ref xian, startupCost);
        SetDestination(carrier, normalizedTarget, YuanshenSoulCarrierAction.Moving);
        RefreshOwnerAndArtifacts(actor);
        return true;
    }

    /// <summary>命令当前临时命魂人物停止战斗并返回肉身。</summary>
    public static bool RequestReturn(ActorExtend actor)
    {
        if (!TryGetSoulCarrier(actor, out Actor carrier)) return false;
        SetDestination(carrier, actor.Base.current_position, YuanshenSoulCarrierAction.Returning);
        return true;
    }

    /// <summary>载体抵达肉身后归还全部未锁定份额并销毁临时人物。</summary>
    public static bool CompleteReturn(ActorExtend actor, Actor carrier)
    {
        if (actor == null || carrier == null ||
            !TryGetSoulCarrier(actor, out Actor current) || current != carrier) return false;

        ref YuanshenRuntimeState runtime = ref actor.GetComponent<YuanshenRuntimeState>();
        runtime.soul_carrier_actor_id = 0L;
        runtime.main_soul_share = ResolveMainAvailableShare(runtime);
        runtime.body_residual_share = 0f;
        runtime.upkeep_elapsed = 0f;
        runtime.travel_elapsed = 0f;
        RecycleCarrier(carrier);
        RefreshOwnerAndArtifacts(actor);
        return true;
    }

    /// <summary>销毁一个已经脱离有效会话的临时命魂人物。</summary>
    public static void RecycleInvalidCarrier(Actor carrier)
    {
        if (carrier == null) return;
        if (carrier.TryGetExtend(out ActorExtend extend) &&
            extend.TryGetComponent(out YuanshenSoulCarrierState state))
        {
            Actor owner = World.world?.units?.get(state.owner_actor_id);
            if (owner != null && !owner.isRekt()) NormalizeStaleRuntime(owner.GetExtend());
        }
        RecycleCarrier(carrier);
    }

    /// <summary>回收失去有效所属关系的分念或高阶元神节点。</summary>
    public static void RecycleInvalidNode(Friflo.Engine.ECS.Entity node)
    {
        if (node.IsNull) return;
        if (node.TryGetComponent(out YuanshenNodeIdentity identity))
        {
            Actor owner = World.world?.units?.get(identity.owner_actor_id);
            if (owner != null && !owner.isRekt())
            {
                YuanshenAdvancedNodeService.Disperse(owner.GetExtend(), node, 1f);
                return;
            }
        }
        YuanshenAnchorNetworkService.ReleaseResidence(node);
        YuanshenNodeLockService.UnregisterNode(node);
        if (!node.Tags.Has<TagRecycle>()) node.AddTag<TagRecycle>();
    }

    /// <summary>按整秒结算一次命魂维持消耗。</summary>
    public static bool TryPayUpkeep(ActorExtend actor, float elapsedSeconds)
    {
        return elapsedSeconds > 0f &&
               WakanResourceService.TrySpendMaximumRatio(actor, UpkeepWakanRatioPerSecond * elapsedSeconds);
    }

    /// <summary>确保已经形成元神的人物从第一刻就持有唯一心神总账。</summary>
    public static void EnsureMindLedger(ActorExtend actor)
    {
        if (actor == null) return;
        ref YuanshenRuntimeState runtime = ref actor.GetOrAddComponent<YuanshenRuntimeState>();
        if (runtime.session_id != 0L || runtime.soul_carrier_actor_id > 0L ||
            runtime.main_soul_share > 0f || runtime.body_residual_share > 0f) return;
        runtime.main_soul_share = runtime.AvailableShare;
    }

    /// <summary>判断人物是否仍有命魂、分念或高阶节点离开主位置。</summary>
    public static bool HasDetachedNodes(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime)) return false;
        if (TryGetSoulCarrier(actor, out _)) return true;
        return CountValidNodes(runtime.thought_nodes) > 0 || CountValidNodes(runtime.advanced_nodes) > 0;
    }

    /// <summary>统计人物当前全部有效元神载体与地图节点。</summary>
    public static int CountActiveNodes(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState runtime)) return 0;
        int count = TryGetSoulCarrier(actor, out _) ? 1 : 0;
        return count + CountValidNodes(runtime.thought_nodes) + CountValidNodes(runtime.advanced_nodes);
    }

    /// <summary>从命魂、肉身残留和活动节点依次转移一笔全局神魂创伤份额。</summary>
    public static float LockMainMindShare(ActorExtend actor, float requestedShare)
    {
        if (actor == null || requestedShare <= 0f) return 0f;
        EnsureMindLedger(actor);
        ref YuanshenRuntimeState runtime = ref actor.GetComponent<YuanshenRuntimeState>();
        float remaining = Mathf.Min(Mathf.Max(0f, requestedShare), runtime.AvailableShare);
        float locked = 0f;

        float fromSoul = Mathf.Min(runtime.main_soul_share, remaining);
        runtime.main_soul_share -= fromSoul;
        remaining -= fromSoul;
        locked += fromSoul;
        if (fromSoul > 0f && TryGetSoulCarrier(actor, out Actor carrier))
            LockCarrierShare(carrier.GetExtend(), fromSoul);

        float fromBody = Mathf.Min(runtime.body_residual_share, remaining);
        runtime.body_residual_share -= fromBody;
        remaining -= fromBody;
        locked += fromBody;
        LockNodeList(runtime.thought_nodes, ref remaining, ref locked);
        LockNodeList(runtime.advanced_nodes, ref remaining, ref locked);
        if (locked <= 0f) return 0f;

        runtime.injury_locked_share = Mathf.Clamp(runtime.injury_locked_share + locked, 0f, 100f);
        ApplySoulTrauma(actor, locked);
        NotifyMindStateChanged(actor);
        return locked;
    }

    /// <summary>判断目标是否仍处于当前肉身或本命法器锚点的牵引范围内。</summary>
    public static bool IsWithinTether(ActorExtend actor, Vector2 position)
    {
        return YuanshenArtifactAnchorService.IsWithinTether(actor, position);
    }

    /// <summary>按姿态解析命魂份额与肉身残留份额。</summary>
    public static void ResolveShares(
        YuanshenTravelStance stance,
        out float lifeSoulShare,
        out float bodyShare)
    {
        ResolveShares(stance, 100f, out lifeSoulShare, out bodyShare);
    }

    /// <summary>在扣除创伤锁定后，按姿态分配剩余可用心神。</summary>
    public static void ResolveShares(
        YuanshenTravelStance stance,
        float availableShare,
        out float lifeSoulShare,
        out float bodyShare)
    {
        float ratio = stance switch
        {
            YuanshenTravelStance.Guarded => 0.4f,
            YuanshenTravelStance.Balanced => 0.6f,
            YuanshenTravelStance.FullRelease => 0.9f,
            _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, "未知心神姿态。")
        };
        float available = Mathf.Clamp(availableShare, 0f, 100f);
        lifeSoulShare = available * ratio;
        bodyShare = available - lifeSoulShare;
    }

    /// <summary>按人物魂量、元神强度、层数和份额计算命魂完整度上限。</summary>
    public static float ResolveIntegrityMaximum(ActorExtend actor, float mindShare)
    {
        float maxSoul = Mathf.Max(1f, actor.Base.stats[WorldboxGame.BaseStats.MaxSoul.id]);
        float strength = actor.TryGetComponent(out Yuanshen yuanshen)
            ? Mathf.Max(0.25f, yuanshen.strength)
            : 0.25f;
        float stageScale = actor.TryGetComponent(out Yuanshen current)
            ? 1f + Mathf.Clamp(current.stage, 0, 9) * 0.08f
            : 1f;
        return Mathf.Max(1f, maxSoul * Mathf.Clamp(mindShare, 0f, 100f) * 0.01f * strength * stageScale);
    }

    /// <summary>创伤或分念改变份额后立即刷新人物属性和法器调度。</summary>
    public static void NotifyMindStateChanged(ActorExtend actor)
    {
        if (actor?.Base != null && !actor.Base.isRekt()) RefreshOwnerAndArtifacts(actor);
    }

    /// <summary>同步临时命魂人物用于界面显示的生命比例。</summary>
    public static void SynchronizeCarrierHealth(Actor carrier, in YuanshenSoulCarrierState state)
    {
        if (carrier == null || carrier.isRekt()) return;
        int maximum = Mathf.Max(1, carrier.getMaxHealth());
        carrier.setHealth(Mathf.Clamp(Mathf.CeilToInt(maximum * state.IntegrityRatio), 1, maximum));
    }

    /// <summary>恢复临时命魂完整度，并按恢复比例解锁对应的心神创伤。</summary>
    /// <param name="carrier">实际接受恢复的临时命魂人物。</param>
    /// <param name="amount">要恢复的完整度数值。</param>
    /// <returns>实际恢复的完整度。</returns>
    public static float RestoreSoulCarrierIntegrity(Actor carrier, float amount)
    {
        if (carrier == null || carrier.isRekt() || amount <= 0f ||
            !carrier.GetExtend().TryGetComponent(out YuanshenSoulCarrierState snapshot) ||
            snapshot.action == YuanshenSoulCarrierAction.Broken) return 0f;
        Actor ownerActor = World.world?.units?.get(snapshot.owner_actor_id);
        if (ownerActor == null || ownerActor.isRekt()) return 0f;
        ActorExtend owner = ownerActor.GetExtend();
        if (!TryGetSoulCarrier(owner, out Actor current) || current != carrier) return 0f;

        ref YuanshenSoulCarrierState state = ref carrier.GetExtend().GetComponent<YuanshenSoulCarrierState>();
        float before = state.current_integrity;
        state.current_integrity = Mathf.Min(state.maximum_integrity, before + amount);
        float restored = state.current_integrity - before;
        if (restored <= 0f) return 0f;

        float allocated = Mathf.Max(0f, state.mind_share + state.locked_share);
        float intendedLocked = allocated * (1f - state.IntegrityRatio);
        float unlocked = Mathf.Clamp(state.locked_share - intendedLocked, 0f, state.locked_share);
        if (unlocked > 0f && owner.TryGetComponent(out YuanshenRuntimeState _))
        {
            state.locked_share -= unlocked;
            state.mind_share += unlocked;
            ref YuanshenRuntimeState runtime = ref owner.GetComponent<YuanshenRuntimeState>();
            runtime.injury_locked_share = Mathf.Max(0f, runtime.injury_locked_share - unlocked);
            runtime.main_soul_share += unlocked;
            NotifyMindStateChanged(owner);
        }
        SynchronizeCarrierHealth(carrier, in state);
        return restored;
    }

    /// <summary>把临时人物或离体肉身解析为共享技能所有者与实际载体。</summary>
    private static SkillCasterContext? ResolveCasterContext(ActorExtend requested)
    {
        if (requested?.Base == null || requested.Base.isRekt()) return null;
        if (requested.TryGetComponent(out YuanshenSoulCarrierState carrierState))
        {
            Actor ownerActor = World.world?.units?.get(carrierState.owner_actor_id);
            if (ownerActor == null || ownerActor.isRekt()) return default(SkillCasterContext);
            ActorExtend owner = ownerActor.GetExtend();
            if (!TryGetSoulCarrier(owner, out Actor current) || current != requested.Base)
                return default(SkillCasterContext);
            return new SkillCasterContext(
                owner,
                requested,
                Mathf.Clamp01(carrierState.mind_share / 100f),
                SkillCarrierKind.Soul,
                !YuanshenLifecycleService.IsBodiless(owner));
        }

        if (YuanshenLifecycleService.IsBodiless(requested))
        {
            float share = requested.TryGetComponent(out YuanshenRuntimeState bodilessRuntime)
                ? bodilessRuntime.AvailableShare
                : 100f;
            return new SkillCasterContext(
                requested,
                requested,
                Mathf.Clamp01(share / 100f),
                SkillCarrierKind.Soul,
                false);
        }

        if (requested.TryGetComponent(out YuanshenRuntimeState runtime) && runtime.IsOutside &&
            TryGetSoulCarrier(requested, out _))
        {
            return new SkillCasterContext(
                requested,
                requested,
                Mathf.Clamp01(runtime.body_residual_share / 100f),
                SkillCarrierKind.Physical,
                true);
        }
        return null;
    }

    /// <summary>按技能资产声明限制临时命魂的技能载体。</summary>
    private static bool CanCastFromCarrier(ActorExtend caster, Friflo.Engine.ECS.Entity skill)
    {
        if (caster == null || skill.IsNull || !skill.TryGetComponent(out SkillContainer container) ||
            container.Asset == null) return true;
        SkillCasterContext context = SkillCasterContextService.Resolve(caster);
        if (!context.IsValid || context.EffectScale <= 0f) return false;
        return container.Asset.CarrierRequirement switch
        {
            SkillCarrierRequirement.General => true,
            SkillCarrierRequirement.PhysicalBody =>
                context.Kind == SkillCarrierKind.Physical && context.HasPhysicalBody,
            SkillCarrierRequirement.Soul => context.Kind == SkillCarrierKind.Soul,
            _ => false,
        };
    }

    /// <summary>命魂离体时，肉身的拳脚、武器和肉身战技按残留心神线性缩放。</summary>
    private static void ScaleOutsideBodyWeaponDamage(
        ActorExtend target,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        if (attackType != AttackType.Weapon || attacker?.isActor() != true || attacker.isRekt()) return;
        ActorExtend source = attacker.a.GetExtend();
        if (source.HasComponent<YuanshenSoulCarrierState>())
        {
            damage = 0f;
            return;
        }
        if (!source.TryGetComponent(out YuanshenRuntimeState runtime) || !runtime.IsOutside) return;
        damage *= Mathf.Clamp01(runtime.body_residual_share * 0.01f);
    }

    /// <summary>判断伤害是否来自明确的阴性攻击，并排除毒、疾病、肉体和环境类别。</summary>
    public static bool CanDamageSoul(ElementComposition composition, AttackType attackType)
    {
        if (attackType != AttackType.Other || composition.neg <= 0f) return false;
        ElementComposition poison = ElementComposition.Static.Poison;
        for (int i = 0; i < 8; i++)
            if (Mathf.Abs(composition[i] - poison[i]) > 0.0001f) return true;
        return false;
    }

    /// <summary>临时命魂只接收明确阴性攻击，并把损失写回所有者心神总账。</summary>
    private static void FilterSoulCarrierDamage(
        ActorExtend target,
        BaseSimObject attacker,
        ElementComposition composition,
        AttackType attackType,
        ref float damage)
    {
        if (target == null || !target.TryGetComponent(out YuanshenSoulCarrierState snapshot)) return;
        float incoming = Mathf.Max(0f, damage);
        damage = 0f;
        if (incoming <= 0f || snapshot.action == YuanshenSoulCarrierAction.Broken) return;
        if (!CanDamageSoul(composition, attackType))
        {
            if (attacker?.isActor() == true && !attacker.isRekt())
            {
                if (attacker.a.attack_target == target.Base) attacker.a.clearAttackTarget();
                attacker.ignoreTarget(target.Base);
            }
            return;
        }

        Actor ownerActor = World.world?.units?.get(snapshot.owner_actor_id);
        if (ownerActor == null || ownerActor.isRekt())
        {
            ref YuanshenSoulCarrierState orphan = ref target.GetComponent<YuanshenSoulCarrierState>();
            orphan.action = YuanshenSoulCarrierAction.Broken;
            return;
        }
        ActorExtend owner = ownerActor.GetExtend();
        if (!TryGetSoulCarrier(owner, out Actor current) || current != target.Base) return;

        ref YuanshenSoulCarrierState state = ref target.GetComponent<YuanshenSoulCarrierState>();
        float before = state.current_integrity;
        state.current_integrity = Mathf.Max(0f, before - incoming);
        state.last_attacker_actor_id = attacker?.isActor() == true && !attacker.isRekt()
            ? attacker.a.data.id
            : 0L;
        state.interference_seconds += 0.35f;
        state.last_interference_at = World.world?.getCurWorldTime() ?? 0d;
        state.tether_condition = ResolveTetherCondition(state.interference_seconds);

        float allocated = Mathf.Max(0f, state.mind_share + state.locked_share);
        float intendedLocked = allocated * (1f - state.IntegrityRatio);
        float newlyLocked = Mathf.Max(0f, intendedLocked - state.locked_share);
        if (newlyLocked > 0f)
        {
            state.locked_share += newlyLocked;
            state.mind_share = Mathf.Max(0f, state.mind_share - newlyLocked);
            ref YuanshenRuntimeState runtime = ref owner.GetComponent<YuanshenRuntimeState>();
            runtime.main_soul_share = Mathf.Max(0f, runtime.main_soul_share - newlyLocked);
            runtime.injury_locked_share = Mathf.Clamp(runtime.injury_locked_share + newlyLocked, 0f, 100f);
            ApplySoulTrauma(owner, newlyLocked);
            NotifyMindStateChanged(owner);
        }
        if (state.current_integrity <= 0f) state.action = YuanshenSoulCarrierAction.Broken;
        SynchronizeCarrierHealth(target.Base, in state);
    }

    /// <summary>意外死亡时解除临时人物与所有者之间的双向引用。</summary>
    private static void HandleActorDeath(ActorExtend actor)
    {
        if (actor == null) return;
        if (actor.TryGetComponent(out YuanshenSoulCarrierState carrierState))
        {
            Actor ownerActor = World.world?.units?.get(carrierState.owner_actor_id);
            if (ownerActor == null || ownerActor.isRekt()) return;
            ActorExtend owner = ownerActor.GetExtend();
            if (!owner.TryGetComponent(out YuanshenRuntimeState runtime) ||
                runtime.soul_carrier_actor_id != actor.Base.data.id) return;
            ref YuanshenRuntimeState mutable = ref owner.GetComponent<YuanshenRuntimeState>();
            mutable.soul_carrier_actor_id = 0L;
            mutable.main_soul_share = ResolveMainAvailableShare(mutable);
            mutable.body_residual_share = 0f;
            RefreshOwnerAndArtifacts(owner);
            return;
        }

        if (TryGetSoulCarrier(actor, out Actor carrier)) RecycleCarrier(carrier);
    }

    /// <summary>创建只携带位置、局部战斗状态和完整度的临时人物。</summary>
    private static Actor CreateSoulCarrier(
        ActorExtend owner,
        long sessionId,
        int generation,
        float mindShare,
        Vector2 destination)
    {
        if (World.world?.units == null || owner.Base.current_tile == null || Actors.BodilessYuanshen == null)
            return null;
        Actor carrier = World.world.units.spawnNewUnit(
            Actors.BodilessYuanshen.id,
            owner.Base.current_tile,
            pSpawnHeight: 0f);
        if (carrier == null) return null;
        carrier.setFlying(true);
        if (owner.Base.kingdom != null) carrier.joinKingdom(owner.Base.kingdom);
        carrier.setName(owner.Base.getName() + "元神");
        carrier.clearAttackTarget();
        if (owner.Base.has_attack_target && owner.Base.isEnemyTargetAlive() &&
            owner.Base.attack_target != null && !owner.Base.attack_target.isRekt())
        {
            carrier.setAttackTarget(owner.Base.attack_target);
            carrier.beh_actor_target = owner.Base.attack_target;
        }
        float maximumIntegrity = ResolveIntegrityMaximum(owner, mindShare);
        carrier.GetExtend().AddComponent(new YuanshenSoulCarrierState
        {
            owner_actor_id = owner.Base.data.id,
            session_id = sessionId,
            generation = generation,
            mind_share = mindShare,
            maximum_integrity = maximumIntegrity,
            current_integrity = maximumIntegrity,
            locked_share = 0f,
            destination = destination,
            action = YuanshenSoulCarrierAction.Moving,
            tether_condition = YuanshenTetherCondition.Stable,
            interference_seconds = 0f,
            last_interference_at = 0d,
            last_attacker_actor_id = 0L,
            movement_refresh_elapsed = 0f,
        });
        carrier.updateStats();
        SynchronizeCarrierHealth(carrier, carrier.GetExtend().GetComponent<YuanshenSoulCarrierState>());
        return carrier;
    }

    /// <summary>在现有临时人物上提交新的心神姿态。</summary>
    private static bool TryApplyStance(ActorExtend actor, Actor carrier, YuanshenTravelStance stance)
    {
        ref YuanshenRuntimeState runtime = ref actor.GetComponent<YuanshenRuntimeState>();
        ResolveShares(stance, ResolveMainAvailableShare(runtime), out float soulShare, out float bodyShare);
        if (soulShare < 10f || DivineSenseBudgetService.Resolve(actor).TotalThreadCapacity < 1) return false;
        runtime.stance = stance;
        runtime.main_soul_share = soulShare;
        runtime.body_residual_share = bodyShare;
        ref YuanshenSoulCarrierState state = ref carrier.GetExtend().GetComponent<YuanshenSoulCarrierState>();
        float integrityRatio = state.IntegrityRatio;
        state.mind_share = soulShare;
        float allocated = soulShare + state.locked_share;
        state.maximum_integrity = ResolveIntegrityMaximum(actor, allocated);
        state.current_integrity = state.maximum_integrity * integrityRatio;
        SynchronizeCarrierHealth(carrier, in state);
        RefreshOwnerAndArtifacts(actor);
        return true;
    }

    /// <summary>把临时人物切换为前往明确地块。</summary>
    private static void SetDestination(
        Actor carrier,
        Vector2 target,
        YuanshenSoulCarrierAction action)
    {
        if (carrier == null || carrier.isRekt() ||
            !carrier.TryGetExtend(out ActorExtend extend) ||
            !extend.HasComponent<YuanshenSoulCarrierState>()) return;
        ref YuanshenSoulCarrierState state = ref extend.GetComponent<YuanshenSoulCarrierState>();
        state.destination = target;
        state.action = action;
        state.movement_refresh_elapsed = 1f;
        if (action == YuanshenSoulCarrierAction.Returning)
        {
            carrier.cancelAllBeh();
            carrier.clearAttackTarget();
        }
        IssueMove(carrier, target);
    }

    /// <summary>向原版人物移动系统提交一个已经校验的目标。</summary>
    internal static bool IssueMove(Actor carrier, Vector2 target)
    {
        WorldTile tile = ResolveTile(target);
        if (carrier == null || carrier.isRekt() || tile == null) return false;
        carrier.beh_tile_target = tile;
        carrier.goTo(tile, pPathOnWater: true, pWalkOnBlocks: true, pWalkOnLava: true);
        return true;
    }

    /// <summary>从活动节点扣除后得到命魂与肉身仍可分配的心神。</summary>
    private static float ResolveMainAvailableShare(in YuanshenRuntimeState runtime)
    {
        float allocated = 0f;
        AddAllocatedShares(runtime.thought_nodes, ref allocated);
        AddAllocatedShares(runtime.advanced_nodes, ref allocated);
        return Mathf.Clamp(runtime.AvailableShare - allocated, 0f, 100f);
    }

    /// <summary>从一组活动节点依次锁定剩余创伤份额。</summary>
    private static void LockNodeList(
        System.Collections.Generic.List<YuanshenNodeHandle> handles,
        ref float remaining,
        ref float locked)
    {
        if (handles == null || remaining <= 0f) return;
        for (int i = 0; i < handles.Count && remaining > 0f; i++)
        {
            if (!YuanshenNodeLockService.TryResolve(handles[i], out Friflo.Engine.ECS.Entity node) ||
                !node.TryGetComponent(out YuanshenNodeIdentity identity)) continue;
            float amount = Mathf.Min(Mathf.Max(0f, identity.mind_share), remaining);
            if (amount <= 0f) continue;
            LockNodeShare(node, amount);
            remaining -= amount;
            locked += amount;
        }
    }

    /// <summary>把临时命魂人物的一部分可用份额同步转为完整度锁伤。</summary>
    private static void LockCarrierShare(ActorExtend carrier, float amount)
    {
        if (carrier == null || amount <= 0f || !carrier.HasComponent<YuanshenSoulCarrierState>()) return;
        ref YuanshenSoulCarrierState state = ref carrier.GetComponent<YuanshenSoulCarrierState>();
        amount = Mathf.Min(Mathf.Max(0f, state.mind_share), amount);
        state.mind_share -= amount;
        state.locked_share += amount;
        float allocated = Mathf.Max(0f, state.mind_share + state.locked_share);
        float loss = allocated > 0f ? state.maximum_integrity * amount / allocated : 0f;
        state.current_integrity = Mathf.Max(0f, state.current_integrity - loss);
        SynchronizeCarrierHealth(carrier.Base, in state);
    }

    /// <summary>把地图节点的一部分可用份额同步转为完整度锁伤。</summary>
    private static void LockNodeShare(Friflo.Engine.ECS.Entity node, float amount)
    {
        if (node.IsNull || amount <= 0f || !node.HasComponent<YuanshenNodeIdentity>()) return;
        ref YuanshenNodeIdentity identity = ref node.GetComponent<YuanshenNodeIdentity>();
        amount = Mathf.Min(Mathf.Max(0f, identity.mind_share), amount);
        identity.mind_share -= amount;
        if (!node.HasComponent<YuanshenNodeIntegrity>()) return;
        ref YuanshenNodeIntegrity integrity = ref node.GetComponent<YuanshenNodeIntegrity>();
        integrity.locked_share += amount;
        float loss = integrity.allocated_share > 0f
            ? integrity.maximum * amount / integrity.allocated_share
            : 0f;
        integrity.current = Mathf.Max(0f, integrity.current - loss);
    }

    /// <summary>统计一组句柄中仍然有效的节点数量。</summary>
    private static int CountValidNodes(System.Collections.Generic.List<YuanshenNodeHandle> handles)
    {
        if (handles == null) return 0;
        int count = 0;
        for (int i = 0; i < handles.Count; i++)
            if (YuanshenNodeLockService.TryResolve(handles[i], out _)) count++;
        return count;
    }

    /// <summary>累计一组活动节点正在承载的心神份额。</summary>
    private static void AddAllocatedShares(
        System.Collections.Generic.List<YuanshenNodeHandle> handles,
        ref float allocated)
    {
        if (handles == null) return;
        for (int i = 0; i < handles.Count; i++)
        {
            if (!YuanshenNodeLockService.TryResolve(handles[i], out Friflo.Engine.ECS.Entity node) ||
                !node.TryGetComponent(out YuanshenNodeIdentity identity)) continue;
            allocated += Mathf.Max(0f, identity.mind_share);
        }
    }

    /// <summary>校验目标坐标和牵引距离。</summary>
    private static bool TryNormalizeTarget(ActorExtend actor, Vector3 target, out Vector2 normalized)
    {
        normalized = new Vector2(target.x, target.y);
        if (float.IsNaN(normalized.x) || float.IsNaN(normalized.y) ||
            float.IsInfinity(normalized.x) || float.IsInfinity(normalized.y)) return false;
        return IsWithinTether(actor, normalized);
    }

    /// <summary>临时人物引用损坏时把所有者恢复为命魂在体状态。</summary>
    private static void NormalizeStaleRuntime(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenRuntimeState snapshot) ||
            snapshot.soul_carrier_actor_id <= 0L || TryGetSoulCarrier(actor, out _)) return;
        ref YuanshenRuntimeState runtime = ref actor.GetComponent<YuanshenRuntimeState>();
        runtime.soul_carrier_actor_id = 0L;
        runtime.main_soul_share = ResolveMainAvailableShare(runtime);
        runtime.body_residual_share = 0f;
        runtime.upkeep_elapsed = 0f;
        runtime.travel_elapsed = 0f;
        RefreshOwnerAndArtifacts(actor);
    }

    /// <summary>安全销毁一个已经解除绑定的临时命魂人物。</summary>
    private static void RecycleCarrier(Actor carrier)
    {
        if (carrier == null || carrier.isRekt()) return;
        if (carrier.TryGetExtend(out ActorExtend extend) && extend.HasComponent<YuanshenSoulCarrierState>())
            extend.E.RemoveComponent<YuanshenSoulCarrierState>();
        carrier.cancelAllBeh();
        carrier.clearAttackTarget();
        carrier.dieAndDestroy(AttackType.None);
    }

    /// <summary>把一笔锁伤同步为人物神魂创伤状态。</summary>
    private static void ApplySoulTrauma(ActorExtend actor, float share)
    {
        if (actor?.Base == null || actor.Base.isRekt() || share <= 0f) return;
        CombatStatusEffects.ApplyStatus(
            actor.Base,
            StatusEffects.SoulTrauma,
            Mathf.Max(Cultiway.Const.TimeScales.SecPerMonth,
                share * Cultiway.Const.TimeScales.SecPerMonth),
            actor.Base);
    }

    /// <summary>在没有新干扰时缓慢衰减牵引干扰，并刷新离散牵引状态。</summary>
    /// <param name="condition">需要更新的牵引状态。</param>
    /// <param name="interferenceSeconds">累计干扰秒数。</param>
    /// <param name="lastInterferenceAt">最近一次受到干扰的世界时间。</param>
    /// <param name="deltaTime">本帧秒数。</param>
    internal static void UpdateTetherCondition(
        ref YuanshenTetherCondition condition,
        ref float interferenceSeconds,
        ref double lastInterferenceAt,
        float deltaTime)
    {
        if (condition == YuanshenTetherCondition.Severed) return;
        double now = World.world?.getCurWorldTime() ?? 0d;
        if (now - lastInterferenceAt >= 1d)
            interferenceSeconds = Mathf.Max(0f, interferenceSeconds - deltaTime * 0.5f);
        condition = ResolveTetherCondition(interferenceSeconds);
    }

    /// <summary>按累计干扰秒数取得牵引状态。</summary>
    internal static YuanshenTetherCondition ResolveTetherCondition(float seconds)
    {
        return seconds switch
        {
            >= 3f => YuanshenTetherCondition.Severed,
            >= 2f => YuanshenTetherCondition.Obstructed,
            >= 0.75f => YuanshenTetherCondition.Fluctuating,
            _ => YuanshenTetherCondition.Stable
        };
    }

    /// <summary>解析一个地面坐标对应的有效世界地块。</summary>
    private static WorldTile ResolveTile(Vector2 position)
    {
        return World.world?.GetTileSimple(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
    }

    /// <summary>从人物编号和单调代次生成当前世界内稳定的会话编号。</summary>
    private static long CreateSessionId(long ownerActorId, int generation)
    {
        unchecked
        {
            return ownerActorId * 2147483647L + generation;
        }
    }

    /// <summary>人物份额改变后立即刷新属性与法器调度。</summary>
    private static void RefreshOwnerAndArtifacts(ActorExtend actor)
    {
        actor.Base.updateStats();
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
    }
}
