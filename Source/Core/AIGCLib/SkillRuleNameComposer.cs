using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Core.AIGCLib;

internal static class SkillRuleNameComposer
{
    private const int MaxNameLength = 6;

    public static SkillNamingContext CreateContext(Entity skillContainerEntity)
    {
        if (skillContainerEntity.IsNull || !skillContainerEntity.HasComponent<SkillContainer>()) return null;

        var container = skillContainerEntity.GetComponent<SkillContainer>();
        var asset = container.Asset;
        var signature = SkillContainerSignature.Build(skillContainerEntity);
        var trajectoryId = ResolveTrajectoryId(skillContainerEntity);
        var atomLibrary = ModClass.L.SkillNameAtomLibrary;
        var context = new SkillNamingContext
        {
            Signature = signature,
            StoreKey = $"skill-name-v3|{signature}",
            Asset = asset,
            BaseName = TrimKnownSuffix(LMTools.GetOrKey(asset.id), "术"),
            ElementTag = atomLibrary.ResolveElementTag(asset),
            FormTag = atomLibrary.ResolveFormTag(asset),
            MotionTag = atomLibrary.ResolveMotionTag(asset, trajectoryId),
            TrajectoryId = trajectoryId
        };

        foreach (var modifier in CollectModifiers(skillContainerEntity))
        {
            var namingModifier = CreateNamingModifier(modifier);
            if (namingModifier != null)
            {
                context.Modifiers.Add(namingModifier);
            }
        }

        return context;
    }

    public static string Compose(SkillNamingContext context)
    {
        if (context == null) return "法术";

        var seed = NamingRuleUtils.StableHash(context.Signature);
        var element = SelectContextAtom(context, SkillNameAtomCategory.Element);
        var form = SelectContextAtom(context, SkillNameAtomCategory.Form);
        var motion = SelectContextAtom(context, SkillNameAtomCategory.Motion);
        var coreCandidates = BuildCoreCandidates(context, element, form, motion, seed);

        var selectedModifiers = SelectModifierAtoms(context)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Atom.priority)
            .ToArray();
        if (selectedModifiers.Length == 0)
        {
            return PickCandidate(coreCandidates, context);
        }

