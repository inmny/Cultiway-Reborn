using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Combat;
using Cultiway.Core;
using Cultiway.Core.Semantics;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     尸体精华的独占领取与有上限消化队列。
///     同一次死亡只能提供一份精华；队列已满时妖兽不能领取新的精华。
/// </summary>
public static class YaoDigestionService
{
    private const float DepositLifetime = 60f;

    /// <summary>首批精华碎片：碎片编号、必须来自的真实特征语义与可以提出的器官。</summary>
    private static readonly (string fragmentId, string semanticId, string organId, int rank)[] fragments =
    {
        ("yao.frag.aquatic", "semantic.element.water", "yao.lung.aquatic", 1),
        ("yao.frag.venom", "semantic.element.poison", "yao.venom.gland.enhanced", 2),
        ("yao.frag.hard_hide", "semantic.element.earth", "yao.scale.fine", 2),
        ("yao.frag.regen", "semantic.element.wood", "yao.regen.low", 1),
    };

    private static readonly Dictionary<long, YaoEssenceDeposit> deposits = new();
    private static readonly Dictionary<long, int> deathSequences = new();
    private static int nextDeathSequence;
    private static bool initialized;

    /// <summary>一份可以被领取的尸体精华。</summary>
    public struct YaoEssenceDeposit
    {
        /// <summary>来源单位编号。</summary>
        public long SourceActorId;

        /// <summary>来源死亡序号。</summary>
        public int SourceDeathSequence;

        /// <summary>精华位置。</summary>
        public Vector2 Position;

        /// <summary>到期世界秒。</summary>
        public float ExpireAt;

        /// <summary>精华碎片编号；创建时按特征匹配确定。</summary>
        public string FragmentId;

        /// <summary>是否已经被领取。</summary>
        public bool Claimed;

        /// <summary>预估样本强度。</summary>
        public float Strength;
    }

