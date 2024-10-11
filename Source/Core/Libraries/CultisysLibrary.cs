namespace Cultiway.Core.Libraries;

public class CultisysLibrary : AssetLibrary<BaseCultisysAsset>
{
    public override void post_init()
    {
        foreach (var cultisys in list)
        {
            cultisys.UpdateAccumStats();
        }
    }
}