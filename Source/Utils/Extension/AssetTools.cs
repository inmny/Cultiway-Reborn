using System.Collections.Generic;

namespace Cultiway.Utils.Extension;

public static class AssetTools
{
    public static TExtend GetExtend<TExtend>(this Asset asset) where TExtend : class, new()
    {
        return AssetExtendWrapper<Asset, TExtend>.GetExtend(asset);
    }

    private static class AssetExtendWrapper<TAsset, TExtend> where TAsset : Asset where TExtend : class, new()
    {
        private static readonly Dictionary<TAsset, TExtend> _extends = new();

        public static TExtend GetExtend(TAsset asset)
        {
            if (!_extends.TryGetValue(asset, out TExtend extend))
            {
                extend = new TExtend();
                _extends.Add(asset, extend);
            }

            return extend;
        }
    }

    public static void AddToPool(this CombatActionAsset asset, ListPool<CombatActionAsset> pool)
    {
        for (int i = 0; i < asset.rate; i++)
        {
            pool.Add(asset);
        }
    }
}