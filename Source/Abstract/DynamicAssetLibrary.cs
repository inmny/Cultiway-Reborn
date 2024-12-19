using System.Collections.Generic;

namespace Cultiway.Abstract;

public class DynamicAssetLibrary<T> : AssetLibrary<T> where T : Asset
{
    protected Dictionary<string, T> dynamic_dict = new();

    public virtual T add_dynamic(T asset)
    {
        dynamic_dict[asset.id] = asset;
        add(asset);
        return asset;
    }
}