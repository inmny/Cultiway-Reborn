using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

public class ElementRootAsset : Asset
{
    public readonly BaseStats base_stats = new();
    public readonly ElementComposition composition;


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