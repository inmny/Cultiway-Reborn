using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cultiway.Content.Components;
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
    public ArtifactIconInstance IconInstance;

    public ArtifactAtomData ToAtomData()
    {
        return new ArtifactAtomData
        {
            atom_ids = Atoms.Select(atom => atom.id).ToArray(),
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
            IconInstance = ComposeIconInstance(shape, atoms, seed),
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
        Dictionary<ArtifactShapeAsset, int> scores = new();
            foreach (var kv in context.shape_counts)
            {
                var affinity = ResolveAffinity(kv.Key);
                if (affinity == null) continue;
                scores.TryGetValue(affinity, out var score);
                scores[affinity] = score + kv.Value;
        }

        var shape = ItemShapes.Sword;
        var best = 0;
        foreach (var kv in scores)
        {
            if (kv.Value <= best) continue;
            best = kv.Value;
            shape = kv.Key;
        }
        return shape;
    }

    private static ArtifactShapeAsset ResolveAffinity(string shapeId)
    {
        var suffix = shapeId.Substring(shapeId.LastIndexOf('.') + 1);
        return suffix switch
        {
            "Bone" or "Claw" or "Tooth" or "Horn" or "Feather" or "Wing" => ItemShapes.Sword,
            "Crystal" or "Stone" or "Shell" => ItemShapes.Seal,
            "Fur" or "Silk" or "Bamboo" or "Herb" or "Flower" => ItemShapes.Robe,
            "Eye" or "Blood" or "Liquid" => ItemShapes.Mirror,
            "Wood" or "Root" or "Mushroom" or "Fruit" or "Lotus" => ItemShapes.Ding,
            _ => null,
        };
    }

    private static ArtifactAtomAsset[] SelectAtoms(ArtifactRecipeContext context, ArtifactShapeAsset shape, string seed)
    {
        List<ArtifactAtomAsset> atoms = new();
        AddIfNotNull(atoms, PickBestAtom(ArtifactAtomCategory.Shape, context, seed) ?? DefaultShapeAtom(shape));
        AddIfNotNull(atoms, PickBestAtom(ArtifactAtomCategory.Material, context, seed));
        AddIfNotNull(atoms, PickBestAtom(ArtifactAtomCategory.Finish, context, seed));
        return atoms.ToArray();
    }

    private static ArtifactAtomAsset PickBestAtom(ArtifactAtomCategory category, ArtifactRecipeContext context, string seed)
    {
        var candidates = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category == category)
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
        if (shape == ItemShapes.Seal) return ArtifactAtoms.HeavySeal;
        if (shape == ItemShapes.Robe) return ArtifactAtoms.RobeWard;
        if (shape == ItemShapes.Mirror) return ArtifactAtoms.BrightMirror;
        if (shape == ItemShapes.Ding) return ArtifactAtoms.CauldronFire;
        return ArtifactAtoms.SwordEdge;
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

    private static ArtifactIconInstance ComposeIconInstance(
        ArtifactShapeAsset shape,
        IReadOnlyList<ArtifactAtomAsset> atoms,
        string seed)
    {
        var catalog = ArtifactIconCatalogLoader.Current;
        var shapeKey = ShapeKey(shape);
        var templates = catalog.TemplatesForShape(shapeKey);
        if (templates.Count == 0)
        {
            return new ArtifactIconInstance
            {
                template_key = string.Empty,
                slots = [],
            };
        }

        var template = templates[StableIndex($"{seed}|template|{shapeKey}", templates.Count)];
        var slots = new List<ArtifactIconSlot>();
        var placements = template.Placements
            .OrderBy(item => item.Z)
            .ToArray();
        foreach (var placement in placements)
        {
            ArtifactIconModuleDef module = catalog.Modules[placement.Module];
            var variant = PickVariant(module, placement, atoms, seed, template.Key);
            slots.Add(new ArtifactIconSlot
            {
                slot = placement.Slot,
                module = placement.Module,
                variant = variant.Key,
                color_scheme = PickColorScheme(atoms, seed, template.Key, placement.Slot, module.Key, variant.Key),
            });
        }

        return new ArtifactIconInstance
        {
            template_key = template.Key,
            slots = slots.ToArray(),
        };
    }

    private static ArtifactIconVariantDef PickVariant(
        ArtifactIconModuleDef module,
        ArtifactIconPlacementDef placement,
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
        var catalog = ArtifactIconCatalogLoader.Current;
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

    private static string ShapeKey(ArtifactShapeAsset shape)
    {
        if (shape == ItemShapes.Seal) return "seal";
        if (shape == ItemShapes.Robe) return "robe";
        if (shape == ItemShapes.Mirror) return "mirror";
        if (shape == ItemShapes.Ding) return "ding";
        return "sword";
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
