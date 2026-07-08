using System;
using System.Linq;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.AIGCLib;

public enum SkillNameAtomCategory
{
    Element,
    Form,
    Motion,
    Modifier
}

public class SkillNameAtomAsset : Asset
{
    public string tag;
    public SkillNameAtomCategory category;
    public string[] name_stems = [];
    public string[] series_tags = [];
    public string[] trajectory_suffixes = [];
    public string pattern;
    public string core_pattern;
    public string[] core_patterns = [];
    public string[] modifier_patterns = [];
    public string[] secondary_patterns = [];
    public string[] ending_stems = [];
    public int element_index = -1;
    public int priority;
    public bool allow_secondary;
    public SkillModifierRarity min_rarity = SkillModifierRarity.Common;
    internal Func<SkillNamingContext, float> ScoreContext;
    internal Func<SkillNamingModifier, float> ScoreModifier;

    internal float ScoreFor(SkillNamingContext context)
    {
        return Math.Max(0f, ScoreContext(context));
    }

    internal float ScoreFor(SkillNamingModifier modifier)
    {
        if (category == SkillNameAtomCategory.Modifier && modifier.Rarity < min_rarity) return 0f;
        return Math.Max(0f, ScoreModifier(modifier));
    }

    internal float ScoreElement(SkillEntityAsset asset)
    {
        var score = ScoreSeriesTags(asset) * 2f;
        if (element_index >= ElementIndex.Iron && DominantElementIndex(asset) == element_index)
        {
            score += 8f;
        }

        return score;
    }

    internal float ScoreForm(SkillEntityAsset asset)
    {
        return ScoreSeriesTags(asset) * 2f;
    }

    internal float ScoreMotion(SkillEntityAsset asset, string trajectoryId)
    {
        var score = ScoreSeriesTags(asset) * 1.5f;
        if (!string.IsNullOrEmpty(trajectoryId))
        {
            for (var i = 0; i < trajectory_suffixes.Length; i++)
            {
                if (trajectoryId.EndsWith(trajectory_suffixes[i], StringComparison.Ordinal))
                {
                    score += 12f;
                }
            }
        }

        return score;
    }

    public string PickNameStem(int seed)
    {
        return PickFrom(name_stems, seed);
    }

    public string PickEndingStem(int seed)
    {
        return PickFrom(ending_stems, seed);
    }

    private static string PickFrom(string[] values, int seed)
    {
        if (values.Length == 0) return string.Empty;
        return values[(seed & int.MaxValue) % values.Length];
    }

    private float ScoreSeriesTags(SkillEntityAsset asset)
    {
        if (series_tags.Length == 0) return 0f;
        return series_tags.Any(tag => asset.SeriesTags.Contains(tag)) ? 6f : 0f;
    }

    private static int DominantElementIndex(SkillEntityAsset asset)
    {
        var values = asset.Element.AsArray();
        if (values.Length == 0) return -1;

        var maxIndex = -1;
        var maxValue = float.MinValue;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] <= maxValue) continue;
            maxValue = values[i];
            maxIndex = i;
        }

        return maxValue <= 0f ? -1 : maxIndex;
    }
}
