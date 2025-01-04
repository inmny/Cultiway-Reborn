using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Prefab;
using Friflo.Engine.ECS;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class ElixirLibrary : DynamicAssetLibrary<ElixirAsset>
{
    public override void init()
    {
        base.init();
        SpecialItemTooltip.RegisterSetupAction((tooltip, type, entity) =>
        {
            if (entity.TryGetComponent(out Elixir elixir)) tooltip.Tooltip.name.text = LM.Get(elixir.Type.name_key);
        });
        ActorExtend.RegisterActionOnGetStats((ae, stat_id) =>
        {
            var items = ae.GetItems().Where(x => x.HasComponent<Elixir>() && x.Tags.Has<TagElixirStatusGain>());
            Entity elixir_entity = default;
            foreach (var item in items)
            {
                if (item.HasComponent<StatusOverwriteStats>())
                {
                    if (item.GetComponent<StatusOverwriteStats>().stats[stat_id] > 0)
                    {
                        elixir_entity = item;
                        break;
                    }
                }
                else if (item.HasComponent<StatusComponent>())
                {
                    if (item.GetComponent<StatusComponent>().Type.stats[stat_id] > 0)
                    {
                        elixir_entity = item;
                        break;
                    }
                }
            }

            if (elixir_entity.IsNull) return;
            if (ae.TryConsumeElixir(elixir_entity))
            {
                ae.Base.setStatsDirty();
                ae.Base.updateStats();
            }
        });
    }

    public ElixirAsset NewElixir(bool dynamic = true)
    {
        ElixirAsset asset = new()
        {
            id = Guid.NewGuid().ToString()
        };
        if (dynamic)
            add_dynamic(asset);
        else
            add(asset);

        return asset;
    }

    public ElixirAsset GetRandom()
    {
        return list.GetRandom();
    }
}