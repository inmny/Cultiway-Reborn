using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>扫描可用角色并推进核心形成效果的冷却、持续形态和资源池。</summary>
public sealed class CoreFormationEffectSystem : QuerySystem<ActorBinder>
{
    private readonly List<ActorExtend> actors = new();

    /// <summary>过滤预制体、失活与待回收角色实体。</summary>
    public CoreFormationEffectSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    /// <summary>先收集角色，离开 ECS 查询迭代后再安全增删运行时组件。</summary>
    protected override void OnUpdate()
    {
        actors.Clear();
        Query.ForEachComponents((ref ActorBinder binder) =>
        {
            if (binder.Actor == null || binder.Actor.isRekt()) return;
            actors.Add(binder.Actor.GetExtend());
        });
        for (var i = 0; i < actors.Count; i++)
            CoreFormationEffectRuntimeBridge.Advance(actors[i], Tick.deltaTime);
    }
}