    /// <summary>注册死亡掉落钩子；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        ActorExtend.RegisterActionOnDeath(self => CreateDeposit(self));
    }

    /// <summary>吞天胃直接吞噬：击杀当场领取，不依赖尸体位置。</summary>
    public static void TryClaimKillDirectly(ActorExtend predator, Actor victim)
    {
        if (victim == null || victim.isRekt()) return;
        TryEnqueueFromVictim(predator, victim, 1f);
    }

    /// <summary>按死亡事件创建原地精华痕迹；特征不匹配任何碎片时不产生。</summary>
    public static void CreateDeposit(ActorExtend deadActor)
    {
        Actor actor = deadActor.Base;
        if (actor == null || actor.isRekt()) return;
        long actorId = actor.data.id;
        int sequence = ++nextDeathSequence;
        deathSequences[actorId] = sequence;

        string fragmentId = MatchFragment(deadActor);
        if (fragmentId == null) return;

        deposits[actorId] = new YaoEssenceDeposit
        {
            SourceActorId = actorId,
            SourceDeathSequence = sequence,
            Position = new Vector2(actor.current_position.x, actor.current_position.y),
            ExpireAt = YaoTime.Now + DepositLifetime,
            FragmentId = fragmentId,
            Strength = 1f,
        };
    }

    /// <summary>读取某位置的可用精华；供吞食工作寻路。</summary>
    public static bool TryFindDeposit(Vector2 position, float maxDistance, out YaoEssenceDeposit deposit)
    {
        deposit = default;
        float best = maxDistance * maxDistance;
        foreach (YaoEssenceDeposit candidate in deposits.Values)
        {
            if (candidate.Claimed || YaoTime.Now > candidate.ExpireAt) continue;
            float sqr = (candidate.Position - position).sqrMagnitude;
            if (sqr > best) continue;
            best = sqr;
            deposit = candidate;
        }

        return !string.IsNullOrEmpty(deposit.FragmentId);
    }

    /// <summary>按来源键领取精华并进入消化队列；同一次死亡只能领取一次。</summary>
    public static bool TryClaim(ActorExtend predator, long sourceActorId, int sourceDeathSequence)
    {
        if (!deposits.TryGetValue(sourceActorId, out YaoEssenceDeposit deposit)) return false;
        if (deposit.Claimed || deposit.SourceDeathSequence != sourceDeathSequence) return false;
        if (YaoTime.Now > deposit.ExpireAt) return false;
        return TryEnqueue(predator, deposit, deposit.FragmentId);
    }

    /// <summary>把一份精华压入消化队列；队列已满返回假。</summary>
    private static bool TryEnqueue(ActorExtend predator, YaoEssenceDeposit deposit, string fragmentId)
    {
        if (!predator.HasCultisys<Yao>()) return false;
        ref Yao yao = ref predator.GetCultisys<Yao>();

        // 领取前先锁定样本并支付最低妖力成本。
        float cost = 2f;
        if (!YaoResourceService.TrySpend(predator, cost)) return false;

        if (!predator.E.TryGetComponent(out YaoDigestion digestion))
        {
            digestion = new YaoDigestion();
            predator.E.AddComponent(digestion);
        }

        digestion.EnsureInitialized();
        int freeSlot = FindFreeSlot(digestion);
        if (freeSlot < 0)
        {
            // 队列已满：妖兽不会吞走新的精华，退还成本。
            YaoResourceService.Gain(predator, ref yao, cost);
            return false;
        }

        digestion.Queue[freeSlot] = new YaoDigestionEntry
        {
            FragmentId = fragmentId,
            SourceActorId = deposit.SourceActorId,
            SourceDeathSequence = deposit.SourceDeathSequence,
            Strength = deposit.Strength,
            StartedAt = YaoTime.Now,
            CompleteAt = YaoTime.Now + DigestDuration(),
            Cost = cost,
            Phase = YaoDigestionPhase.Queued,
        };
        predator.E.GetComponent<YaoDigestion>() = digestion;

        deposit.Claimed = true;
        deposits[deposit.SourceActorId] = deposit;
        predator.MarkCultiwayStatsDirty();
        return true;
    }

    /// <summary>按稳定编号解析语义资产。</summary>
    private static bool TryResolveSemantic(string semanticId, out SemanticAsset semantic)
    {
        return ModClass.L.SemanticLibrary.TryResolve(semanticId, out semantic);
    }

    /// <summary>吞噬工作按击杀现场直接入队（跳过精华痕迹）。</summary>
    private static bool TryEnqueueFromVictim(ActorExtend predator, Actor victim, float strength)
    {
        string fragmentId = MatchFragment(victim.GetExtend());
        if (fragmentId == null) return false;

        var deposit = new YaoEssenceDeposit
        {
            SourceActorId = victim.data.id,
            SourceDeathSequence = deathSequences.TryGetValue(victim.data.id, out int seq) ? seq : nextDeathSequence,
            Position = new Vector2(victim.current_position.x, victim.current_position.y),
            ExpireAt = YaoTime.Now,
            FragmentId = fragmentId,
            Strength = strength,
        };
        return TryEnqueue(predator, deposit, fragmentId);
    }

    /// <summary>推进全部消化队列；由低频系统调用。</summary>
    public static void Update(ActorExtend actor, ref YaoDigestion digestion, ref Yao yao)
    {
        digestion.EnsureInitialized();
        float now = YaoTime.Now;
        bool activeFound = false;

        for (int i = 0; i < YaoDigestion.QueueSize; i++)
        {
            YaoDigestionEntry entry = digestion.Queue[i];
            if (entry.IsEmpty || entry.Phase is not (YaoDigestionPhase.Queued or YaoDigestionPhase.Digesting))
                continue;

            // 同时只有一格真正推进，其余排队。
            if (activeFound) break;
            activeFound = true;

            entry.Phase = YaoDigestionPhase.Digesting;
            if (now >= entry.CompleteAt)
            {
                entry.Phase = YaoDigestionPhase.Ready;
                YaoOrganCandidateService.ResolveCompleted(actor, ref digestion, ref yao, entry);
            }

            digestion.Queue[i] = entry;
        }

        actor.E.GetComponent<YaoDigestion>() = digestion;
    }

    /// <summary>消化时长：随样本强度小幅延长，固定在 30 到 90 世界秒之间。</summary>
    public static float DigestDuration()
    {
        return 30f;
    }

    /// <summary>按器官编号读取碎片规定的器官与等级。</summary>
    public static bool TryGetFragmentOrgan(string fragmentId, out string organId, out int rank)
    {
        organId = null;
        rank = 0;
        foreach (var fragment in fragments)
        {
            if (fragment.fragmentId != fragmentId) continue;
            organId = fragment.organId;
            rank = fragment.rank;
            return true;
        }

        return false;
    }

    /// <summary>队列中是否有正在消化或待结算的条目。</summary>
    public static bool HasPending(ActorExtend actor)
    {
        return actor.E.TryGetComponent(out YaoDigestion digestion) && digestion.CountOccupied() > 0;
    }

    /// <summary>清理世界时直接清空全部精华与死亡序号。</summary>
    public static void ClearWorldState()
    {
        deposits.Clear();
        deathSequences.Clear();
        nextDeathSequence = 0;
    }

    private static int FindFreeSlot(YaoDigestion digestion)
    {
        for (int i = 0; i < YaoDigestion.QueueSize; i++)
        {
            if (digestion.Queue[i].IsEmpty ||
                digestion.Queue[i].Phase is YaoDigestionPhase.Resolved or YaoDigestionPhase.Rejected)
                return i;
        }

        return -1;
    }

    /// <summary>按猎物的真实语义特征匹配白名单碎片；没有匹配返回 null。</summary>
    private static string MatchFragment(ActorExtend victim)
    {
        SemanticProfile profile = victim.GetSemanticProfile();
        foreach (var fragment in fragments)
        {
            if (!TryResolveSemantic(fragment.semanticId, out SemanticAsset semantic)) continue;
            if (profile.Has(semantic, SemanticQueryPolicy.Default)) return fragment.fragmentId;
        }

        return null;
    }
}
