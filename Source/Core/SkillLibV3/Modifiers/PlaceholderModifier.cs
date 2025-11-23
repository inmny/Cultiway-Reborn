using System.Collections.Generic;
using System.Linq;
using Cultiway;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Modifiers;

public struct PlaceholderModifier : IModifier
{
    public const string PlaceholderAssetId = "Cultiway.SkillModifier.PlaceholderAggregator";
    public HashSet<string> ModifierAssetIds;

    public SkillModifierAsset ModifierAsset => ModClass.I.SkillV3.ModifierLib.get(PlaceholderAssetId);

    public string GetKey()
    {
        return ModifierAsset.id.Localize();
    }

    public string GetValue()
    {
        if (ModifierAssetIds == null || ModifierAssetIds.Count == 0) return string.Empty;
        return string.Join("、", ModifierAssetIds.Select(id => id.Localize()));
    }
}
