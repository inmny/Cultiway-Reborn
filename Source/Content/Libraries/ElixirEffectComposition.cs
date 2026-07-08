using System.Collections.Generic;

namespace Cultiway.Content.Libraries;

public sealed class ElixirEffectComposition
{
    public ElixirEffectAtomAsset[] Atoms = [];
    public string Name;
    public string Description;
    public Dictionary<string, float> StatusStats = new();
    public string DataGainChosen = "attribute";
    public Dictionary<string, float> DataAttributes = new();
    public Dictionary<string, float> FallbackAttributes = new();
    public List<string> DataTraits = new();
    public List<string> DataOperations = new();
    public Dictionary<string, string> OperationArgs;
}
