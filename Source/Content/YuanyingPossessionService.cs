using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Progression;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>元婴夺舍的集中平衡规则。</summary>
public static class YuanyingPossessionRules
{
    public const float SearchRadius = 120f;
    public const float MinimumSuccessChance = 0.10f;
    public const float MaximumSuccessChance = 0.95f;
    public const float NewBodyHealthRatio = 0.25f;
    public const float NewBodyWakanRatio = 0.10f;
    public const float MinimumSeedStrengthLoss = 0.10f;
    public const float MaximumSeedStrengthLoss = 0.35f;
    public const float SoulLifetime = 12f * TimeScales.SecPerMonth;
    public const float ChannelDuration = TimeScales.SecPerMonth;
    public const float BodyDisharmonyDuration = TimeScales.SecPerYear;
    public const float SoulTraumaDuration = 2f * TimeScales.SecPerYear;

    internal const float SearchInterval = 0.5f;
    internal const float BehaviourInterval = 0.2f;
    internal const int SearchRadiusSquared = 120 * 120;
}

/// <summary>致死出逃、寻主、神魂对抗和换体提交的总入口。</summary>
public static class YuanyingPossessionService
{
    private const string FormYuanyingTransition = "xian.form_yuanying";
    private static readonly HashSet<string> BodyTraitGroups = new(StringComparer.Ordinal)
    {
        "body",
        "physique",
        "health",
        "appearance",
        "protection"
    };
    private static readonly Dictionary<long, float> PendingPowerRestores = new();
    private static bool runtimeAssetErrorLogged;
    private static bool initialized;

