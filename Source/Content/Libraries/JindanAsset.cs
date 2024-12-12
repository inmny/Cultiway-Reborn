using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class JindanAsset : Asset
{
    public enum Type
    {
        None,
        Element,
        Special,
        External
    }

    public Type type = Type.None;

    public string GetName()
    {
        return LM.Get(id);
    }
}