        var primary = selectedModifiers[0];
        var secondary = PickSecondary(selectedModifiers, primary);
        var candidates = BuildModifierCandidates(context, element, form, motion, coreCandidates, primary, secondary, seed);
        return PickCandidate(candidates, context);
    }

    public static string ComposeFallback(string[] param)
    {
        if (param == null || param.Length == 0 || string.IsNullOrWhiteSpace(param[0])) return "法术";
        return FinalizeName(TrimKnownSuffix(param[0], "术"));
    }

    public static string BuildPrompt(SkillNamingContext context)
    {
        if (context == null) return "为未知法术生成一个简洁名称";

        var parts = new List<string>
        {
            $"本体={context.BaseName}",
            $"元素={context.ElementTag}",
            $"形态={context.FormTag}"
        };

        if (!string.IsNullOrEmpty(context.MotionTag))
        {
            parts.Add($"轨迹={context.MotionTag}");
        }

        foreach (var modifier in context.Modifiers)
        {
            parts.Add($"{modifier.LocalizedName}={modifier.Value}");
        }

        return $"为这个法术生成一个中文名，要求2到6个字、玄幻风格、不要标点。{string.Join("；", parts)}";
    }

    internal static string TrimKnownSuffix(string value, params string[] suffixes)
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

    private static List<NameCandidate> BuildCoreCandidates(SkillNamingContext context, SkillNameAtomAsset element,
        SkillNameAtomAsset form, SkillNameAtomAsset motion, int seed)
    {
        var candidates = new List<NameCandidate>();
        var endings = CollectEndings(null, form);
        var hasModifier = context.Modifiers.Count > 0;

        if (context.Modifiers.Count == 0 && !string.IsNullOrEmpty(context.BaseName))
        {
            AddCandidate(candidates, context.BaseName, 82f, NameCandidateFeatures.Base);
        }

        if (!string.IsNullOrEmpty(context.BaseName) && context.BaseName.Length <= 2)
        {
            AddCandidate(candidates, context.BaseName, hasModifier ? 24f : 74f, NameCandidateFeatures.Base);
        }

        foreach (var ending in endings)
        {
            var values = CreatePatternValues(context, element, form, motion, null, null, string.Empty, ending, seed);
            foreach (var pattern in CollectCorePatterns(element, form, motion))
            {
                var features = NameCandidateFeatures.Element | NameCandidateFeatures.Form;
                if (PatternUses(pattern, "motion")) features |= NameCandidateFeatures.Motion;
                if (PatternUses(pattern, "base")) features |= NameCandidateFeatures.Base;
                if (PatternUses(pattern, "ending")) features |= NameCandidateFeatures.Ending;
                var score = hasModifier && PatternUses(pattern, "base") ? 28f : 50f;

                AddCandidate(candidates, ApplyPattern(pattern, values), score, features);
            }
        }

        var elementStem = element.PickNameStem(seed);
        var formStem = form.PickNameStem(seed / 3 + 11);
        AddCandidate(candidates, elementStem + formStem, 46f, NameCandidateFeatures.Element | NameCandidateFeatures.Form);
        AddEndingVariants(candidates, elementStem, endings, 38f, NameCandidateFeatures.Element);

        if (motion != null)
        {
            var motionStem = motion.PickNameStem(seed / 5 + 23);
            AddCandidate(candidates, elementStem + motionStem, 44f,
                NameCandidateFeatures.Element | NameCandidateFeatures.Motion);
            AddCandidate(candidates, motionStem + formStem, 42f,
                NameCandidateFeatures.Motion | NameCandidateFeatures.Form);
        }

        return candidates;
    }

    private static List<NameCandidate> BuildModifierCandidates(SkillNamingContext context, SkillNameAtomAsset element,
        SkillNameAtomAsset form, SkillNameAtomAsset motion, List<NameCandidate> coreCandidates,
        SelectedModifierAtom primary, SelectedModifierAtom secondary, int seed)
    {
        var candidates = new List<NameCandidate>();
        var endings = CollectEndings(primary, form);
        var patterns = CollectModifierPatterns(primary, secondary);

        foreach (var coreCandidate in coreCandidates.OrderByDescending(x => x.Score).Take(8))
        {
            foreach (var ending in endings)
            {
                var values = CreatePatternValues(context, element, form, motion, primary, secondary,
                    coreCandidate.Name, ending, seed);
                foreach (var pattern in patterns)
                {
                    var features = NameCandidateFeatures.PrimaryModifier;
                    var score = 34f + primary.Score / 20f;
                    if (PatternUses(pattern, "core"))
                    {
                        features |= coreCandidate.Features;
                        score += coreCandidate.Score;
                    }
                    else
                    {
                        score += 18f;
                    }

                    if (PatternUses(pattern, "element")) features |= NameCandidateFeatures.Element;
                    if (PatternUses(pattern, "form")) features |= NameCandidateFeatures.Form;
                    if (PatternUses(pattern, "base")) features |= NameCandidateFeatures.Base;
                    if (PatternUses(pattern, "secondary") && secondary != null)
                    {
                        features |= NameCandidateFeatures.SecondaryModifier;
                        score += 12f + secondary.Score / 40f;
                    }
                    if (pattern.StartsWith("{secondary}", StringComparison.Ordinal)) score -= 10f;

                    if (PatternUses(pattern, "motion")) features |= NameCandidateFeatures.Motion;
                    if (PatternUses(pattern, "ending")) features |= NameCandidateFeatures.Ending;

                    AddCandidate(candidates, ApplyPattern(pattern, values), score, features);
                }
            }

            AddEndingVariants(candidates, primary.Atom.PickNameStem(seed) + coreCandidate.Name, endings,
                24f + coreCandidate.Score + primary.Score / 25f,
                coreCandidate.Features | NameCandidateFeatures.PrimaryModifier);
        }

        return candidates;
    }

    private static SkillNameAtomAsset SelectContextAtom(SkillNamingContext context, SkillNameAtomCategory category)
    {
        var selected = ModClass.L.SkillNameAtomLibrary.All
            .Where(atom => atom.category == category)
            .Select(atom => new { Atom = atom, Score = atom.ScoreFor(context) + TieBreak(context.Signature, atom.id) })
            .Where(x => x.Score > 0f)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Atom.priority)
            .FirstOrDefault();
        return selected == null ? null : selected.Atom;
    }

    private static IEnumerable<SelectedModifierAtom> SelectModifierAtoms(SkillNamingContext context)
    {
        var library = ModClass.L.SkillNameAtomLibrary;
        foreach (var modifier in context.Modifiers)
        {
            var selected = library.All
                .Where(candidate => candidate.category == SkillNameAtomCategory.Modifier)
                .Select(candidate => new
                {
                    Atom = candidate,
                    Score = candidate.ScoreFor(modifier) + TieBreak(context.Signature, candidate.id)
                })
                .Where(x => x.Score > 0f)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();
            var atom = selected == null ? library.CreateFallbackModifierAtom(modifier) : selected.Atom;

            yield return new SelectedModifierAtom
            {
                Modifier = modifier,
                Atom = atom,
                Score = atom.priority + (int)modifier.Rarity * 100 + modifier.ValueTier
            };
        }
    }

    private static SelectedModifierAtom PickSecondary(SelectedModifierAtom[] selected, SelectedModifierAtom primary)
    {
        if (!primary.Atom.allow_secondary && primary.Modifier.Rarity >= SkillModifierRarity.Epic) return null;

        foreach (var candidate in selected.OrderByDescending(x => x.Score))
        {
            if (candidate == primary) continue;
            if (candidate.Atom.tag == primary.Atom.tag) continue;
            return candidate;
        }

        return null;
    }

    private static List<string> CollectCorePatterns(SkillNameAtomAsset element, SkillNameAtomAsset form,
        SkillNameAtomAsset motion)
    {
        var patterns = new List<string>();
        AddPatterns(patterns, element.core_patterns);
        AddPattern(patterns, element.core_pattern);
        AddPatterns(patterns, form.core_patterns);
        AddPattern(patterns, form.core_pattern);
        if (motion != null)
        {
            AddPatterns(patterns, motion.core_patterns);
            AddPattern(patterns, motion.core_pattern);
        }

        AddPattern(patterns, "{element}{form}");
        return patterns;
    }

    private static List<string> CollectModifierPatterns(SelectedModifierAtom primary, SelectedModifierAtom secondary)
    {
        var patterns = new List<string>();
        AddPatterns(patterns, primary.Atom.modifier_patterns);
        AddPattern(patterns, primary.Atom.pattern);
        if (secondary != null)
        {
            AddPatterns(patterns, primary.Atom.secondary_patterns);
            AddPattern(patterns, "{modifier}{secondary}{core}");
        }

        AddPattern(patterns, "{modifier}{core}");
        return patterns;
    }

    private static string[] CollectEndings(SelectedModifierAtom primary, SkillNameAtomAsset form)
    {
        var endings = new List<string> { string.Empty };
        if (primary != null)
        {
            AddWords(endings, primary.Atom.ending_stems);
        }

        AddWords(endings, form.ending_stems);
        return endings.ToArray();
    }

    private static void AddPatterns(List<string> patterns, string[] values)
    {
        foreach (var value in values)
        {
            AddPattern(patterns, value);
        }
    }

    private static void AddPattern(List<string> patterns, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (patterns.Contains(value)) return;
        patterns.Add(value);
    }

    private static void AddWords(List<string> words, string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            if (words.Contains(value)) continue;
            words.Add(value);
        }
    }

    private static Dictionary<string, string> CreatePatternValues(SkillNamingContext context,
        SkillNameAtomAsset element, SkillNameAtomAsset form, SkillNameAtomAsset motion,
        SelectedModifierAtom primary, SelectedModifierAtom secondary, string core, string ending, int seed)
    {
        return new Dictionary<string, string>
        {
            ["base"] = context.BaseName,
            ["element"] = element.PickNameStem(seed),
            ["form"] = form.PickNameStem(seed / 3 + 11),
            ["motion"] = motion == null ? string.Empty : motion.PickNameStem(seed / 5 + 23),
            ["modifier"] = primary == null ? string.Empty : primary.Atom.PickNameStem(seed / 7 + 31),
            ["secondary"] = secondary == null ? string.Empty : secondary.Atom.PickNameStem(seed / 11 + 47),
            ["core"] = core,
            ["ending"] = ending
        };
    }

    private static string ApplyPattern(string pattern, Dictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(pattern)) pattern = "{modifier}{core}";
        var result = pattern;
        foreach (var kv in values)
        {
            result = result.Replace("{" + kv.Key + "}", kv.Value);
        }

        return result;
    }

    private static bool PatternUses(string pattern, string key)
    {
        return pattern.IndexOf("{" + key + "}", StringComparison.Ordinal) >= 0;
    }

    private static void AddEndingVariants(List<NameCandidate> candidates, string name, string[] endings,
        float score, NameCandidateFeatures features)
    {
        if (EndsWithAny(name, endings)) return;
        foreach (var ending in endings)
        {
            if (string.IsNullOrEmpty(ending)) continue;
            AddCandidate(candidates, name + ending, score, features | NameCandidateFeatures.Ending);
        }
    }

    private static bool EndsWithAny(string name, string[] endings)
    {
        foreach (var ending in endings)
        {
            if (string.IsNullOrEmpty(ending)) continue;
            if (name.EndsWith(ending, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static void AddCandidate(List<NameCandidate> candidates, string rawName, float baseScore,
        NameCandidateFeatures features)
    {
        var name = NormalizeName(rawName);
        if (string.IsNullOrEmpty(name)) return;
        if (name.IndexOf('{') >= 0 || name.IndexOf('}') >= 0) return;
        if (name.Length < 2) return;
        if (name.Length > MaxNameLength) return;

        var score = baseScore + ScoreName(name, features);
        if (score <= 0f) return;
        candidates.Add(new NameCandidate
        {
            Name = name,
            Score = score,
            Features = features
        });
    }

    private static float ScoreName(string name, NameCandidateFeatures features)
    {
        var score = name.Length switch
        {
            2 => 12f,
            3 => 16f,
            4 => 18f,
            5 => 14f,
            6 => 10f,
            _ => -40f
        };

        if (HasAdjacentDuplicate(name)) score -= 18f;
        if ((features & NameCandidateFeatures.PrimaryModifier) != 0) score += 18f;
        if ((features & NameCandidateFeatures.SecondaryModifier) != 0) score += 12f;
        if ((features & NameCandidateFeatures.Element) != 0) score += 4f;
        if ((features & NameCandidateFeatures.Form) != 0) score += 4f;
        if ((features & NameCandidateFeatures.Motion) != 0) score += 5f;
        if ((features & NameCandidateFeatures.Ending) != 0) score += 3f;
        if ((features & NameCandidateFeatures.Base) != 0) score += 2f;
        return score;
    }

    private static bool HasAdjacentDuplicate(string name)
    {
        for (var i = 1; i < name.Length; i++)
        {
            if (name[i] == name[i - 1]) return true;
        }

        return false;
    }

    private static string PickCandidate(List<NameCandidate> candidates, SkillNamingContext context)
    {
        var distinct = candidates
            .GroupBy(candidate => candidate.Name)
            .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();
        if (distinct.Length == 0) return "法术";

        var bestScore = distinct[0].Score;
        var pool = distinct
            .Where(candidate => candidate.Score >= bestScore - 8f)
            .Take(5)
            .ToArray();
        var index = PositiveMod(NamingRuleUtils.StableHash($"{context.Signature}|name-candidate"), pool.Length);
        return pool[index].Name;
    }

    private static int PositiveMod(int value, int divisor)
    {
        return (value & int.MaxValue) % divisor;
    }

    private static SkillNamingModifier CreateNamingModifier(IModifier modifier)
    {
        var asset = modifier.ModifierAsset;
        if (asset == null || string.IsNullOrEmpty(asset.id)) return null;

        var kind = GetKind(asset.id);
        if (kind == "Placeholder") return null;

        return new SkillNamingModifier
        {
            Id = asset.id,
            Kind = kind,
            LocalizedName = LMTools.GetOrKey(asset.id),
            Value = SafeGetValue(modifier),
            ValueTier = GetValueTier(modifier),
            Rarity = asset.Rarity,
            SimilarityTags = new HashSet<string>(asset.SimilarityTags ?? [], StringComparer.Ordinal)
        };
    }

    private static IEnumerable<IModifier> CollectModifiers(Entity skillContainerEntity)
    {
        var modifierTypes = skillContainerEntity.GetComponentTypes()
            .Where(type => typeof(IModifier).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var type in modifierTypes)
        {
            yield return (IModifier)skillContainerEntity.GetComponent(type);
        }
    }

    private static string ResolveTrajectoryId(Entity skillContainerEntity)
    {
        if (skillContainerEntity.TryGetComponent(out Trajectory trajectory))
        {
            return trajectory.ID;
        }

        return string.Empty;
    }

    private static string SafeGetValue(IModifier modifier)
    {
        try
        {
            return modifier.GetValue();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int GetValueTier(IModifier modifier)
    {
        var tier = 0;
        foreach (var field in modifier.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var value = field.GetValue(modifier);
            switch (value)
            {
                case int i:
                    if (i >= 8) tier = Math.Max(tier, 3);
                    else if (i >= 4) tier = Math.Max(tier, 2);
                    else if (i >= 2) tier = Math.Max(tier, 1);
                    break;
                case float f:
                    var abs = Math.Abs(f);
                    if (abs >= 2.5f) tier = Math.Max(tier, 3);
                    else if (abs >= 1.5f) tier = Math.Max(tier, 2);
                    else if (abs >= 0.75f) tier = Math.Max(tier, 1);
                    break;
            }
        }

        return tier;
    }

    private static string GetKind(string assetId)
    {
        if (string.IsNullOrEmpty(assetId)) return string.Empty;
        var idx = assetId.LastIndexOf('.');
        return idx >= 0 && idx < assetId.Length - 1 ? assetId.Substring(idx + 1) : assetId;
    }

    private static string FinalizeName(string name)
    {
        name = NormalizeName(name);
        if (string.IsNullOrEmpty(name)) return "法术";
        return name.Length <= MaxNameLength ? name : name.Substring(0, MaxNameLength);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var text = name.Trim();
        text = text.Replace(" ", string.Empty);

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && text[i] == text[i - 1]) continue;
            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static float TieBreak(string signature, string id)
    {
        return (NamingRuleUtils.StableHash($"{signature}|{id}") % 1000) / 100000f;
    }

    private sealed class SelectedModifierAtom
    {
        public SkillNamingModifier Modifier;
        public SkillNameAtomAsset Atom;
        public float Score;
    }

    private sealed class NameCandidate
    {
        public string Name;
        public float Score;
        public NameCandidateFeatures Features;
    }

    [Flags]
    private enum NameCandidateFeatures
    {
        None = 0,
        Base = 1,
        Element = 2,
        Form = 4,
        Motion = 8,
        PrimaryModifier = 16,
        SecondaryModifier = 32,
        Ending = 64
    }
}