    public static void Init()
    {
        if (initialized) return;
        if (!HasRequiredRuntimeAssets()) return;
        initialized = true;
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.LastResort, OnFinalDamage);
        ProgressionLifecycle.RegisterCommitted(OnProgressionCommitted);
    }

    private static bool HasRequiredRuntimeAssets()
    {
        bool ready = Actors.YuanyingSoul != null
                     && StatusEffects.YuanyingEscape != null
                     && StatusEffects.BodyDisharmony != null
                     && StatusEffects.SoulTrauma != null;
        if (ready || runtimeAssetErrorLogged) return ready;

        runtimeAssetErrorLogged = true;
        ModClass.LogError(
            $"元婴夺舍依赖资产未完成初始化: soul={Actors.YuanyingSoul != null}, "
            + $"escape={StatusEffects.YuanyingEscape != null}, "
            + $"disharmony={StatusEffects.BodyDisharmony != null}, trauma={StatusEffects.SoulTrauma != null}");
        return false;
    }

    public static bool IsEscapedSoul(Actor actor)
    {
        return actor != null
               && !actor.isRekt()
               && actor.GetExtend().HasComponent<YuanyingSoulState>();
    }

    public static bool IsEligibleHost(Actor target)
    {
        if (target == null || target.isRekt() || !target.isAlive() || target.current_tile == null) return false;
        if (target.asset == null || !target.asset.has_soul || !target.isSapient()) return false;
        if (target.isInsideSomething()) return false;

        ActorExtend targetExtend = target.GetExtend();
        if (!targetExtend.HasElementRoot() || targetExtend.HasComponent<YuanyingSoulState>()) return false;
        return !targetExtend.TryGetComponent(out Xian xian) || xian.CurrLevel < XianLevels.Yuanying;
    }

    /// <summary>由专用行为反复调用，直到夺舍完成或元婴真正死亡。</summary>
    internal static bool TickSoul(Actor source)
    {
        if (!IsEscapedSoul(source)) return false;
        if (!HasRequiredRuntimeAssets())
        {
            TerminateSoul(source, "runtime_assets_missing");
            return false;
        }
        ActorExtend sourceExtend = source.GetExtend();
        ref YuanyingSoulState state = ref sourceExtend.GetComponent<YuanyingSoulState>();
        double now = World.world.getCurWorldTime();
        if (now >= state.expires_at)
        {
            TerminateSoul(source, "expired");
            return false;
        }

        Actor target = ResolveTarget(source, ref state);
        if (target == null)
        {
            source.timer_action = YuanyingPossessionRules.SearchInterval;
            return true;
        }

        int distanceSquared = Toolbox.SquaredDistVec2(source.current_tile.pos, target.current_tile.pos);
        if (distanceSquared > 1)
        {
            state.channel_started_at = 0d;
            source.beh_actor_target = target;
            source.goTo(target.current_tile, pPathOnWater: true);
            source.timer_action = YuanyingPossessionRules.BehaviourInterval;
            return true;
        }

        if (state.channel_started_at <= 0d) state.channel_started_at = now;
        if (now - state.channel_started_at < YuanyingPossessionRules.ChannelDuration)
        {
            source.timer_action = YuanyingPossessionRules.BehaviourInterval;
            return true;
        }

        if (!TryCaptureHost(target, out HostBodySnapshot snapshot))
        {
            ClearTarget(source, ref state);
            return true;
        }

        SoulContestResult contest = SoulContestResolver.Resolve(sourceExtend, target.GetExtend());
        if (!Randy.randomChance(contest.SuccessChance))
        {
            target.addStatusEffect(StatusEffects.SoulTrauma.id,
                YuanyingPossessionRules.SoulTraumaDuration, pColorEffect: false);
            WorldLogUtils.LogYuanyingPossessionFailure(source, target);
            TerminateSoul(source, "contest_failed");
            return false;
        }

        string sourceName = source.getName();
        string targetName = target.getName();
        if (!TryCommitPossession(source, target, snapshot, contest.Compatibility))
        {
            TerminateSoul(source, "commit_failed");
            return false;
        }

        WorldLogUtils.LogYuanyingPossessionSuccess(source, sourceName, targetName);
        return false;
    }

    internal static void QueuePowerRestore(ActorExtend actor, float powerLevel)
    {
        PendingPowerRestores[actor.Base.data.id] = powerLevel;
    }

    internal static void TerminateSoul(Actor actor, string reason)
    {
        if (actor == null || actor.isRekt()) return;
        ActorExtend actorExtend = actor.GetExtend();
        if (actorExtend.HasComponent<YuanyingSoulState>())
            actorExtend.E.RemoveComponent<YuanyingSoulState>();
        if (actorExtend.HasComponent<YuanyingSeed>()) actorExtend.E.RemoveComponent<YuanyingSeed>();
        if (actorExtend.HasComponent<Yuanying>()) actorExtend.E.RemoveComponent<Yuanying>();
        PendingPowerRestores.Remove(actor.data.id);
        ModClass.LogInfo($"元婴 {actor.data.id} 真正消亡: {reason}");
        actor.dieAndDestroy(AttackType.None);
    }

    private static Actor ResolveTarget(Actor source, ref YuanyingSoulState state)
    {
        if (state.target_actor_id >= 0)
        {
            Actor current = World.world.units.get(state.target_actor_id);
            if (IsEligibleHost(current)
                && Toolbox.SquaredDistVec2(source.current_tile.pos, current.current_tile.pos)
                <= YuanyingPossessionRules.SearchRadiusSquared)
                return current;
            ClearTarget(source, ref state);
        }

        Actor selected = FindBestHost(source);
        if (selected == null) return null;
        state.target_actor_id = selected.data.id;
        source.beh_actor_target = selected;
        return selected;
    }

    private static Actor FindBestHost(Actor source)
    {
        List<Actor> actors = World.world.units.getSimpleList();
        Actor best = null;
        float bestScore = float.NegativeInfinity;
        YuanyingSeed seed = ResolveEscapeSeed(source.GetExtend());
        for (var i = 0; i < actors.Count; i++)
        {
            Actor candidate = actors[i];
            if (candidate == source || !IsEligibleHost(candidate)) continue;
            int distanceSquared = Toolbox.SquaredDistVec2(source.current_tile.pos, candidate.current_tile.pos);
            if (distanceSquared > YuanyingPossessionRules.SearchRadiusSquared) continue;

            float compatibility = SoulContestResolver.CalculateCompatibility(seed.formation,
                candidate.GetExtend().GetElementRoot());
            float healthRatio = candidate.getMaxHealth() > 0
                ? Mathf.Clamp01((float)candidate.getHealth() / candidate.getMaxHealth())
                : 0f;
            float distance = Mathf.Sqrt(distanceSquared);
            float score = compatibility * 80f + healthRatio * 20f - distance * 0.15f
                          - candidate.GetExtend().GetPowerLevel() * 2f;
            if (score > bestScore || Mathf.Approximately(score, bestScore)
                && (best == null || candidate.data.id < best.data.id))
            {
                best = candidate;
                bestScore = score;
            }
        }
        return best;
    }

    private static void OnFinalDamage(
        ActorExtend actorExtend,
        BaseSimObject attacker,
        ElementComposition damageComposition,
        AttackType attackType,
        ref float damage)
    {
        Actor actor = actorExtend?.Base;
        if (actor == null || actor.data == null || actor.current_tile == null || actor.isRekt()) return;
        if (Mathf.Floor(damage) < actor.data.health) return;
        if (attackType is AttackType.Divine or AttackType.Metamorphosis) return;
        if (!HasRequiredRuntimeAssets() || World.world == null || World.world.subspecies == null) return;
        if (actorExtend.HasComponent<YuanyingSoulState>()
            || actor.hasStatus(StatusEffects.BodyDisharmony.id)) return;

        YuanyingSeed seed = ResolveEscapeSeed(actorExtend);
        if (!seed.IsValid) return;
        Subspecies soulSubspecies = World.world.subspecies.getNearbySpecies(
            Actors.YuanyingSoul, actor.current_tile, out _, false)
            ?? World.world.subspecies.newSpecies(Actors.YuanyingSoul, actor.current_tile);
        if (soulSubspecies == null || soulSubspecies.isRekt()) return;

        try
        {
            actor.cancelAllBeh();
            actor.beh_actor_target = null;
            actor.clearAttackTarget();
            actor.clearTileTarget();
            ReleaseMortalBody(actor);
            RemoveBodyTraits(actor);
            if (actorExtend.HasElementRoot()) actorExtend.E.RemoveComponent<ElementRoot>();

            actor.setAsset(Actors.YuanyingSoul);
            actor.setSubspecies(soulSubspecies);
            actor.data.head = -1;
            actor.setFlying(true);
            actor.setShowShadow(Actors.YuanyingSoul.shadow);
            actor.clearGraphicsFully();

            double now = World.world.getCurWorldTime();
            actorExtend.AddComponent(new YuanyingSoulState
            {
                expires_at = now + YuanyingPossessionRules.SoulLifetime,
                channel_started_at = 0d,
                target_actor_id = -1L
            });
            actor.addStatusEffect(StatusEffects.YuanyingEscape.id,
                YuanyingPossessionRules.SoulLifetime, pColorEffect: false);
            actorExtend.MarkCultiwayStatsDirty(false);
            CoreFormationEffectResolver.Synchronize(actorExtend);
            actor.setStatsDirty();
            actor.updateStats();
            actor.setHealth(Mathf.Max(1, Mathf.RoundToInt(actor.getMaxHealth() * 0.5f)));
            actor.city?.setCitizensDirty();
            WorldLogUtils.LogYuanyingEscape(actor);
            damage = 0f;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"元婴出逃失败: actor={actor.data.id}\n{exception}");
        }
    }

    private static YuanyingSeed ResolveEscapeSeed(ActorExtend actor)
    {
        if (actor.TryGetComponent(out YuanyingSeed seed) && seed.IsValid) return seed.DeepClone();
        if (actor.TryGetComponent(out Yuanying active) && active.formation.IsFinalized)
            return YuanyingSeed.FromYuanying(active, actor.GetPowerLevel());
        return default;
    }

    private static void ClearTarget(Actor source, ref YuanyingSoulState state)
    {
        state.target_actor_id = -1L;
        state.channel_started_at = 0d;
        source.beh_actor_target = null;
        source.clearTileTarget();
    }

    private static void OnProgressionCommitted(ProgressionCommittedEvent evt)
    {
        if (evt.TransitionId != FormYuanyingTransition) return;
        long actorId = evt.Actor.Base.data.id;
        if (!PendingPowerRestores.TryGetValue(actorId, out float powerLevel)) return;
        PendingPowerRestores.Remove(actorId);
        evt.Actor.SetPowerLevel(powerLevel);
    }

    private static bool TryCaptureHost(Actor target, out HostBodySnapshot snapshot)
    {
        snapshot = null;
        if (!IsEligibleHost(target)) return false;
        if (target.asset == null || target.subspecies == null || target.data == null) return false;

        ActorExtend targetExtend = target.GetExtend();
        var rootValues = new float[ElementIndex.Count];
        ref ElementRoot root = ref targetExtend.GetElementRoot();
        for (var i = 0; i < rootValues.Length; i++) rootValues[i] = root[i];

        string[] bodyTraits = target.traits
            .Where(trait => trait != null && BodyTraitGroups.Contains(trait.group_id))
            .Select(trait => trait.id)
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        snapshot = new HostBodySnapshot
        {
            ActorId = target.data.id,
            BodyAssetId = target.asset.id,
            SubspeciesId = target.subspecies.data.id,
            Sex = target.data.sex,
            Head = target.data.head,
            PhenotypeIndex = target.data.phenotype_index,
            PhenotypeShade = target.data.phenotype_shade,
            Age = target.data.getAge(),
            BodyTraitIds = bodyTraits,
            ElementRootValues = rootValues,
            Cultivation = HostCultivationSnapshot.Capture(targetExtend)
        };
        return true;
    }

    private static bool TryCommitPossession(
        Actor source,
        Actor target,
        HostBodySnapshot snapshot,
        float compatibility)
    {
        if (source == null || target == null || snapshot == null) return false;
        if (source.data.id == target.data.id || target.data.id != snapshot.ActorId) return false;
        if (!IsEscapedSoul(source) || !IsEligibleHost(target)) return false;

        ActorAsset bodyAsset = AssetManager.actor_library.get(snapshot.BodyAssetId);
        Subspecies bodySubspecies = World.world.subspecies.get(snapshot.SubspeciesId);
        if (bodyAsset == null || bodySubspecies == null || bodySubspecies.isRekt()) return false;

        ActorExtend sourceExtend = source.GetExtend();
        try
        {
            YuanyingSeed seed = ResolveEscapeSeed(sourceExtend);
            float strengthLoss = Mathf.Lerp(
                YuanyingPossessionRules.MaximumSeedStrengthLoss,
                YuanyingPossessionRules.MinimumSeedStrengthLoss,
                Mathf.Clamp01(compatibility));
            seed.strength *= 1f - strengthLoss;

            ApplyHostCultivation(sourceExtend, snapshot.Cultivation);
            ReplaceBodyTraits(source, snapshot.BodyTraitIds);
            ReplaceElementRoot(sourceExtend, snapshot.ElementRootValues);
            AdoptBody(source, bodyAsset, bodySubspecies, snapshot);
            sourceExtend.GetOrAddComponent<YuanyingSeed>() = seed;

            source.setStatsDirty();
            source.updateStats();
            ref Xian xian = ref sourceExtend.GetCultisys<Xian>();
            xian.wakan = Mathf.Max(0f, source.stats[BaseStatses.MaxWakan.id]
                                      * YuanyingPossessionRules.NewBodyWakanRatio);
            source.setHealth(Mathf.Max(1, Mathf.RoundToInt(source.getMaxHealth()
                                                           * YuanyingPossessionRules.NewBodyHealthRatio)));

            source.addStatusEffect(StatusEffects.BodyDisharmony.id,
                YuanyingPossessionRules.BodyDisharmonyDuration, pColorEffect: false);
            source.finishStatusEffect(StatusEffects.YuanyingEscape.id);
            source.beh_actor_target = null;
            source.clearAttackTarget();
            source.clearTileTarget();
            sourceExtend.E.RemoveComponent<YuanyingSoulState>();

            sourceExtend.MarkCultiwayStatsDirty(false);
            sourceExtend.MarkCultiwaySkillCacheDirty(false);
            CoreFormationEffectResolver.Synchronize(sourceExtend);
            source.setStatsDirty();
            source.updateStats();
            source.city?.setCitizensDirty();

            try
            {
                KillHost(target, source);
            }
            catch (Exception exception)
            {
                ModClass.LogError($"夺舍已经完成，但宿主死亡结算失败，改用直接销毁: target={target.data.id}\n{exception}");
                if (!target.isRekt()) target.dieAndDestroy(AttackType.Metamorphosis);
            }
            return true;
        }
        catch (Exception exception)
        {
            ModClass.LogError($"元婴夺舍提交失败: source={source.data.id}, target={target.data.id}\n{exception}");
            return false;
        }
    }

    private static void ReleaseMortalBody(Actor actor)
    {
        if (actor?.equipment == null || !actor.hasEquipment()) return;
        List<Item> items = actor.equipment.getItems().ToList();
        actor.equipment.destroyAllEquipment();
        if (actor.current_tile?.zone?.hasCity() == true)
            actor.current_tile.zone.city.tryToPutItems(items);
    }

    private static void RemoveBodyTraits(Actor actor)
    {
        if (actor == null) return;
        ActorTrait[] traits = actor.traits
            .Where(trait => trait != null && BodyTraitGroups.Contains(trait.group_id))
            .ToArray();
        actor.removeTraits(traits);
    }

    private static void ReplaceBodyTraits(Actor source, IReadOnlyList<string> traitIds)
    {
        RemoveBodyTraits(source);
        for (var i = 0; i < traitIds.Count; i++)
        {
            ActorTrait trait = AssetManager.traits.get(traitIds[i]);
            if (trait != null) source.addTrait(trait, true);
        }
    }

    private static void ApplyHostCultivation(ActorExtend source, HostCultivationSnapshot host)
    {
        ref Xian active = ref source.GetOrAddComponent<Xian>();
        active = host.HasXian
            ? host.Xian
            : new Xian { level = XianLevels.QiRefinement, wakan = 0f };

        ref QiRefinementState qi = ref source.GetOrAddComponent<QiRefinementState>();
        qi = host.HasQiRefinement ? host.QiRefinement.DeepClone() : default;

        if (host.HasFoundation)
            source.GetOrAddComponent<XianBase>() = host.Foundation.DeepClone();
        else if (source.HasComponent<XianBase>())
            source.E.RemoveComponent<XianBase>();

        if (host.HasJindan)
            source.GetOrAddComponent<Jindan>() = host.Jindan.DeepClone();
        else if (source.HasComponent<Jindan>())
            source.E.RemoveComponent<Jindan>();

        if (source.HasComponent<Yuanying>()) source.E.RemoveComponent<Yuanying>();
        source.GetOrAddComponent<CultivationResourceState>() = host.HasResources ? host.Resources : default;
        source.SetPowerLevel(host.PowerLevel);
        source.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(source);
    }

    private static void ReplaceElementRoot(ActorExtend source, IReadOnlyList<float> values)
    {
        var rootValues = new float[ElementIndex.Count];
        for (var i = 0; i < rootValues.Length; i++) rootValues[i] = values[i];
        source.GetOrAddComponent<ElementRoot>() = new ElementRoot(rootValues);
    }

    private static void AdoptBody(
        Actor source,
        ActorAsset bodyAsset,
        Subspecies bodySubspecies,
        HostBodySnapshot snapshot)
    {
        source.setAsset(bodyAsset);
        source.setSubspecies(bodySubspecies);
        source.data.sex = snapshot.Sex;
        source.data.head = snapshot.Head;
        source.data.phenotype_index = snapshot.PhenotypeIndex;
        source.data.phenotype_shade = snapshot.PhenotypeShade;
        source.data.age_overgrowth = snapshot.Age - Date.getYearsSince(source.data.created_time);
        source.setFlying(bodyAsset.flying);
        source.setShowShadow(bodyAsset.shadow);
        source.clearGraphicsFully();
        source.setStatsDirty();
    }

    private static void KillHost(Actor host, Actor source)
    {
        float lethalDamage = Mathf.Max(host.data.health, host.getMaxHealth()) + 1f;
        PatchActor.getHit_snapshot(
            host,
            lethalDamage,
            pFlash: false,
            pAttackType: AttackType.Other,
            pAttacker: source,
            pSkipIfShake: false,
            pMetallicWeapon: false,
            pCheckDamageReduction: false);
        if (host.isAlive() && !host.hasHealth()) host.checkDeath();
        if (host.isAlive())
        {
            host.setHealth(0);
            host.checkDeath();
        }
        if (host.isAlive()) host.dieAndDestroy(AttackType.Other);
    }

    private sealed class HostBodySnapshot
    {
        internal long ActorId;
        internal string BodyAssetId;
        internal long SubspeciesId;
        internal ActorSex Sex;
        internal int Head;
        internal int PhenotypeIndex;
        internal int PhenotypeShade;
        internal int Age;
        internal string[] BodyTraitIds;
        internal float[] ElementRootValues;
        internal HostCultivationSnapshot Cultivation;
    }

    private struct HostCultivationSnapshot
    {
        internal bool HasXian;
        internal Xian Xian;
        internal bool HasQiRefinement;
        internal QiRefinementState QiRefinement;
        internal bool HasFoundation;
        internal XianBase Foundation;
        internal bool HasJindan;
        internal Jindan Jindan;
        internal bool HasResources;
        internal CultivationResourceState Resources;
        internal float PowerLevel;

        internal static HostCultivationSnapshot Capture(ActorExtend actor)
        {
            bool hasQi = actor.TryGetComponent(out QiRefinementState qi);
            bool hasFoundation = actor.TryGetComponent(out XianBase foundation);
            bool hasJindan = actor.TryGetComponent(out Jindan jindan);
            return new HostCultivationSnapshot
            {
                HasXian = actor.TryGetComponent(out Xian xian),
                Xian = xian,
                HasQiRefinement = hasQi,
                QiRefinement = hasQi ? qi.DeepClone() : default,
                HasFoundation = hasFoundation,
                Foundation = hasFoundation ? foundation.DeepClone() : default,
                HasJindan = hasJindan,
                Jindan = hasJindan ? jindan.DeepClone() : default,
                HasResources = actor.TryGetComponent(out CultivationResourceState resources),
                Resources = resources,
                PowerLevel = actor.GetPowerLevel()
            };
        }
    }
}

