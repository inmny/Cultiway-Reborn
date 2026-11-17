using System;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Persistence;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>原库存物件已经接受的额外寿命进度，到期继续交原回收流程。</summary>
public readonly struct ConstructedInventoryAging
{
    /// <summary>本次施放、原来源、当前归属与实际库存人物。</summary>
    public readonly long CastId, SourceId, OwnerId, TargetId;
    /// <summary>正式构造、原物件稳定身份及其实际形状。</summary>
    public readonly string AbilityId, ItemId, Shape;
    /// <summary>真实作用部位。</summary>
    public readonly int Node;
    /// <summary>本次额外推进前后进度及原寿命上限，单位为世界秒。</summary>
    public readonly float Before, After, Lifetime;
    /// <summary>本次已到期并交给原回收流程。</summary>
    public readonly bool Expired;
    /// <summary>本次真实接触的共同世界时点。</summary>
    public readonly double WorldTime;

    /// <summary>只记录已经提交到同一原物件的进度。</summary>
    /// <param name="cast">实际施放。</param><param name="source">原来源。</param><param name="owner">当前归属。</param>
    /// <param name="target">实际库存人物。</param><param name="ability">正式构造。</param><param name="item">原物件稳定身份。</param>
    /// <param name="shape">实际形状。</param><param name="node">作用部位。</param><param name="before">原进度。</param>
    /// <param name="after">接受后的进度。</param><param name="lifetime">原寿命上限。</param>
    /// <param name="expired">已交原回收流程。</param><param name="time">真实接触时点。</param>
    internal ConstructedInventoryAging(long cast, long source, long owner, long target, string ability, string item,
        string shape, int node, float before, float after, float lifetime, bool expired, double time)
    {
        CastId = cast; SourceId = source; OwnerId = owner; TargetId = target;
        AbilityId = ability; ItemId = item; Shape = shape; Node = node;
        Before = before; After = after; Lifetime = lifetime; Expired = expired; WorldTime = time;
    }
}

/// <summary>真实接触沿原库存和原寿命推进，不另造物件或结束产物。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>库存原物件已经接受额外进度。</summary>
    public static event Action<ConstructedInventoryAging> InventoryAgingConfirmed;

    /// <summary>按当前库存顺序选择有限寿命；到期即时失效，再通知原库存释放借用。</summary>
    /// <param name="running">实际施放。</param><param name="node">作用部位。</param><param name="target">已确认的实际人物。</param>
    /// <param name="effect">已编译的有限进度及筛选。</param><param name="result">真实攻击结果。</param>
    /// <param name="strength">接触或传播衰减。</param>
    /// <returns>仍为同一世界的存活人物，且原施放仍可继续时返回真。</returns>
    private static bool ApplyInventoryAging(RunningCast running, int node, Actor target, CompiledInventoryAging effect,
        ApplyAttackState result, float strength)
    {
        if (effect == null || result != ApplyAttackState.Hit) return true;
        long epoch = worldGeneration, targetId = target.id, owner = running.Owner(node).Id;
        double time = simulation.contactTime;
        var inventory = target.GetExtend(); int accepted = 0;
        foreach (Entity item in inventory.GetItems().ToArray())
        {
            if (accepted >= effect.MaximumItems) break;
            if (!CanAdvanceInventoryLife(item) ||
                !item.GetIncomingLinks<InventoryRelation>().Entities.Contains(inventory.E)) continue;
            string shape = item.GetComponent<ItemShape>().shape_id;
            if (effect.Shapes.Count > 0 && !effect.Shapes.Contains(shape)) continue;
            float before = item.GetComponent<AliveTimer>().value, limit = item.GetComponent<AliveTimeLimit>().value;
            float after = InventoryAgingEnd(before, limit, effect.Seconds * (double)strength);
            if (after <= before) continue;
            if (!item.HasComponent<WorldSaveIdentity>())
                item.AddComponent(new WorldSaveIdentity { Id = Guid.NewGuid().ToString("N") });
            bool expired = after >= limit;
            var fact = new ConstructedInventoryAging(running.Id, running.SourceId, owner, targetId, running.Cast.Plan.Id,
                "object:" + item.GetComponent<WorldSaveIdentity>().Id, shape, node, before, after, limit, expired, time);
            item.GetComponent<AliveTimer>().value = after; accepted++;
            if (expired)
            {
                item.AddTag<TagRecycle>();
                inventory.ExtractSpecialItem(item);
            }
            InventoryAgingConfirmed?.Invoke(fact);
            if (epoch != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth() ||
                running.Cast.Phase == AbilityCastPhase.Interrupted) return false;
        }
        return true;
    }

    /// <summary>只有仍在维护的有限物件寿命可接受进度，实际占用不改变自然寿命。</summary>
    /// <param name="item">当前库存原物件。</param>
    private static bool CanAdvanceInventoryLife(Entity item) => !item.IsNull && item.HasComponent<SpecialItem>() &&
        item.HasComponent<ItemShape>() && !item.Tags.HasAny(Tags.Get<TagPrefab, TagInactive, TagConsumed, TagRecycle>()) &&
        !item.Tags.HasAny(Tags.Get<TagSuspendAliveTimeLimit, TagReadyRecycle>()) &&
        item.TryGetComponent(out AliveTimer timer) && item.TryGetComponent(out AliveTimeLimit limit) &&
        timer.value >= 0 && timer.value < limit.value && limit.value < float.PositiveInfinity;

    /// <summary>向下取可表示进度，实际额外推进始终不超过本次预算及原寿命上限。</summary>
    /// <param name="before">原来已接受的进度。</param><param name="limit">原寿命上限。</param>
    /// <param name="seconds">本次经过接触衰减的额外秒数。</param>
    private static unsafe float InventoryAgingEnd(float before, float limit, double seconds)
    {
        double desired = Math.Min(limit, before + seconds);
        float after = (float)desired;
        if (after > desired)
        {
            int bits = *(int*)&after - 1;
            after = *(float*)&bits;
        }
        return after;
    }
}
