using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进当前世界中正在进行的涅槃过程。</summary>
public sealed class NirvanaSystem : QuerySystem<ActorBinder, Nirvana>
{
    private const int ActorsPerBatch = 4;

    private readonly List<Actor> batch = new();
    private float timer;

    /// <summary>只统计未回收的活动实体。</summary>
    public NirvanaSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    /// <summary>每秒推进有限数量的涅槃过程。</summary>
    protected override void OnUpdate()
    {
        timer -= Tick.deltaTime;
        if (timer > 0f) return;
        timer = 1f;

        batch.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref Nirvana _, Entity entity) =>
        {
            if (batch.Count >= ActorsPerBatch) return;
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            batch.Add(actor);
        });

        foreach (Actor actor in batch)
        {
            ActorExtend extend = actor.GetExtend();
            ref Nirvana nirvana = ref extend.E.GetComponent<Nirvana>();
            YaoNirvanaService.Update(extend, ref nirvana);
        }
    }
}
