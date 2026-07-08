using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Core.AIGCLib;

internal sealed class SkillNamingContext
{
    public string Signature;
    public string StoreKey;
    public string BaseName;
    public string ElementTag;
    public string FormTag;
    public string MotionTag;
    public string TrajectoryId;
    public SkillEntityAsset Asset;
    public readonly List<SkillNamingModifier> Modifiers = new();
}

internal sealed class SkillNamingModifier
{
    public string Id;
    public string Kind;
    public string LocalizedName;
    public string Value;
    public int ValueTier;
    public SkillModifierRarity Rarity;
    public HashSet<string> SimilarityTags = new(StringComparer.Ordinal);
}
