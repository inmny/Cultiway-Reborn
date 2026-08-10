using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Abstract;

public interface IDynamicAssetLibrary
{
    public void ClearDynamicAssets();
    public IEnumerable<Asset> GetDynamicAssets();
    public void RemoveAll(IEnumerable<string> ids);
}
public class DynamicAssetLibrary<T> : AssetLibrary<T>, IDynamicAssetLibrary where T : Asset
{
    protected readonly Dictionary<string, T> DynamicDict = new();

    public virtual T AddDynamic(T asset)
    {
        DynamicDict[asset.id] = asset;
        add(asset);
        return asset;
    }

    public void ClearDynamicAssets()
    {
        RemoveAll(DynamicDict.Keys.ToArray());
    }

    public IEnumerable<Asset> GetDynamicAssets()
    {
        foreach (var asset in DynamicDict.Values)
        {
            yield return asset;
        }
        yield break;
    }

    public void RemoveAll(IEnumerable<string> ids)
    {
        foreach (var asset_id in ids)
        {
            if (DynamicDict.TryGetValue(asset_id, out var asset))
            {
                OnRemoveDynamic(asset);
            }
            DynamicDict.Remove(asset_id);
            dict.Remove(asset_id);
        }
        list.RemoveAll(x => !dict.ContainsKey(x.id));
    }

    protected virtual void OnRemoveDynamic(T asset)
    {
        if (asset is IDeleteWhenUnknown deleteWhenUnknown)
        {
            deleteWhenUnknown.OnDelete();
        }
    }
}
