namespace Cultiway.Core;

public class NameSetAssetExtend
{
    public NameSetAssetExtend()
    {
        Sect = WorldboxGame.NameGenerators.Sect.id;
        ModClass.LogInfo($"({nameof(NameSetAssetExtend)}) Initializes with sect: {Sect}");
    }

    public string Sect;
}