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
using UnityEngine;

namespace Cultiway.Content.Artifacts;

public sealed class ArtifactComposeResult
{
    public ArtifactShapeAsset Shape;
    public string Name;
    public ItemLevel Level;
    public ArtifactAtomSelection[] Atoms = [];
    public ArtifactMaterialData MaterialData;
    public ArtifactAppearance Appearance;
    public ArtifactAbilitySet AbilitySet;
    public ArtifactAbilityRuntime AbilityRuntime;

    public ArtifactAtomData ToAtomData()
    {
        return new ArtifactAtomData
        {
            entries = Atoms
                .Select(atom => new ArtifactAtomEntry
                {
                    atom_id = atom.Atom.id,
                    strength = atom.Strength,
                })
                .ToArray(),
        };
    }

    public ArtifactControlProfile ToControlProfile()
    {
        return new ArtifactControlProfile
        {
            complexity = 1f + MaterialData.complexity,
            prepared_load_ratio = ArtifactSetting.DefaultPreparedLoadRatio,
            thread_cost = 1,
            autonomous = false,
        };
    }

}

/// <summary>
/// 百宝阁等显式炼制入口提供的法宝设计。器形和品阶固定，atom 数量不受限制。
/// </summary>
public sealed class ArtifactDesignRequest
{
    public ArtifactShapeAsset Shape;
    public ItemLevel Level;
    public ArtifactAtomAsset[] Atoms = [];
    public string Name;
}

public static class ArtifactComposer
{
    public static ArtifactComposeResult Compose(IReadOnlyList<Entity> ingredients)
    {
        ArtifactRecipeContext context = ArtifactMaterialSemantics.Build(ingredients);
        ArtifactShapeAsset shape = ResolveShape(context);
        ArtifactAtomSelection[] atoms = SelectAtoms(context, shape);
        ArtifactMaterialSemantics.ApplyAtoms(context, atoms);
        string compositionKey = BuildCompositionKey("recipe", context, shape, atoms);
        return ComposeResolved(context, shape, atoms, compositionKey, null);
    }

    /// <summary>
    /// 按显式设计组合一件法宝，供百宝阁和后续专属制造入口使用。
    /// </summary>
    public static ArtifactComposeResult ComposeDesign(ArtifactDesignRequest design)
    {
        List<ArtifactAtomSelection> atoms = new();
        ArtifactAtomAsset shapeAtom = design.Atoms
            .Where(atom => atom.category == ArtifactAtomCategory.Shape && atom.artifact_shape == design.Shape)
            .OrderByDescending(atom => atom.priority)
            .ThenBy(atom => atom.id)
            .FirstOrDefault() ?? DefaultShapeAtom(design.Shape);
        float qualityStrength = 1f + (int)design.Level / 35f * 0.5f;
        AddIfNotNull(atoms, shapeAtom, qualityStrength);
        foreach (ArtifactAtomAsset atom in design.Atoms
                     .Where(atom => atom.category != ArtifactAtomCategory.Shape)
                     .OrderBy(atom => atom.category)
                     .ThenBy(atom => atom.id, StringComparer.Ordinal))
        {
            AddIfNotNull(atoms, atom, qualityStrength);
        }

        ArtifactAtomSelection[] normalizedAtoms = atoms.ToArray();
        ArtifactRecipeContext context = ArtifactMaterialSemantics.BuildDesign(design.Level, normalizedAtoms.Length);
        ArtifactMaterialSemantics.ApplyAtoms(context, normalizedAtoms);
        string compositionKey = BuildCompositionKey("design", context, design.Shape, normalizedAtoms);
        return ComposeResolved(context, design.Shape, normalizedAtoms, compositionKey, design.Name);
    }

