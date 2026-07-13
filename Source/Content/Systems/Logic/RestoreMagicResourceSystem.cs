using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 按月恢复魔法师的 mana 和精神力。mana 可以自然回满；精神力只恢复到设定比例，突破仍需主动冥想。
/// </summary>
public sealed class RestoreMagicResourceSystem : QuerySystem<Magic, ActorBinder>
{
    private float _restoreTimer = TimeScales.SecPerMonth;

    public RestoreMagicResourceSystem()
    {
        Filter.AllComponents(ComponentTypes.Get<Magic>());
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        if (!GeneralSettings.EnableNaturalMagicRestore) return;

        _restoreTimer -= Tick.deltaTime;
        if (_restoreTimer > 0f) return;
        _restoreTimer = TimeScales.SecPerMonth;

        Query.ForEachComponents(([Hotfixable](ref Magic magic, ref ActorBinder binder) =>
        {
            var actor = binder.Actor;
            if (actor.isRekt()) return;

            var maxSpirit = actor.stats[BaseStatses.MaxSpirit.id];
            var spiritLimit = maxSpirit * MagicSetting.SpiritRestoreLimit;
            if (magic.spirit < spiritLimit)
            {
                var spiritRegen = Mathf.Max(0f, actor.stats[BaseStatses.SpiritRegen.id]);
                magic.spirit = Mathf.Min(spiritLimit, magic.spirit + spiritRegen);
            }

            var manaRegen = Mathf.FloorToInt(Mathf.Max(0f, actor.stats[BaseStatses.ManaRegen.id]));
            if (manaRegen > 0)
            {
                actor.restoreMana(manaRegen);
            }
        }));
    }
}
