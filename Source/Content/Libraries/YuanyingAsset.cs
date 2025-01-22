using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class YuanyingAsset : Asset
{
    public string GetName()
    {
        return LM.Get(id);
    }

    public string GetDescription()
    {
        return LM.Get($"{id}.Info");
    }
}