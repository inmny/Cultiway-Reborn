using Cultiway.Content.Components;
using Cultiway.Core;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;
public delegate bool JindanCheck(ActorExtend ae, ref XianBase xian_base);
public delegate float JindanScore(ActorExtend ae, ref XianBase xian_base);
public class JindanAsset : Asset
{
    private JindanGroupAsset _group;
    public JindanCheck check;
    public JindanScore score;
    public string wrapped_skill_id;
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

    public string GetDescription()
    {
        return LM.Get($"{id}.Info");
    }
}