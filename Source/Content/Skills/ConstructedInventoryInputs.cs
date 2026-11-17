using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Persistence;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>本次施放对库存材料的临时借用，不写入长期库存资料。</summary>
internal struct ConstructedInventorySession : IComponent { }

/// <summary>库存继续持有原物件，本次施放只独占使用，真实消耗后才退出库存。</summary>
internal sealed class ConstructedInventoryInputs : IDisposable
{
    /// <summary>仍在借用的实际会话，库存转移可立即找到并停止原施放。</summary>
    private static readonly Dictionary<Entity, ConstructedInventoryInputs> sessions = new();
    /// <summary>物件原库存及人物身份。</summary>
    private readonly Entity inventory, execution;
    /// <summary>实际人物及其开始时的编号。</summary>
    private readonly Actor actor;
    /// <summary>实际人物的稳定身份。</summary>
    private readonly long actorId;
    /// <summary>每类尚未使用的原物件。</summary>
    private readonly Dictionary<string, List<Entity>> items;
    /// <summary>每次操作已真正消耗的稳定编号，重复提交只返回原回执。</summary>
    private readonly Dictionary<int, string[]> consumed = new();
    /// <summary>正式受理后才取得施放编号，启动中的借用也可独立释放。</summary>
    internal long CastId;
    /// <summary>结束只释放一次。</summary>
    private bool disposed;

    /// <summary>只记录已取得的真实身份。</summary>
    /// <param name="owner">原库存人物。</param><param name="execution">本次独占会话。</param><param name="items">按输入名称分组的原物件。</param>
    private ConstructedInventoryInputs(ActorExtend owner, Entity execution, Dictionary<string, List<Entity>> items)
    { inventory = owner.E; actor = owner.Base; actorId = actor.id; this.execution = execution; this.items = items; }

    /// <summary>沿通用库存变更入口解除借用，读档不重新建立短时会话。</summary>
    internal static void Initialize()
    {
        InventoryLifecycle.RegisterBeforeItemAdded(ChangingInventory);
        InventoryLifecycle.RegisterBeforeItemExtracted(ChangingInventory);
    }

    /// <summary>正式登记和编辑共用真实物品形状检查。</summary>
    /// <param name="plan">完整计划。</param>
    internal static void Validate(CompiledAbility plan)
    {
        foreach (var input in plan.Inventory)
            if (ModClass.L.ItemShapeLibrary.get(input.Shape) == null)
                throw new ArgumentException("Inventory：物品形状不存在：" + input.Shape);
    }

    /// <summary>当前可作为原材料的真实物件，创作采集与实际取用共用。</summary>
    /// <param name="item">库存原物件。</param>
    internal static bool IsMaterial(Entity item) => item.IsAvailable() && item.Tags.Has<TagIngredient>() && item.HasComponent<ItemShape>() &&
        !item.HasComponent<SkillExecutionBodyLease>() && item.GetIncomingLinks<CraftOccupyingRelation>().Entities.Count == 0;

    /// <summary>同一套选择用于可用性和启动，重复材料声明不能复用同一件物品。</summary>
    /// <param name="owner">当前库存人物。</param><param name="requirements">已编译的实际要求。</param>
    /// <param name="selected">按名称分组的原物件。</param>
    private static bool Select(ActorExtend owner, CompiledInventoryInput[] requirements, out Dictionary<string, List<Entity>> selected)
    {
        selected = new(StringComparer.Ordinal);
        if (requirements.Length == 0) return true;
        if (owner?.Base?.data == null || !owner.Base.isAlive() || owner.E.IsNull) return false;
        foreach (var requirement in requirements) selected.Add(requirement.Id, new List<Entity>(requirement.Count));
        int left = requirements.Sum(requirement => requirement.Count);
        foreach (Entity item in owner.GetItems())
        {
            if (!IsMaterial(item)) continue;
            ItemShape shape = item.GetComponent<ItemShape>();
            foreach (var requirement in requirements)
            {
                var group = selected[requirement.Id];
                if (shape.shape_id != requirement.Shape || group.Count == requirement.Count) continue;
                group.Add(item); left--; break;
            }
            if (left == 0) return true;
        }
        return false;
    }

    /// <summary>没有材料声明的旧构造直接沿用原可用性。</summary>
    /// <param name="owner">当前人物。</param><param name="plan">实际计划。</param>
    internal static bool CanAcquire(ActorExtend owner, CompiledAbility plan) => plan.Inventory.Length == 0 || Select(owner, plan.Inventory, out _);

