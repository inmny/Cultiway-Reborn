using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

public class RestoreWakanSystem : QuerySystem<Xian, ActorBinder>
{
    private float _restore_timer = TimeScales.SecPerMonth;
    public RestoreWakanSystem()
    {
        Filter.AllComponents(ComponentTypes.Get<Xian>());
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }
    protected override void OnUpdate()
    {
        if (!GeneralSettings.EnableNaturalWakanRestore) return;
        _restore_timer -= Tick.deltaTime;
        if (_restore_timer > 0) return;
        _restore_timer = TimeScales.SecPerMonth;
        Query.ForEachComponents(([Hotfixable](ref Xian xian, ref ActorBinder binder) =>
        {
            var a = binder.Actor;
            if (a.isRekt()) return;
            var max_wakan = a.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit;
            if (xian.wakan >= max_wakan) return;
            CultivationSettlementService.AbsorbAmbientWakan(
                a.GetExtend(),
                a.stats[BaseStatses.WakanRegen.id],
                max_wakan);
        }));
    }
}
