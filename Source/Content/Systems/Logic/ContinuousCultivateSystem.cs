using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 持续修炼系统（处理Continuous类型的修炼方式，如国运修炼）
/// </summary>
public class ContinuousCultivateSystem : QuerySystem<Xian, ActorBinder>
{
    private float _updateTimer = 0f;
    private const float UpdateInterval = 1f;

    public ContinuousCultivateSystem()
    {
        Filter.AnyTags(Tags.Get<ContinuousCultivateTag>());
    }

    protected override void OnUpdate()
    {
        _updateTimer -= Tick.deltaTime;
        if (_updateTimer > 0) return;
        _updateTimer = UpdateInterval;

        Query.ForEachComponents(([Hotfixable](ref Xian xian, ref ActorBinder binder) =>
        {
            var actor = binder.Actor;
            if (actor == null || !actor.isAlive()) return;

            var ae = actor.GetExtend();
            var mainCultibook = ae.GetMainCultibook();
            if (mainCultibook == null) return;

            var method = mainCultibook.GetCultivateMethod();
            if (method == null) return;

            if (method.CanCultivate != null && !method.CanCultivate(ae)) return;

            var maxWakan = actor.stats[BaseStatses.MaxWakan.id];
            if (xian.wakan >= maxWakan) return;

            // 持续修炼的收益计算（这里简化处理，实际应该由修炼方式自己定义）
            var efficiency = method.GetEfficiency?.Invoke(ae) ?? 1f;
            var baseGain = 0.1f * efficiency; // 每秒基础收益
            var wakanGain = baseGain * UpdateInterval;

            wakanGain = Mathf.Min(wakanGain, maxWakan - xian.wakan);
            if (wakanGain > 0)
            {
                xian.wakan += wakanGain;
                
                method.OnSideEffect?.Invoke(ae, wakanGain);
            }
        }));
    }
}

