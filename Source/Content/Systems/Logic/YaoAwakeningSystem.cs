using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>按稳定批处理推进凡兽启灵积累，并提交满足条件的启灵。</summary>
public sealed class YaoAwakeningSystem : QuerySystem<YaoAwakeningPotential, ActorBinder>
{
    private const int CandidatesPerBatch = 8;

    private readonly List<Entity> expired = new();
    private readonly List<Actor> awakeningCandidates = new();
    private float timer;

    /// <summary>只统计未回收的活动实体。</summary>
    public YaoAwakeningSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    /// <summary>先收集要移除的积累与满足条件的候选，查询结束后再提交结构变化。</summary>
    protected override void OnUpdate()
    {
        timer -= Tick.deltaTime;
        if (timer > 0f) return;
        timer = YaoSetting.AwakeningEvaluationInterval;

        expired.Clear();
        awakeningCandidates.Clear();
        int processed = 0;

        Query.ForEachEntity((ref YaoAwakeningPotential potential, ref ActorBinder binder, Entity entity) =>
        {
            if (processed >= CandidatesPerBatch) return;
            processed++;

            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt())
            {
                expired.Add(entity);
                return;
            }

            YaoAwakeningService.AccrueExposure(actor.GetExtend(), ref potential);
            entity.GetComponent<YaoAwakeningPotential>() = potential;

            if (YaoAwakeningService.MeetsAwakeningThresholds(actor.GetExtend(), ref potential))
                awakeningCandidates.Add(actor);
        });

        foreach (Entity entity in expired)
        {
            entity.RemoveComponent<YaoAwakeningPotential>();
        }

        int awakened = 0;
        foreach (Actor actor in awakeningCandidates)
        {
            if (awakened >= 2) break;
            if (YaoAwakeningService.TryAwaken(actor.GetExtend())) awakened++;
        }
    }
}
