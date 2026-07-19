using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Utils.Extension;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 负责角色法器配置的自动选择和运行调度。
/// 自动选择决定应建立哪些装备关系，运行调度决定已装备法器当前处于何种控制状态。
/// </summary>
public static class ArtifactLoadoutPlanner
{
    /// <summary>
    /// 自动装备时为准备负荷预留的神识比例，避免自动选择占满全部容量。
    /// </summary>
    private const float AutomaticPreparedCapacityRatio = 0.8f;

    /// <summary>
    /// 自动模式允许用于运转法器的神识比例。
    /// </summary>
    private const float AutomaticOperatingCapacityRatio = 0.8f;

    /// <summary>
    /// 强制运转模式允许达到的神识负荷比例，超过完整容量的部分会形成超载。
    /// </summary>
    private const float ForcedOperatingCapacityRatio = 1.3f;

    /// <summary>
    /// 刷新角色的整套法器配置和运行状态。
    /// </summary>
    /// <param name="actor">需要调度法器的角色扩展。</param>
    /// <param name="manageAutomaticEquipment">是否允许本轮自动建立或移除装备关系。</param>
    /// <param name="elapsedSeconds">距上次周期调度经过的秒数，用于推进祭炼熟练度。</param>
    public static void Refresh(ActorExtend actor, bool manageAutomaticEquipment, float elapsedSeconds)
    {
        float capacity = actor.Base.stats[nameof(WorldboxGame.BaseStats.DivineSense)];
        bool inCombat = IsCombatContext(actor.Base);
        Dictionary<int, EquippedArtifactRelation> existing = ReadRelations(actor.E);
        bool semanticProfileChanged = RemoveUnavailableRelations(actor.E, existing);
        List<Entity> inventory = CollectArtifacts(actor);
        List<Candidate> candidates = BuildCandidates(actor, inventory, existing, inCombat);

        // 周期调度会重新规划自动装备；手动操作后的即时刷新只重排现有装备。
        if (manageAutomaticEquipment)
        {
            HashSet<int> desired = ManageAutomaticEquipment(
                actor, candidates, existing, capacity, out bool equipmentChanged);
            semanticProfileChanged |= equipmentChanged;
            candidates.RemoveAll(candidate => !desired.Contains(candidate.Item.Id));
        }
        else
        {
            candidates.RemoveAll(candidate => !candidate.HasRelation);
        }

        semanticProfileChanged |= Schedule(actor, candidates, capacity, inCombat, elapsedSeconds);
        if (semanticProfileChanged) actor.MarkSemanticProfileDirty();
    }

