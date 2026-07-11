using System;
using Cultiway.Abstract;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

/// <summary>
/// 特殊物品库存变更的通用扩展点。Core 只负责通知，不感知具体内容类型。
/// </summary>
public static class InventoryLifecycle
{
    private static Action<IHasInventory, Entity> beforeItemAdded;
    private static Action<IHasInventory, Entity> afterItemAdded;
    private static Action<IHasInventory, Entity> beforeItemExtracted;
    private static Action<IHasInventory, Entity> afterItemExtracted;

    public static void RegisterBeforeItemAdded(Action<IHasInventory, Entity> action)
    {
        beforeItemAdded += action;
    }

    public static void RegisterAfterItemAdded(Action<IHasInventory, Entity> action)
    {
        afterItemAdded += action;
    }

    public static void RegisterBeforeItemExtracted(Action<IHasInventory, Entity> action)
    {
        beforeItemExtracted += action;
    }

    public static void RegisterAfterItemExtracted(Action<IHasInventory, Entity> action)
    {
        afterItemExtracted += action;
    }

    internal static void NotifyBeforeItemAdded(IHasInventory inventory, Entity item)
    {
        beforeItemAdded?.Invoke(inventory, item);
    }

    internal static void NotifyAfterItemAdded(IHasInventory inventory, Entity item)
    {
        afterItemAdded?.Invoke(inventory, item);
    }

    internal static void NotifyBeforeItemExtracted(IHasInventory inventory, Entity item)
    {
        beforeItemExtracted?.Invoke(inventory, item);
    }

    internal static void NotifyAfterItemExtracted(IHasInventory inventory, Entity item)
    {
        afterItemExtracted?.Invoke(inventory, item);
    }
}
