using System.Collections.Generic;
using Cultiway.Content.Const;
using Cultiway.Core.Libraries;

namespace Cultiway.Content.Libraries;

public sealed class ElixirEffectComposition
{
    public ElixirEffectType EffectType;
    public ElixirEffectAtomAsset[] Atoms = [];
    public string Name;
    public string Description;
    public Dictionary<string, float> StatusStats = new();
    public ElixirDataGainKind DataGainKind;
    public Dictionary<string, float> DataAttributes = new();
    public Dictionary<string, float> FallbackAttributes = new();
    public List<string> DataTraits = new();
    public List<OperationAsset> DataOperations = new();
    public Dictionary<string, string> OperationArgs = new();
}