    /// <summary>
    /// 从角色库存中收集已经完成且当前可用的法器。
    /// </summary>
    private static List<Entity> CollectArtifacts(ActorExtend actor)
    {
        List<Entity> result = new();
        foreach (Entity item in actor.GetItems())
        {
            if (item.IsAvailable() && item.HasComponent<Artifact>()) result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// 清除因未完成、占用、消耗或回收而失效的装备关系。
    /// 库存转移导致的关系删除由 ArtifactEquipmentManager 保证，不在此重复处理。
    /// </summary>
    private static bool RemoveUnavailableRelations(
        Entity owner,
        Dictionary<int, EquippedArtifactRelation> existing)
    {
        List<Entity> unavailable = new();
        foreach (EquippedArtifactRelation relation in existing.Values)
        {
            if (!relation.artifact.IsAvailable()) unavailable.Add(relation.artifact);
        }
        for (int i = 0; i < unavailable.Count; i++)
        {
            owner.RemoveRelation<EquippedArtifactRelation>(unavailable[i]);
            existing.Remove(unavailable[i].Id);
        }
        return unavailable.Count > 0;
    }

    /// <summary>
    /// 为库存中的可用法器一次性计算本轮选择与调度共用的候选数据。
    /// </summary>
    private static List<Candidate> BuildCandidates(
        ActorExtend actor,
        List<Entity> inventory,
        Dictionary<int, EquippedArtifactRelation> existing,
        bool inCombat)
    {
        long ownerId = actor.Base.data.id;
        List<Candidate> candidates = new(inventory.Count);
        for (int i = 0; i < inventory.Count; i++)
        {
            Entity item = inventory[i];
            bool hasRelation = existing.TryGetValue(item.Id, out EquippedArtifactRelation relation);
            if (!hasRelation && ArtifactControlRules.IsAutoEquipDisabled(item, ownerId)) continue;
            candidates.Add(BuildCandidate(actor, item, hasRelation, relation, inCombat));
        }
        return candidates;
    }

    /// <summary>
    /// 根据角色场景、法器用途和准备负荷，调整自动装备关系。
    /// 锁定装备和非自动模式装备被视为人工选择，不会被该过程移除。
    /// </summary>
    /// <returns>同步关系后最终应保持装备的法器实体 ID。</returns>
    private static HashSet<int> ManageAutomaticEquipment(
        ActorExtend actor,
        List<Candidate> candidates,
        Dictionary<int, EquippedArtifactRelation> existing,
        float capacity,
        out bool changed)
    {
        changed = false;
        long ownerId = actor.Base.data.id;
        candidates.Sort(CompareSelectionCandidates);

        // 人工锁定或指定为非自动模式的法器必须保留，并先占用准备负荷。
        HashSet<int> desired = new();
        float usedPreparedLoad = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (!candidate.Locked && candidate.Mode == ArtifactEquipMode.Automatic) continue;
            desired.Add(candidate.Item.Id);
            usedPreparedLoad += candidate.PreparedLoad;
        }
        // 自动选择只使用部分神识容量，并按当前场景下的候选评分依次填充。
        float targetPreparedLoad = capacity * AutomaticPreparedCapacityRatio;
        for (int i = 0; i < candidates.Count; i++)
        {
            TrySelect(candidates[i], desired, ref usedPreparedLoad, targetPreparedLoad, capacity);
        }

        // 移除不再属于本轮选择结果的自动装备关系。
        List<Entity> toRemove = new();
        foreach (EquippedArtifactRelation relation in existing.Values)
        {
            if (relation.locked || relation.mode != ArtifactEquipMode.Automatic) continue;
            if (!desired.Contains(relation.artifact.Id)) toRemove.Add(relation.artifact);
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            actor.E.RemoveRelation<EquippedArtifactRelation>(toRemove[i]);
            existing.Remove(toRemove[i].Id);
            changed = true;
        }

        // 为本轮新选中的法器建立装备关系，并将其祭炼归属绑定到当前角色。
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (!desired.Contains(candidate.Item.Id) || existing.ContainsKey(candidate.Item.Id)) continue;
            ArtifactControlRules.BindAttunement(candidate.Item, ownerId);
            actor.E.AddRelation(new EquippedArtifactRelation
            {
                artifact = candidate.Item,
                mode = ArtifactEquipMode.Automatic,
            });
            changed = true;
        }
        return desired;
    }

    /// <summary>
    /// 在神识负荷和分念限制内，为已装备法器分配冷待命、准备、运转或超载状态。
    /// </summary>
    private static bool Schedule(
        ActorExtend actor,
        List<Candidate> candidates,
        float capacity,
        bool inCombat,
        float elapsedSeconds)
    {
        bool attunementChanged = false;
        candidates.Sort(CompareRuntimeCandidates);

        // 每轮从冷待命开始重新分配，后续阶段逐步提升满足条件的法器状态。
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            ref EquippedArtifactRelation relation = ref actor.E
                .GetRelation<EquippedArtifactRelation, Entity>(candidate.Item);
            relation.state = ArtifactControlState.Cold;
        }

