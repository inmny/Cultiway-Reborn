using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>只推进已经拥有核心形成运行时的角色。</summary>
public sealed class CoreFormationEffectSystem : QuerySystem<ActorBinder, CoreFormationEffectRuntime>
{
    private readonly List<ActorExtend> actors = new();

    /// <summary>排除预制体、失活与待回收角色实体。</summary>
    public CoreFormationEffectSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    /// <summary>先收集运行时角色，离开查询迭代后再安全增删运行时组件。</summary>
    protected override void OnUpdate()
    {
        actors.Clear();
        Query.ForEachComponents((ref ActorBinder binder, ref CoreFormationEffectRuntime _) =>
        {
            actors.Add(binder.AE);
        });
        for (var i = 0; i < actors.Count; i++)
            CoreFormationEffectRuntimeBridge.Advance(actors[i], Tick.deltaTime);
    }
}
