using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.Semantics;

namespace Cultiway.Core.AIGCLib;

internal sealed class SkillNamingContext
{
    public string Signature;
    public string StoreKey;
    public string BaseName;
    public SemanticAsset ElementSemantic;
    public SemanticAsset FormSemantic;
    public SemanticAsset MotionSemantic;
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
    public HashSet<SemanticAsset> Semantics = new();
}