        // 分念限制控制同时运转的法器数量，神识负荷限制同时维持的总复杂度。
        int threadCapacity = ArtifactControlRules.GetThreadCapacity(capacity);
        int usedThreads = 0;
        float usedPreparedLoad = 0f;
        float usedOperatingLoad = 0f;
        float usedTotalLoad = 0f;
        HashSet<int> operating = new();

        // 强制运转法器最先分配资源，并允许在强制上限内突破完整神识容量。
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (candidate.Mode != ArtifactEquipMode.ForcedOperating) continue;
            if (usedThreads + candidate.ThreadCost > threadCapacity) continue;
            float nextLoad = usedTotalLoad + candidate.OperatingLoad;
            if (capacity <= 0f || nextLoad > capacity * ForcedOperatingCapacityRatio) continue;

            ref EquippedArtifactRelation relation = ref actor.E
                .GetRelation<EquippedArtifactRelation, Entity>(candidate.Item);
            relation.state = ArtifactControlState.Operating;
            operating.Add(candidate.Item.Id);
            usedOperatingLoad += candidate.OperatingLoad;
            usedTotalLoad = nextLoad;
            usedThreads += candidate.ThreadCost;
        }

        // 自动法器仅在用途适合当前场景时运转，通常保留一部分神识余量。
        float automaticLimit = capacity * AutomaticOperatingCapacityRatio;
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (operating.Contains(candidate.Item.Id)) continue;
            if (candidate.Mode != ArtifactEquipMode.Automatic || !ShouldOperate(candidate.UseProfile, inCombat)) continue;
            if (usedThreads + candidate.ThreadCost > threadCapacity) continue;

            float nextLoad = usedTotalLoad + candidate.OperatingLoad;
            bool firstOperatingArtifact = operating.Count == 0;
            if (nextLoad > automaticLimit && !(firstOperatingArtifact && nextLoad <= capacity)) continue;

            ref EquippedArtifactRelation relation = ref actor.E
                .GetRelation<EquippedArtifactRelation, Entity>(candidate.Item);
            relation.state = ArtifactControlState.Operating;
            operating.Add(candidate.Item.Id);
            usedOperatingLoad += candidate.OperatingLoad;
            usedTotalLoad = nextLoad;
            usedThreads += candidate.ThreadCost;
        }

        // 未运转法器按优先级尝试进入准备状态；准备状态占用较少负荷，不消耗分念。
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            if (operating.Contains(candidate.Item.Id)) continue;
            float nextLoad = usedTotalLoad + candidate.PreparedLoad;
            if (nextLoad > capacity) continue;

            ref EquippedArtifactRelation relation = ref actor.E
                .GetRelation<EquippedArtifactRelation, Entity>(candidate.Item);
            relation.state = ArtifactControlState.Ready;
            usedPreparedLoad += candidate.PreparedLoad;
            usedTotalLoad = nextLoad;
        }

        // 强制运转可能令总负荷超过容量，此时所有正在运转的法器统一标记为超载。
        if (usedTotalLoad > capacity)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                if (!operating.Contains(candidate.Item.Id)) continue;
                ref EquippedArtifactRelation relation = ref actor.E
                    .GetRelation<EquippedArtifactRelation, Entity>(candidate.Item);
                relation.state = ArtifactControlState.Overloaded;
            }
        }

        // 周期调度根据本轮状态推进每件装备法器的祭炼熟练度。
        if (elapsedSeconds > 0f)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];
                ref EquippedArtifactRelation relation = ref actor.E
                    .GetRelation<EquippedArtifactRelation, Entity>(candidate.Item);
                attunementChanged |= ArtifactControlRules.AdvanceAttunement(
                    candidate.Item, relation.state, elapsedSeconds);
            }
        }

        // 保存本轮汇总结果，供角色页面和 Tooltip 直接展示。
        ref ArtifactLoadoutState state = ref actor.E.GetComponent<ArtifactLoadoutState>();
        state.prepared_load = usedPreparedLoad;
        state.operating_load = usedOperatingLoad;
        state.used_threads = usedThreads;
        return attunementChanged;
    }

    /// <summary>
    /// 将角色当前的装备关系按法器实体 ID 建立索引，供自动选择阶段快速比较现状。
    /// </summary>
    private static Dictionary<int, EquippedArtifactRelation> ReadRelations(Entity owner)
    {
        Dictionary<int, EquippedArtifactRelation> result = new();
        var relations = owner.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity item = relations[i].artifact;
            result[item.Id] = relations[i];
        }
        return result;
    }

    /// <summary>
    /// 汇总一件法器参与选择和调度所需的派生数据。
    /// </summary>
    /// <param name="actor">候选法器的当前持有者。</param>
    /// <param name="item">候选法器实体。</param>
    /// <param name="hasRelation">该法器当前是否已经装备。</param>
    /// <param name="relation">已有装备关系；未装备时使用默认值。</param>
    /// <param name="inCombat">角色本轮是否处于战斗场景。</param>
    private static Candidate BuildCandidate(
        ActorExtend actor,
        Entity item,
        bool hasRelation,
        EquippedArtifactRelation relation,
        bool inCombat)
    {
        long ownerId = actor.Base.data.id;
        ArtifactControlRules.ResolveLoads(
            item,
            ownerId,
            out float preparedLoad,
            out float operatingLoad,
            out int threadCost);
        ArtifactUseProfile useProfile = ArtifactAbilityDispatcher.ResolveUseProfile(item);
        float score = CalculateUtility(actor, item, useProfile, inCombat);

        // relation 上的人工配置只影响排序，不改变法器自身的基础价值。
        if (hasRelation)
        {
            score += relation.priority;
            if (relation.locked) score += 500f;
            if (relation.mode == ArtifactEquipMode.ForcedOperating) score += 10000f;
        }

        return new Candidate
        {
            Item = item,
            UseProfile = useProfile,
            Score = score,
            PreparedLoad = preparedLoad,
            OperatingLoad = operatingLoad,
            ThreadCost = threadCost,
            HasRelation = hasRelation,
            Locked = hasRelation && relation.locked,
            Mode = hasRelation ? relation.mode : ArtifactEquipMode.Automatic,
        };
    }

    /// <summary>
    /// 计算法器在角色当前场景中的选择价值。
    /// 品阶决定基础强度，用途决定场景适配，祭炼和宗门借用提供附加权重。
    /// </summary>
    private static float CalculateUtility(
        ActorExtend actor,
        Entity item,
        ArtifactUseProfile useProfile,
        bool inCombat)
    {
        ItemLevel itemLevel = item.GetComponent<ItemLevel>();
        float purposeMultiplier = GetContextUtility(useProfile, inCombat);
        float itemPower = 10f * Mathf.Pow(2f, itemLevel.Stage) * (1f + itemLevel.Level / 9f);

        float masteryMultiplier = 1f;
        if (item.TryGetComponent(out ArtifactAttunement attunement) &&
            attunement.owner_actor_id == actor.Base.data.id)
        {
            masteryMultiplier += attunement.mastery / 100f * 0.3f;
        }
        if (item.HasComponent<SectTreasureLoan>()) purposeMultiplier += 0.15f;
        return itemPower * purposeMultiplier * masteryMultiplier;
    }

    /// <summary>
    /// 尝试把候选法器加入自动装备结果，并累计其准备负荷。
    /// 第一件自动法器可以使用完整容量，后续法器只能使用自动准备目标容量。
    /// </summary>
    private static bool TrySelect(
        Candidate candidate,
        HashSet<int> desired,
        ref float usedPreparedLoad,
        float targetPreparedLoad,
        float capacity)
    {
        if (desired.Contains(candidate.Item.Id)) return true;
        float nextLoad = usedPreparedLoad + candidate.PreparedLoad;
        bool fitsTarget = nextLoad <= targetPreparedLoad;
        bool fitsAsOnlyChoice = desired.Count == 0 && nextLoad <= capacity;
        if (!fitsTarget && !fitsAsOnlyChoice) return false;

        desired.Add(candidate.Item.Id);
        usedPreparedLoad = nextLoad;
        return true;
    }

    /// <summary>
    /// 判断法器用途是否值得在当前战斗或非战斗场景中自动运转。
    /// </summary>
    private static bool ShouldOperate(ArtifactUseProfile profile, bool inCombat)
    {
        float relevant = inCombat
            ? profile.offensive + profile.defensive + profile.support * 0.75f
            : profile.cultivate + profile.production + profile.support * 0.75f;
        return relevant >= 0.5f;
    }

    /// <summary>
    /// 将用途权重转换为当前场景下的综合价值倍率。
    /// </summary>
    private static float GetContextUtility(ArtifactUseProfile profile, bool inCombat)
    {
        if (inCombat)
        {
            return profile.offensive * 1.5f + profile.defensive * 1.6f + profile.support * 1.2f +
                   profile.cultivate * 0.35f + profile.production * 0.2f;
        }

        return profile.offensive * 0.75f + profile.defensive * 0.9f + profile.support * 1.25f +
               profile.cultivate * 1.7f + profile.production * 1.5f;
    }

    /// <summary>
    /// 判断角色当前是否处于需要战斗用途法器的场景。
    /// </summary>
    private static bool IsCombatContext(Actor actor)
    {
        return actor.has_attack_target || actor.isJustAttacked();
    }

    /// <summary>
    /// 自动装备排序：锁定优先，其次比较单位准备负荷提供的价值，再比较总价值。
    /// </summary>
    private static int CompareSelectionCandidates(Candidate left, Candidate right)
    {
        if (left.Locked != right.Locked) return left.Locked ? -1 : 1;
        float leftEfficiency = left.Score / left.PreparedLoad;
        float rightEfficiency = right.Score / right.PreparedLoad;
        int score = rightEfficiency.CompareTo(leftEfficiency);
        if (score != 0) return score;
        score = right.Score.CompareTo(left.Score);
        return score != 0 ? score : left.Item.Id.CompareTo(right.Item.Id);
    }

    /// <summary>
    /// 运行调度排序：强制运转优先，其次锁定装备优先，最后按综合价值排序。
    /// </summary>
    private static int CompareRuntimeCandidates(Candidate left, Candidate right)
    {
        bool leftForced = left.Mode == ArtifactEquipMode.ForcedOperating;
        bool rightForced = right.Mode == ArtifactEquipMode.ForcedOperating;
        if (leftForced != rightForced) return leftForced ? -1 : 1;
        if (left.Locked != right.Locked) return left.Locked ? -1 : 1;
        int score = right.Score.CompareTo(left.Score);
        return score != 0 ? score : left.Item.Id.CompareTo(right.Item.Id);
    }

    /// <summary>
    /// 单轮规划使用的临时法器数据，不写入 ECS。
    /// </summary>
    private struct Candidate
    {
        /// <summary>法器实体。</summary>
        public Entity Item;

        /// <summary>法器在不同场景中的用途权重。</summary>
        public ArtifactUseProfile UseProfile;

        /// <summary>结合品阶、用途和祭炼程度计算出的综合价值。</summary>
        public float Score;

        /// <summary>进入准备状态所需的神识负荷。</summary>
        public float PreparedLoad;

        /// <summary>进入运转状态所需的神识负荷。</summary>
        public float OperatingLoad;

        /// <summary>运转时占用的分念数量。</summary>
        public int ThreadCost;

        /// <summary>本轮开始时是否已经存在装备关系。</summary>
        public bool HasRelation;

        /// <summary>是否为人工锁定装备。</summary>
        public bool Locked;

        /// <summary>装备关系指定的调度模式。</summary>
        public ArtifactEquipMode Mode;
    }
}
