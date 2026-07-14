using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts;

public sealed class ArtifactComposeResult
{
    public ArtifactShapeAsset Shape;
    public string Name;
    public ItemLevel Level;
    public ArtifactAtomAsset[] Atoms = [];
    public ArtifactAppearance Appearance;
    public ArtifactAbilitySet AbilitySet;
    public ArtifactAbilityRuntime AbilityRuntime;

    public ArtifactAtomData ToAtomData()
    {
        return new ArtifactAtomData
        {
            atom_ids = Atoms.Select(atom => atom.id).ToArray(),
        };
    }

    public ArtifactControlProfile ToControlProfile()
    {
        float complexity = 1f + Atoms.Length * 0.05f;
        return new ArtifactControlProfile
        {
            complexity = complexity,
            prepared_load_ratio = ArtifactSetting.DefaultPreparedLoadRatio,
            thread_cost = 1,
            autonomous = false,
        };
    }

}

public static class ArtifactComposer
{
    public static ArtifactComposeResult Compose(IReadOnlyList<Entity> ingredients, string creatorName)
    {
        var context = BuildRecipeContext(ingredients);
        var shape = ResolveShape(context);
        var seed = BuildSeed(ingredients, creatorName, shape);
        var atoms = SelectAtoms(context, shape, seed);
        ArtifactAbilityComposition abilities = ArtifactAbilityComposer.Compose(context, shape, atoms, seed);
        return new ArtifactComposeResult
        {
            Shape = shape,
            Name = ComposeName(shape, atoms, seed),
            Level = new ItemLevel
            {
                Stage = context.quality_stage,
                Level = context.quality_level,
            },
            Atoms = atoms,
            Appearance = ComposeAppearance(shape, atoms, seed),
            AbilitySet = abilities.ability_set,
            AbilityRuntime = abilities.runtime,
        };
    }

    private static ArtifactRecipeContext BuildRecipeContext(IReadOnlyList<Entity> ingredients)
    {
        ArtifactRecipeContext context = new()
        {
            ingredient_count = ingredients.Count,
            shape_counts = new Dictionary<string, int>(),
        };

        var bestShapeCount = 0;
        var bestQuality = -1;
        foreach (var ingredient in ingredients)
        {
            if (ingredient.TryGetComponent(out ItemShape shape))
            {
                if (string.IsNullOrEmpty(context.main_material_shape_id))
                {
                    context.main_material_shape_id = shape.shape_id;
                }
                context.shape_counts.TryGetValue(shape.shape_id, out var count);
                count++;
                context.shape_counts[shape.shape_id] = count;
                if (count > bestShapeCount)
                {
                    bestShapeCount = count;
                    context.dominant_shape_id = shape.shape_id;
                }
            }
            if (ingredient.TryGetComponent(out ItemLevel level))
            {
                var quality = level.Stage * 9 + level.Level;
                if (quality > bestQuality)
                {
                    bestQuality = quality;
                    context.quality_stage = level.Stage;
                    context.quality_level = level.Level;
                }
            }
        }
        return context;
    }

