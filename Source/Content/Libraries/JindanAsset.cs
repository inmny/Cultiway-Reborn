using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class JindanAsset : Asset
{
    private JindanGroupAsset _group;

    public JindanGroupAsset Group
    {
        get => _group;
        set
        {
            _group?.jindans.Remove(this);
            _group = value;
            _group.jindans.Add(this);
        }
    }

    public string GetName()
    {
        return LM.Get(id);
    }
}