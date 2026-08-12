namespace Cultiway.Core.SubWorlds.Model;

internal struct SubWorldTile
{
    public string MainAssetId;
    public string TopAssetId;

    public SubWorldTile(string mainAssetId, string topAssetId = null)
    {
        MainAssetId = mainAssetId;
        TopAssetId = topAssetId;
    }
}