    private static ArtifactShapeAsset ResolveShape(ArtifactRecipeContext context)
    {
        ArtifactAtomAsset shapeAtom = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category == ArtifactAtomCategory.Shape && atom.artifact_shape != null)
            .Select(atom => (atom, score: atom.ScoreFor(context)))
            .Where(item => item.score > 0f)
            .OrderByDescending(item => item.score)
            .ThenByDescending(item => item.atom.priority)
            .ThenBy(item => item.atom.id)
            .Select(item => item.atom)
            .FirstOrDefault();
        return (shapeAtom ?? Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
                .Where(atom => atom.category == ArtifactAtomCategory.Shape && atom.artifact_shape != null)
                .OrderByDescending(atom => atom.priority)
                .ThenBy(atom => atom.id)
                .First())
            .artifact_shape;
    }

    private static ArtifactAtomAsset[] SelectAtoms(ArtifactRecipeContext context, ArtifactShapeAsset shape, string seed)
    {
        List<ArtifactAtomAsset> atoms = new();
        AddIfNotNull(atoms, PickBestAtom(ArtifactAtomCategory.Shape, context, seed, shape) ?? DefaultShapeAtom(shape));
        AddIfNotNull(atoms, PickBestAtom(ArtifactAtomCategory.Material, context, seed));
        AddIfNotNull(atoms, PickBestAtom(ArtifactAtomCategory.Finish, context, seed));
        return atoms.ToArray();
    }

    private static ArtifactAtomAsset PickBestAtom(
        ArtifactAtomCategory category,
        ArtifactRecipeContext context,
        string seed,
        ArtifactShapeAsset shape = null)
    {
        var candidates = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category == category && (shape == null || atom.artifact_shape == shape))
            .Select(atom => (atom, score: atom.ScoreFor(context)))
            .Where(item => item.score > 0f)
            .ToArray();
        if (candidates.Length == 0) return null;

        var max = candidates.Max(item => item.score);
        var best = candidates
            .Where(item => Math.Abs(item.score - max) < 0.001f)
            .OrderByDescending(item => item.atom.priority)
            .ThenBy(item => item.atom.id)
            .Select(item => item.atom)
            .ToArray();
        return best[StableIndex($"{seed}|atom|{category}", best.Length)];
    }

    private static ArtifactAtomAsset DefaultShapeAtom(ArtifactShapeAsset shape)
    {
        return Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category == ArtifactAtomCategory.Shape && atom.artifact_shape == shape)
            .OrderByDescending(atom => atom.priority)
            .ThenBy(atom => atom.id)
            .First();
    }

    private static void AddIfNotNull(List<ArtifactAtomAsset> atoms, ArtifactAtomAsset atom)
    {
        if (atom == null || atoms.Contains(atom)) return;
        atoms.Add(atom);
    }

    private static string ComposeName(ArtifactShapeAsset shape, IReadOnlyList<ArtifactAtomAsset> atoms, string seed)
    {
        var value = StableSeed(seed);
        StringBuilder builder = new();
        for (int i = 0; i < atoms.Count && i < 2; i++)
        {
            var stem = atoms[i].PickNameStem(value + i * 37);
            if (!string.IsNullOrEmpty(stem)) builder.Append(stem);
        }

        var shapeName = shape.PickIngredientNameCandidate(value);
        if (string.IsNullOrEmpty(shapeName)) shapeName = "器";
        builder.Append(shapeName);
        return builder.ToString();
    }

    private static ArtifactAppearance ComposeAppearance(
        ArtifactShapeAsset shape,
        IReadOnlyList<ArtifactAtomAsset> atoms,
        string seed)
    {
        var catalog = ArtifactAppearanceCatalogLoader.Current;
        var shapeKey = shape.appearance_family;
        var templates = catalog.TemplatesForShape(shapeKey);
        if (templates.Count == 0)
        {
            return new ArtifactAppearance
            {
                template_key = string.Empty,
                parts = [],
            };
        }

        var template = templates[StableIndex($"{seed}|template|{shapeKey}", templates.Count)];
        var parts = new List<ArtifactAppearancePart>();
        var placements = template.Placements
            .OrderBy(item => item.Z)
            .ToArray();
        foreach (var placement in placements)
        {
            ArtifactAppearanceModuleDef module = catalog.Modules[placement.Module];
            var variant = PickVariant(module, placement, atoms, seed, template.Key);
            parts.Add(new ArtifactAppearancePart
            {
                slot = placement.Slot,
                module = placement.Module,
                variant = variant.Key,
                color_scheme = PickColorScheme(atoms, seed, template.Key, placement.Slot, module.Key, variant.Key),
                colors = [],
            });
        }

        return new ArtifactAppearance
        {
            template_key = template.Key,
            parts = parts.ToArray(),
        };
    }

    private static ArtifactAppearanceVariantDef PickVariant(
        ArtifactAppearanceModuleDef module,
        ArtifactAppearancePlacementDef placement,
        IReadOnlyList<ArtifactAtomAsset> atoms,
        string seed,
        string templateKey)
    {
        var candidates = module.Variants
            .Where(variant => variant.GetAnchor(placement.Anchor) != null)
            .ToArray();

        var maxScore = candidates.Max(variant => VariantScore(atoms, module.Key, variant.Key));
        var best = candidates
            .Where(variant => VariantScore(atoms, module.Key, variant.Key) == maxScore)
            .OrderBy(variant => variant.Key)
            .ToArray();
        return best[StableIndex($"{seed}|variant|{templateKey}|{placement.Slot}|{module.Key}", best.Length)];
    }

    private static int VariantScore(IReadOnlyList<ArtifactAtomAsset> atoms, string moduleKey, string variantKey)
    {
        var score = 0;
        for (int i = 0; i < atoms.Count; i++)
        {
            if (atoms[i].BiasesVariant(moduleKey, variantKey)) score += 1;
        }
        return score;
    }

    private static string PickColorScheme(
        IReadOnlyList<ArtifactAtomAsset> atoms,
        string seed,
        string templateKey,
        string slot,
        string moduleKey,
        string variantKey)
    {
        var catalog = ArtifactAppearanceCatalogLoader.Current;
        if (catalog.ColorSchemes.Count == 0) return string.Empty;
        var schemes = catalog.ColorSchemes.Values.OrderBy(scheme => scheme.Key).ToArray();
        var maxScore = schemes.Max(scheme => ColorScore(atoms, scheme.Key));
        var best = schemes.Where(scheme => ColorScore(atoms, scheme.Key) == maxScore).ToArray();
        return best[StableIndex($"{seed}|color|{templateKey}|{slot}|{moduleKey}|{variantKey}", best.Length)].Key;
    }

    private static int ColorScore(IReadOnlyList<ArtifactAtomAsset> atoms, string schemeKey)
    {
        var score = 0;
        for (int i = 0; i < atoms.Count; i++)
        {
            if (atoms[i].BiasesColorScheme(schemeKey)) score += 1;
        }
        return score;
    }

    private static string BuildSeed(IReadOnlyList<Entity> ingredients, string creatorName, ArtifactShapeAsset shape)
    {
        StringBuilder builder = new();
        builder.Append(creatorName);
        builder.Append('|').Append(shape.id);
        foreach (var ingredient in ingredients)
        {
            builder.Append('|').Append(ingredient.Id);
            if (ingredient.TryGetComponent(out ItemShape shapeComponent))
            {
                builder.Append(':').Append(shapeComponent.shape_id);
            }
            if (ingredient.TryGetComponent(out ItemLevel level))
            {
                builder.Append(':').Append(level.Stage).Append('.').Append(level.Level);
            }
        }
        return builder.ToString();
    }

    private static int StableIndex(string text, int count)
    {
        return (int)(StableUInt(text) % (uint)count);
    }

    private static int StableSeed(string text)
    {
        return unchecked((int)StableUInt(text));
    }

    private static uint StableUInt(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }
}
