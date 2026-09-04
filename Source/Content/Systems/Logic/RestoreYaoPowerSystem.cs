using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>妖兽每月按世界环境自然恢复妖力。</summary>
public class RestoreYaoPowerSystem : QuerySystem<Yao, ActorBinder>
{
    private float _restore_timer = TimeScales.SecPerMonth;

    /// <summary>只统计未回收的活动实体。</summary>
    public RestoreYaoPowerSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagUncompleted, TagRecycle>());
    }

    /// <summary>按月恢复到上限的一定比例，暂停世界时不恢复。</summary>
    [Hotfixable]
    protected override void OnUpdate()
    {
        _restore_timer -= Tick.deltaTime;
        if (_restore_timer > 0) return;
        _restore_timer = TimeScales.SecPerMonth;

        Query.ForEachComponents(([Hotfixable](ref Yao yao, ref ActorBinder binder) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            ActorExtend extend = actor.GetExtend();
            float maximum = actor.stats[BaseStatses.MaxYaoPower.id] * YaoSetting.YaoPowerRestoreLimit;
            if (yao.yao_power >= maximum) return;

            float regen = actor.stats[BaseStatses.YaoPowerRegen.id];
            YaoResourceService.Gain(extend, ref yao, regen);
        }));
    }
}
