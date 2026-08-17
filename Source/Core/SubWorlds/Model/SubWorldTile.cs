namespace Cultiway.Core.SubWorlds.Model;

/// <summary>
/// 保存一个小世界格子对原版 Main 和 Top terrain Asset 的引用。
/// </summary>
internal struct SubWorldTile
{
    /// <summary>必需的原版 <see cref="TileType"/> Asset ID。</summary>
    public string MainAssetId;

    /// <summary>可选的原版 <see cref="TopTileType"/> Asset ID；空值表示没有 Top terrain。</summary>
    public string TopAssetId;

    /// <summary>
    /// 创建格子资产引用。
    /// </summary>
    /// <param name="mainAssetId">Main terrain Asset ID。</param>
    /// <param name="topAssetId">可选的 Top terrain Asset ID。</param>
    public SubWorldTile(string mainAssetId, string topAssetId = null)
    {
        MainAssetId = mainAssetId;
        TopAssetId = topAssetId;
    }
}
