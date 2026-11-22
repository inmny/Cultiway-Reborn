using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>一次性法术物品正在等待实际发射或完成研读。该占用随当前世界结束。</summary>
internal struct TagPendingConsumable : ITag;
