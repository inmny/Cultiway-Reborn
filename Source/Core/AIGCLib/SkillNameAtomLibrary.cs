using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Core.AIGCLib;

public class SkillNameAtomLibrary : AssetLibrary<SkillNameAtomAsset>
{
    internal IEnumerable<SkillNameAtomAsset> All => list;

    internal string ResolveElementTag(SkillEntityAsset asset)
    {
        var atom = SelectBestContextAtom(SkillNameAtomCategory.Element, candidate => candidate.ScoreElement(asset));
        return atom == null ? SkillTags.Element.Generic : atom.tag;
    }

    internal string ResolveFormTag(SkillEntityAsset asset)
    {
        var atom = SelectBestContextAtom(SkillNameAtomCategory.Form, candidate => candidate.ScoreForm(asset));
        return atom == null ? SkillTags.Form.Spell : atom.tag;
    }

    internal string ResolveMotionTag(SkillEntityAsset asset, string trajectoryId)
    {
        var atom = SelectBestContextAtom(SkillNameAtomCategory.Motion, candidate => candidate.ScoreMotion(asset, trajectoryId));
        return atom == null ? string.Empty : atom.tag;
    }

    internal SkillNameAtomAsset CreateFallbackModifierAtom(SkillNamingModifier modifier)
    {
        var stem = TrimKnownSuffix(modifier.LocalizedName, "词条", "效果", "术");
        if (string.IsNullOrEmpty(stem)) stem = "玄";
        if (stem.Length > 2) stem = stem.Substring(0, 2);

        return new SkillNameAtomAsset
        {
            id = "Cultiway.SkillNameAtom.Fallback",
            tag = SkillTags.Modifier.Fallback,
            category = SkillNameAtomCategory.Modifier,
            name_stems = [stem],
            pattern = "{modifier}{core}",
            modifier_patterns = ["{modifier}{core}", "{modifier}{form}"],
            secondary_patterns = ["{modifier}{secondary}{core}", "{secondary}{modifier}{core}"],
            ending_stems = ["术", "法"],
            priority = (int)modifier.Rarity * 100,
            ScoreModifier = _ => 1f
        };
    }

    private SkillNameAtomAsset SelectBestContextAtom(SkillNameAtomCategory category,
        Func<SkillNameAtomAsset, float> score)
    {
        var selected = All
            .Where(atom => atom.category == category)
            .Select(atom => new { Atom = atom, Score = score(atom) })
            .Where(x => x.Score > 0f)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Atom.priority)
            .FirstOrDefault();
        return selected == null ? null : selected.Atom;
    }

    private static string TrimKnownSuffix(string value, params string[] suffixes)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return value.Substring(0, value.Length - suffix.Length);
            }
        }

        return value;
    }
}
