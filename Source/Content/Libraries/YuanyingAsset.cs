using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class YuanyingAsset : Asset
{
    public ElementComposition composition;
    public List<SkillEntityAsset> skills = new();
    public List<float> skill_acc_weight = new();

    public string GetName()
    {
        return LM.Get(id);
    }

    public string GetDescription()
    {
        return LM.Get($"{id}.Info");
    }

    public override string ToString()
    {
        return id;
    }
}