    private static ArtifactComposeResult ComposeResolved(
        ArtifactRecipeContext context,
        ArtifactShapeAsset shape,
        ArtifactAtomSelection[] atoms,
        string compositionKey,
        string explicitName)
    {
        ArtifactAbilityComposition abilities = ArtifactAbilityComposer.Compose(context, shape, atoms, compositionKey);
        return new ArtifactComposeResult
        {
            Shape = shape,
            Name = string.IsNullOrWhiteSpace(explicitName) ? ComposeName(shape, atoms, compositionKey) : explicitName,
            Level = new ItemLevel
            {
                Stage = context.quality_stage,
                Level = context.quality_level,
            },
            Atoms = atoms,
            MaterialData = context.material_data,
            Appearance = ComposeAppearance(shape, atoms, compositionKey),
            AbilitySet = abilities.ability_set,
            AbilityRuntime = abilities.runtime,
        };
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

    private static ArtifactAtomSelection[] SelectAtoms(
        ArtifactRecipeContext context,
        ArtifactShapeAsset shape)
    {
        List<ArtifactAtomSelection> atoms = new();
        ArtifactAtomAsset shapeAtom = PickBestShapeAtom(context, shape) ?? DefaultShapeAtom(shape);
        AddIfNotNull(atoms, shapeAtom, NormalizeAtomStrength(Mathf.Max(1f, shapeAtom.ScoreFor(context))));

        ArtifactAtomSelection[] matched = Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category != ArtifactAtomCategory.Shape)
            .Select(atom => new { Atom = atom, Score = atom.ScoreFor(context) })
            .Where(candidate => candidate.Score >= candidate.Atom.minimum_score)
            .OrderBy(candidate => candidate.Atom.category)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Atom.id, StringComparer.Ordinal)
            .Select(candidate => new ArtifactAtomSelection(
                candidate.Atom,
                NormalizeAtomStrength(candidate.Score)))
            .ToArray();
        atoms.AddRange(matched);
        return atoms.ToArray();
    }

    private static ArtifactAtomAsset PickBestShapeAtom(
        ArtifactRecipeContext context,
        ArtifactShapeAsset shape)
    {
        return Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category == ArtifactAtomCategory.Shape && atom.artifact_shape == shape)
            .Select(atom => (atom, score: atom.ScoreFor(context)))
            .Where(item => item.score >= item.atom.minimum_score)
            .OrderByDescending(item => item.score)
            .ThenByDescending(item => item.atom.priority)
            .ThenBy(item => item.atom.id)
            .Select(item => item.atom)
            .FirstOrDefault();
    }

    private static ArtifactAtomAsset DefaultShapeAtom(ArtifactShapeAsset shape)
    {
        return Cultiway.Content.Libraries.Manager.ArtifactAtomLibrary.All
            .Where(atom => atom.category == ArtifactAtomCategory.Shape && atom.artifact_shape == shape)
            .OrderByDescending(atom => atom.priority)
            .ThenBy(atom => atom.id)
            .FirstOrDefault();
    }

    private static void AddIfNotNull(
        List<ArtifactAtomSelection> atoms,
        ArtifactAtomAsset atom,
        float strength)
    {
        if (atom == null || atoms.Any(value => value.Atom == atom)) return;
        atoms.Add(new ArtifactAtomSelection(atom, strength));
    }

    private static string ComposeName(
        ArtifactShapeAsset shape,
        IReadOnlyList<ArtifactAtomSelection> atoms,
        string compositionKey)
    {
        var value = StableHash(compositionKey);
        StringBuilder builder = new();
        ArtifactAtomSelection[] nameAtoms = atoms
            .OrderBy(atom => atom.Atom.category)
            .ThenByDescending(atom => atom.Strength)
            .ThenBy(atom => atom.Atom.id, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        for (int i = 0; i < nameAtoms.Length; i++)
        {
            string stem = nameAtoms[i].Atom.PickNameStem(value + i * 37);
            if (!string.IsNullOrEmpty(stem)) builder.Append(stem);
        }

        var shapeName = shape.PickIngredientNameCandidate(value);
        if (string.IsNullOrEmpty(shapeName)) shapeName = "器";
        builder.Append(shapeName);
        return builder.ToString();
    }

    private static ArtifactAppearance ComposeAppearance(
        ArtifactShapeAsset shape,
        IReadOnlyList<ArtifactAtomSelection> atoms,
        string compositionKey)
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

        var template = templates[StableIndex($"{compositionKey}|template|{shapeKey}", templates.Count)];
        var parts = new List<ArtifactAppearancePart>();
        var placements = template.Placements
            .OrderBy(item => item.Z)
            .ToArray();
        foreach (var placement in placements)
        {
            ArtifactAppearanceModuleDef module = catalog.Modules[placement.Module];
            var variant = PickVariant(module, placement, atoms, compositionKey, template.Key);
            parts.Add(new ArtifactAppearancePart
            {
                slot = placement.Slot,
                module = placement.Module,
                variant = variant.Key,
                color_scheme = PickColorScheme(atoms, compositionKey, template.Key, placement.Slot, module.Key,
                    variant.Key),
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
        IReadOnlyList<ArtifactAtomSelection> atoms,
        string compositionKey,
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
        return best[StableIndex(
            $"{compositionKey}|variant|{templateKey}|{placement.Slot}|{module.Key}", best.Length)];
    }

    private static float VariantScore(
        IReadOnlyList<ArtifactAtomSelection> atoms,
        string moduleKey,
        string variantKey)
    {
        float score = 0f;
        for (int i = 0; i < atoms.Count; i++)
        {
            if (atoms[i].Atom.BiasesVariant(moduleKey, variantKey)) score += atoms[i].Strength;
        }
        return score;
    }

    private static string PickColorScheme(
        IReadOnlyList<ArtifactAtomSelection> atoms,
        string compositionKey,
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
        return best[StableIndex(
            $"{compositionKey}|color|{templateKey}|{slot}|{moduleKey}|{variantKey}", best.Length)].Key;
    }

    private static float ColorScore(IReadOnlyList<ArtifactAtomSelection> atoms, string schemeKey)
    {
        float score = 0f;
        for (int i = 0; i < atoms.Count; i++)
        {
            if (atoms[i].Atom.BiasesColorScheme(schemeKey)) score += atoms[i].Strength;
        }
        return score;
    }

    private static string BuildCompositionKey(
        string origin,
        ArtifactRecipeContext context,
        ArtifactShapeAsset shape,
        IReadOnlyList<ArtifactAtomSelection> atoms)
    {
        StringBuilder builder = new();
        builder.Append(origin).Append('|').Append(shape.id)
            .Append('|').Append(context.quality_stage).Append('.').Append(context.quality_level)
            .Append('|').Append(StableDigest(context.material_data.GetCacheKey()));
        for (int i = 0; i < atoms.Count; i++)
        {
            builder.Append('|').Append(atoms[i].Atom.id).Append(':')
                .Append(atoms[i].Strength.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    private static float NormalizeAtomStrength(float score)
    {
        return 0.75f + Mathf.Log(1f + Mathf.Max(0f, score), 2f) * 0.65f;
    }

    private static int StableIndex(string text, int count)
    {
        return (int)(StableUInt(text) % (uint)count);
    }

    private static int StableHash(string text)
    {
        return unchecked((int)StableUInt(text));
    }

    private static uint StableUInt(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static string StableDigest(string text)
    {
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
            .Replace("-", string.Empty);
    }
}
