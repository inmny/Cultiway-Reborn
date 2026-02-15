using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class ElementRootAsset : Asset
{
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
}