/// <summary>一次神魂对抗的可观察结果。</summary>
public readonly struct SoulContestResult
{
    public SoulContestResult(float attackScore, float defenseScore, float compatibility, float successChance)
    {
        AttackScore = attackScore;
        DefenseScore = defenseScore;
        Compatibility = compatibility;
        SuccessChance = successChance;
    }

    public float AttackScore { get; }
    public float DefenseScore { get; }
    public float Compatibility { get; }
    public float SuccessChance { get; }
}

/// <summary>只计算神魂对抗数值，不修改双方状态。</summary>
public static class SoulContestResolver
{
    public static SoulContestResult Resolve(ActorExtend source, ActorExtend target)
    {
        YuanyingSeed seed = source.TryGetComponent(out YuanyingSeed dormant)
            ? dormant
            : YuanyingSeed.FromYuanying(source.GetComponent<Yuanying>(), source.GetPowerLevel());
        float compatibility = CalculateCompatibility(seed.formation, target.GetElementRoot());
        float qualityFactor = 1f + Mathf.Max(0, seed.formation.quality.Stage) * 0.2f;
        float divineSense = Mathf.Max(0f, source.Base.stats[WorldboxGame.BaseStats.DivineSense.id]);
        float maxSoul = Mathf.Max(0f, source.Base.stats[WorldboxGame.BaseStats.MaxSoul.id]);
        float attack = Mathf.Max(0.1f, seed.strength) * qualityFactor
                       * (1f + divineSense * 0.01f + maxSoul * 0.005f);

        float targetDivineSense = Mathf.Max(0f, target.Base.stats[WorldboxGame.BaseStats.DivineSense.id]);
        float targetMaxSoul = Mathf.Max(0f, target.Base.stats[WorldboxGame.BaseStats.MaxSoul.id]);
        float defense = 1f + target.GetPowerLevel() * 0.5f
                        + targetDivineSense * 0.01f + targetMaxSoul * 0.005f;
        if (target.Base.hasTag("strong_mind")) defense *= 1.5f;

        float ratio = attack / Mathf.Max(0.1f, attack + defense);
        float chance = Mathf.Clamp(ratio * (0.65f + compatibility * 0.7f),
            YuanyingPossessionRules.MinimumSuccessChance,
            YuanyingPossessionRules.MaximumSuccessChance);
        return new SoulContestResult(attack, defense, compatibility, chance);
    }

    internal static float CalculateCompatibility(CoreFormationSnapshot formation, in ElementRoot root)
    {
        if (!formation.IsValid) return 0f;
        ElementComposition composition = formation.composition;
        float dot = 0f;
        float formationNorm = 0f;
        float rootNorm = 0f;
        for (var i = 0; i < ElementIndex.Count; i++)
        {
            float left = Mathf.Max(0f, composition[i]);
            float right = Mathf.Max(0f, root[i]);
            dot += left * right;
            formationNorm += left * left;
            rootNorm += right * right;
        }
        float denominator = Mathf.Sqrt(formationNorm * rootNorm);
        return denominator > 0f ? Mathf.Clamp01(dot / denominator) : 0f;
    }
}
