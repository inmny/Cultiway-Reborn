using System;
using Cultiway.Abstract;

namespace Cultiway.Content.Libraries;

public class ElixirLibrary : DynamicAssetLibrary<ElixirAsset>
{
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