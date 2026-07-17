using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.Semantics;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public class YuanyingAsset : Asset
{
    /// <summary>该元婴类型除元素组成外的稳定语义。</summary>
    public SemanticDescriptor Semantics = new();

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
