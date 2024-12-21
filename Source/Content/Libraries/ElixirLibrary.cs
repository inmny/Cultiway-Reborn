using System;
using Cultiway.Abstract;
using Cultiway.Content.CultisysComponents;
using Cultiway.UI.Prefab;
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
}