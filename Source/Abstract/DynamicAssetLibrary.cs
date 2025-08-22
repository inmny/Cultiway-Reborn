using System.Collections.Generic;

namespace Cultiway.Abstract;

public interface IDynamicAssetLibrary
{
    public void ClearDynamicAssets();
}
public class DynamicAssetLibrary<T> : AssetLibrary<T>, IDynamicAssetLibrary where T : Asset
{
    protected Dictionary<string, T> dynamic_dict = new();

    public virtual T add_dynamic(T asset)
    {
        dynamic_dict[asset.id] = asset;
        add(asset);
        return asset;
    }

    public void ClearDynamicAssets()
    {
        list.RemoveAll(x => dynamic_dict.ContainsKey(x.id));
        dynamic_dict.Clear();
    }
}