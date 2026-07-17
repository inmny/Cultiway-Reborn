using NeoModLoader.General;
using Cultiway.Core.Semantics;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class ElementRootAsset : Asset
{
    /// <summary>灵根类型稳定表达的先天语义；实际元素比例由组件数值贡献。</summary>
    public SemanticDescriptor Semantics = new();

    public readonly BaseStats base_stats = new();
    public readonly ElementComposition composition;
    public string icon_path;
    public Sprite GetSprite()
    {
        return SpriteTextureLoader.getSprite($"cultiway/icons/element_root/{icon_path}");
    }

    private string f_desc_key;
    private string f_name_key;

    public ElementRootAsset(string id, ElementComposition composition)
    {
        this.id = id;
        this.composition = composition;
    }

    private string name_key => f_name_key ??= $"Cultiway.ER.{id}";
    private string desc_key => f_desc_key ??= $"Cultiway.ER.{id}.Info";

    public override string ToString()
    {
        return id;
    }

    public string GetName()
    {
        return LM.Get(name_key);
    }

    public string GetDescription()
    {
        return LM.Get(desc_key);
    }

    /// <summary>
    ///     按修炼体系风格返回元素根名字。cultisys 为 null 或其风格未配置前缀时回退到默认（仙道 Cultiway.ER）。
    /// </summary>
    public string GetName(BaseCultisysAsset cultisys)
    {
        var prefix = cultisys?.DisplayStyle?.element_root_name_prefix;
        if (string.IsNullOrEmpty(prefix)) return GetName();
        return LM.Get($"{prefix}.{id}");
    }

    /// <summary>
    ///     按修炼体系风格返回元素根描述。cultisys 为 null 或其风格未配置前缀时回退到默认（仙道 Cultiway.ER）。
    /// </summary>
    public string GetDescription(BaseCultisysAsset cultisys)
    {
        var prefix = cultisys?.DisplayStyle?.element_root_desc_prefix;
        if (string.IsNullOrEmpty(prefix)) return GetDescription();
        return LM.Get($"{prefix}.{id}.Info");
    }
}
