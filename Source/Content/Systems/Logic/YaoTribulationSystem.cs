using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进当前世界中正在进行的妖丹天劫。</summary>
public sealed class YaoTribulationSystem : QuerySystem<ActorBinder, YaoTribulation>
{
    private readonly List<Actor> tribulating = new();

    /// <summary>只统计未回收的活动实体。</summary>
    public YaoTribulationSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    /// <summary>先收集劫程中的妖兽，查询结束后再提交状态变化。</summary>
    protected override void OnUpdate()
    {
        tribulating.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref YaoTribulation _, Entity entity) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            tribulating.Add(actor);
        });

        foreach (Actor actor in tribulating)
        {
            ActorExtend extend = actor.GetExtend();
            Entity entity = extend.E;
            ref YaoTribulation tribulation = ref entity.GetComponent<YaoTribulation>();
            YaoTribulationService.Update(extend, ref tribulation);
        }
    }
}
