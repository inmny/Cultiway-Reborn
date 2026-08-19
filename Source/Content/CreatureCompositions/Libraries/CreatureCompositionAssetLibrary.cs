using System;

namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>组合生灵资源库共用的编号登记规则。</summary>
public abstract class CreatureCompositionAssetLibrary<TAsset> : AssetLibrary<TAsset> where TAsset : Asset
{
    public override TAsset add(TAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (string.IsNullOrWhiteSpace(asset.id))
            throw new InvalidOperationException($"{typeof(TAsset).Name} 缺少资源编号");
        if (has(asset.id))
            throw new InvalidOperationException($"{typeof(TAsset).Name} 重复登记: {asset.id}");
        return base.add(asset);
    }
}