    /// <summary>扣费前取得全部原物件，任何失败释放本次已取得的部分。</summary>
    /// <param name="owner">原库存人物。</param><param name="plan">实际计划。</param>
    internal static ConstructedInventoryInputs TryAcquire(ActorExtend owner, CompiledAbility plan)
    {
        if (!Select(owner, plan.Inventory, out var selected)) return null;
        Entity execution = ModClass.I.W.CreateEntity(new SkillExecution(), new SkillExecutionWithoutBody(), new ConstructedInventorySession());
        var session = new ConstructedInventoryInputs(owner, execution, selected); sessions.Add(execution, session);
        foreach (Entity item in selected.Values.SelectMany(group => group))
        {
            if (!SkillExecutionLifecycle.TryBorrowBody(execution, item, "inventory")) { session.Dispose(); return null; }
            if (!item.HasComponent<WorldSaveIdentity>()) item.AddComponent(new WorldSaveIdentity { Id = Guid.NewGuid().ToString("N") });
            item.AddTag<TagOccupied>();
        }
        return session;
    }

    /// <summary>尚未消耗的材料必须仍由原人物持有及本会话独占。</summary>
    internal bool IsValid => !disposed && (items.Values.All(group => group.Count == 0) ||
        !inventory.IsNull && actor.data?.id == actorId && actor.isAlive() &&
        items.Values.SelectMany(group => group).All(item => !item.IsNull && item.Tags.Has<TagOccupied>() &&
            !item.Tags.HasAny(Tags.Get<TagConsumed, TagRecycle>()) &&
            item.TryGetComponent(out SkillExecutionBodyLease lease) && lease.execution == execution &&
            item.GetIncomingLinks<InventoryRelation>().Entities.Contains(inventory)));

    /// <summary>只在主线程确认窗口消费，先记录不可重复的事实，再移出库存并交给原回收系统。</summary>
    /// <param name="operation">本次操作身份。</param><param name="use">材料输入和件数。</param>
    /// <param name="identities">已经消耗的原物件稳定编号。</param>
    internal bool TryConsume(int operation, CompiledInventoryUse use, out string[] identities)
    {
        if (consumed.TryGetValue(operation, out identities)) return true;
        if (!IsValid || !items.TryGetValue(use.Input, out var group) || group.Count < use.Count) return false;
        Entity[] taking = group.Take(use.Count).ToArray();
        identities = taking.Select(item => "object:" + item.GetComponent<WorldSaveIdentity>().Id).ToArray();
        consumed.Add(operation, identities); group.RemoveRange(0, use.Count);
        foreach (Entity item in taking)
        {
            item.AddTag<TagConsumed>(); item.AddTag<TagRecycle>();
            execution.RemoveRelation<SkillExecutionBodyRelation>(item);
            item.RemoveComponent<SkillExecutionBodyLease>(); item.RemoveTag<TagOccupied>();
            if (!inventory.IsNull) inventory.RemoveRelation<InventoryRelation>(item);
        }
        return true;
    }

    /// <summary>保存时只忽略本会话的短时占用，炼制原有占用仍保存。</summary>
    /// <param name="item">实际原物件。</param>
    internal static bool IsBorrowed(Entity item) => !item.IsNull && item.TryGetComponent(out SkillExecutionBodyLease lease) &&
        !lease.execution.IsNull && lease.execution.HasComponent<ConstructedInventorySession>();

    /// <summary>转移借用物件前停止旧施放并归还其余物件的使用权。</summary>
    /// <param name="owner">本次库存操作的持有者。</param><param name="item">即将转移的原物件。</param>
    private static void ChangingInventory(IHasInventory owner, Entity item)
    {
        if (!IsBorrowed(item)) return;
        if (!sessions.TryGetValue(item.GetComponent<SkillExecutionBodyLease>().execution, out var session)) return;
        if (session.CastId != 0) ConstructedAbilityService.Cancel(session.CastId);
        session.Dispose();
    }

    /// <summary>保留原物件及原库存关系，只解除本次仍拥有的占用。</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; sessions.Remove(execution);
        foreach (Entity item in items.Values.SelectMany(group => group))
            if (!item.IsNull && item.TryGetComponent(out SkillExecutionBodyLease lease) && lease.execution == execution)
                item.RemoveTag<TagOccupied>();
        SkillExecutionLifecycle.ReleaseBodies(execution); SkillExecutionLifecycle.RequestEnd(execution);
    }

    /// <summary>换世界还包括正在启动、尚未加入正式列表的借用。</summary>
    internal static void Clear()
    {
        foreach (var session in sessions.Values.ToArray()) session.Dispose();
    }
}
