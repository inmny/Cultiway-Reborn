using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>让实际世界需求选择已学普通构造，不需要敌人或预设整招编号。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>一个支付者的有界重试位置，不属于长期世界事实。</summary>
    private sealed class WorldNeedState
    {
        /// <summary>下一次实际需求检查。</summary>
        internal double Next;

        /// <summary>独立轮换此人的材料和已学能力。</summary>
        internal int MaterialOffset, AbilityOffset;
    }

    /// <summary>只排入被世界材料或实际学习唤醒的人物。</summary>
    private static readonly Queue<long> worldNeedQueue = new();

    /// <summary>每人一份临时需求，重复事件不重复排队。</summary>
    private static readonly Dictionary<long, WorldNeedState> worldNeeds = new();

    /// <summary>已有世界回收过程的人数计数，避免不断扫描全部运行施放。</summary>
    private static readonly Dictionary<long, int> worldRecoveryCounts = new();

    /// <summary>选择预估复用真实作用形状。</summary>
    private static readonly List<AbilityCapsule> worldNeedSelection = new();

    /// <summary>只接收实际变化的支付者，永久资料与临时队列分别维护。</summary>
    private static void InitializeWorldNeeds() => WorldMaterialService.Changed += OnWorldMaterialNeed;

    /// <summary>世界材料变化只唤醒，不在它的确认回调里重新查询或修改材料。</summary>
    /// <param name="change">已确认的变化。</param>
    private static void OnWorldMaterialNeed(WorldMaterialChange change) => NotifyWorldNeed(change.Provider);

    /// <summary>同一世界内按稳定人物编号接受需求唤醒。</summary>
    /// <param name="provider">原支付者的长期编号。</param>
    internal static void NotifyWorldNeed(long provider)
    {
        if (provider <= 0 || worldNeeds.ContainsKey(provider)) return;
        worldNeeds.Add(provider, new WorldNeedState());
        worldNeedQueue.Enqueue(provider);
    }

    /// <summary>每个共同模拟边界最多检查八人，每人至多八项详细能力检查；没有需求时不遍历人物。</summary>
    private static void ProcessWorldNeeds()
    {
        if (!GeneralSettings.EnableSkillSystems || SmoothLoader.isLoading()) return;
        double now = World.world.getCurWorldTime();
        int count = Math.Min(8, worldNeedQueue.Count);
        for (int i = 0; i < count; i++)
        {
            long id = worldNeedQueue.Dequeue();
            WorldNeedState state = worldNeeds[id];
            bool keep = true;
            if (now >= state.Next)
            {
                state.Next = now + 2;
                Actor actor = World.world.units.get(id);
                if (actor.isRekt() || actor.data.id != id || !actor.isAlive() || !actor.hasHealth()) keep = false;
                else if (!WorldMaterialService.TryTemporaryNeed(id, ref state.MaterialOffset,
                             out long material, out Vector3 point, out MaterialFunding funding)) keep = false;
                else if (material > 0 && !actor.hasTag("frozen_ai") && !actor.under_forces && !actor.isInMagnet() &&
                         !ControllableUnit.isControllingUnit(actor) && !CombatWorldService.IsEngaged(actor) &&
                         actor.attack_target.isRekt() && !worldRecoveryCounts.ContainsKey(id))
                    keep = TryWorldRecovery(actor.GetExtend(), state, material, point, funding);
            }

            if (keep) worldNeedQueue.Enqueue(id);
            else worldNeeds.Remove(id);
        }
    }

    /// <summary>仅从实际知识选择普通世界回收，正式入口仍决定能否支付和启动。</summary>
    /// <param name="owner">实际原支付者。</param><param name="state">有限轮换位置。</param>
    /// <param name="material">实际临时材料。</param><param name="point">它的真实形状位置。</param>
    /// <param name="funding">原支付资源组成。</param>
    private static bool TryWorldRecovery(ActorExtend owner, WorldNeedState state, long material, Vector3 point,
        MaterialFunding funding)
    {
        if (!owner.E.TryGetComponent(out ConstructedKnowledge known) || known.Ids.Length == 0) return false;
        bool any = false;
        foreach (string id in known.Ids)
        {
            CompiledAbility plan = PlanFor(id);
            if ((plan.Use.Channels & ActiveAbilityChannel.World) != 0 && plan.HasRecovery)
            {
                any = true;
                break;
            }
        }

        if (!any) return false;
        if (!funding.Entries.Any(entry => MaterialRecoveryCredit.Room(owner, entry.Resource) > 0)) return true;
        int start = state.AbilityOffset % known.Ids.Length;
        int count = Math.Min(8, known.Ids.Length);
        for (int i = 0; i < count; i++)
        {
            int index = (start + i) % known.Ids.Length;
            state.AbilityOffset = (index + 1) % known.Ids.Length;
            string id = known.Ids[index];
            CompiledAbility plan = PlanFor(id);
            if ((plan.Use.Channels & ActiveAbilityChannel.World) == 0 || plan.Body != null ||
                plan.Use.Target is not (ActiveAbilityTargetMode.Point or ActiveAbilityTargetMode.ObjectOrPoint))
                continue;
            var handle = new ActiveAbilityHandle(ProviderId, owner.E, id);
            AbilityHeightField ground = GroundProvider?.Invoke(new Rect(point.x, point.y, 1, 1));
            Vector3 relative = point;
            relative.z -= ground?.Sample(point.x, point.y) ?? 0f;
            var target = new ActiveAbilityTarget(null, relative);
            if (!ActiveAbilityService.CanUse(owner, handle, target)) continue;
            int node = Array.FindIndex(plan.Nodes, form => form.Recovery?.WorldMaterials == true &&
                                                           form.Recovery.TemporaryOnly && form.Recovery.Efficiency > 0);
            if (node < 0) continue;
            StartingPose(owner.Base, out Vector3 source, out float height);
            var inputs = new AbilityInputs(source, point, (point - source).normalized, height);
            var preview = new AbilityCast(plan, Matrix4x4.TRS(point, Quaternion.identity, Vector3.one), inputs: inputs);
            preview.Advance(plan.Nodes[node].RecoveryAt, point);
            worldNeedSelection.Clear();
            preview.MaterialSelection(node, worldNeedSelection);
            double expected = WorldMaterialService.EstimateRecovery(material, worldNeedSelection) *
                              plan.Nodes[node].Recovery.Efficiency;
            var refunds = funding.Totals(expected);
            var quoted = ConstructionResources.Preview(owner, plan);
            if (quoted == null) continue;
            var costs = quoted.ToDictionary(entry => entry.Resource.id, entry => entry.Cost, StringComparer.Ordinal);
            var balances = refunds.Keys.Concat(costs.Keys).Distinct().GroupBy(id => ConstructionResources.Resolve(id).BalanceId);
            bool gain = false, loss = false;
            foreach (var balance in balances)
            {
                double refund = balance.Sum(id => refunds.TryGetValue(id, out double amount) ? amount : 0);
                double cost = balance.Sum(id => costs.TryGetValue(id, out double amount) ? amount : 0);
                double room = MaterialRecoveryCredit.Room(owner, balance.First());
                double net = Math.Min(refund, room + cost) - cost;
                gain |= net > .001; loss |= net < -.001;
            }
            if (!gain || loss) continue;
            Actor actor = owner.Base;
            long provider = actor.id, generation = worldGeneration;
            var scope = new ConstructionWorldScope(
                () => generation == worldGeneration && actor.data?.id == provider && actor.isAlive() && actor.hasHealth(),
                material, () => WorldMaterialService.IsTemporaryNeedCurrent(material, provider));
            var request = new ActiveAbilityTarget(null, relative, worldScope: scope);
            if (ActiveAbilityService.TryUse(owner, handle, request, ActiveAbilityUseOrigin.Autonomous)) return true;
        }

        return true;
    }

    /// <summary>开始与结束共用计数，正常收势、中断及清世界均只释放一次。</summary>
    /// <param name="running">已开始或正在回收的施放。</param><param name="start">是否实际开始。</param>
    private static void TrackWorldRecovery(RunningCast running, bool start)
    {
        if (start)
        {
            if ((running.Cast.Plan.Use.Channels & ActiveAbilityChannel.World) == 0 ||
                !running.Cast.Plan.HasRecovery) return;
            running.WorldRecoveryTracked = true;
            worldRecoveryCounts.TryGetValue(running.SourceId, out int previous);
            worldRecoveryCounts[running.SourceId] = previous + 1;
        }
        else if (running.WorldRecoveryTracked)
        {
            running.WorldRecoveryTracked = false;
            int remaining = worldRecoveryCounts[running.SourceId] - 1;
            if (remaining == 0) worldRecoveryCounts.Remove(running.SourceId);
            else worldRecoveryCounts[running.SourceId] = remaining;
        }
    }
}
