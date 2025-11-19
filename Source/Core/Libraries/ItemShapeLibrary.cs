namespace Cultiway.Core.Libraries;

public class ItemShapeLibrary : AssetLibrary<ItemShapeAsset>
{
    public ItemShapeAsset GetOrDefault(string key, ItemShapeAsset default_value)
    {
        if (string.IsNullOrEmpty(key)) return default_value;
        return get(key) ?? default_value;
    }
}