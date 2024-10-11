using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

public class ElementRootAsset : Asset
{
    public readonly BaseStats          base_stats;
    public readonly ElementComposition composition;
    private         string             desc_key;

    private string name_key;

    public ElementRootAsset(string id, ElementComposition composition)
    {
        this.id = id;
        this.composition = composition;

        name_key = $"er_{id}";
        desc_key = $"er_info_{id}";

        base_stats = new();
    }

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