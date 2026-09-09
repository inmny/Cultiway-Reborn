using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>低频推进妖兽消化队列与失败记忆衰减。</summary>
public sealed class YaoDigestionSystem : QuerySystem<ActorBinder, YaoDigestion>
{
    private const int ActorsPerBatch = 8;
    private const float Interval = 1f;

    private readonly List<(Actor actor, Entity entity)> batch = new();
    private float timer;

    /// <summary>只统计未回收的活动实体。</summary>
    public YaoDigestionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    /// <summary>攒够间隔后每批推进有限数量的消化队列。</summary>
    protected override void OnUpdate()
    {
        timer -= Tick.deltaTime;
        if (timer > 0f) return;
        timer = Interval;

        batch.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref YaoDigestion _, Entity entity) =>
        {
            if (batch.Count >= ActorsPerBatch) return;
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            batch.Add((actor, entity));
        });

        foreach ((Actor actor, Entity entity) in batch)
        {
            ActorExtend extend = actor.GetExtend();
            ref YaoDigestion digestion = ref entity.GetComponent<YaoDigestion>();
            ref Yao yao = ref extend.GetCultisys<Yao>();
            YaoDigestionService.Update(extend, ref digestion, ref yao);
        }
    }
}
