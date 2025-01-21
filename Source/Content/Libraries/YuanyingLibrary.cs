using System.Collections.Generic;

namespace Cultiway.Content.Libraries;

public class YuanyingLibrary : AssetLibrary<YuanyingAsset>
{
    private Dictionary<JindanAsset, List<YuanyingAsset>> _map = new();

    public YuanyingAsset GetRandomYuanying(JindanAsset jindan)
    {
        if (!_map.TryGetValue(jindan, out var yuanyings) || yuanyings.Count == 0)
        {
            return list.GetRandom();
        }
        return yuanyings.GetRandom();
    }
    public void Map(YuanyingAsset yuanying, JindanAsset jindan)
    {
        if (!_map.ContainsKey(jindan))
        {
            _map[jindan] = new List<YuanyingAsset>();
        }
        _map[jindan].Add(yuanying);
    }
}