using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using UnityEngine;

namespace Cultiway.Content.Artifacts.Baibao;

/// <summary>候选 atom 相对当前蓝图执行的编辑动作。</summary>
public enum ArtifactAtomEditOperation
{
    Current,
    Add,
    Remove,
    ReplaceShape,
}

/// <summary>候选 atom 经过正式组合器后，对蓝图造成的可展示差异。</summary>
public sealed class ArtifactAtomImpact
{
    public ArtifactAtomEditOperation Operation;
    public ArtifactMaterialTrait[] TraitDeltas = [];
    public string[] AddedAbilityIds = [];
    public string[] RemovedAbilityIds = [];
    public string[] ChangedAbilityIds = [];
    public float StabilityDelta;
    public float ComplexityDelta;
    public bool NameChanged;
    public string BeforeName;
    public string AfterName;
    public int AppearanceChangedPartCount;

    public bool HasChanges => Operation != ArtifactAtomEditOperation.Current &&
                              (TraitDeltas.Length > 0 || AddedAbilityIds.Length > 0 ||
                               RemovedAbilityIds.Length > 0 || ChangedAbilityIds.Length > 0 ||
                               Mathf.Abs(StabilityDelta) > 0.0001f || Mathf.Abs(ComplexityDelta) > 0.0001f ||
                               NameChanged || AppearanceChangedPartCount > 0);
}

/// <summary>
/// 使用与实际炼成完全相同的组合器评估 atom 编辑结果，供百宝阁等设计界面展示真实影响。
/// </summary>
public static class ArtifactAtomImpactAnalyzer
{
    public static ArtifactAtomImpact Analyze(
        ArtifactBlueprint blueprint,
        ArtifactAtomAsset atom,
        bool autoName,
        bool autoAppearance)
    {
        ArtifactAtomEntry[] currentEntries = blueprint.AtomData.entries ?? [];
        bool selected = currentEntries.Any(entry => entry.atom_id == atom.id);
        ArtifactAtomEditOperation operation = ResolveOperation(atom, selected);
        ArtifactAtomImpact impact = new()
        {
            Operation = operation,
            BeforeName = blueprint.Name,
            AfterName = blueprint.Name,
        };
        if (operation == ArtifactAtomEditOperation.Current) return impact;

        ArtifactAtomEntry[] candidateEntries = BuildCandidateEntries(currentEntries, atom, operation);
        ArtifactComposeResult candidate = ArtifactComposer.ComposeDesign(new ArtifactDesignRequest
        {
            Shape = (ArtifactShapeAsset)ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId),
            Level = blueprint.Level,
            AtomEntries = candidateEntries,
            Name = autoName ? null : blueprint.Name,
            AppearanceOverride = autoAppearance ? null : blueprint.Appearance,
        });

        impact.TraitDeltas = CompareTraits(blueprint.MaterialData.traits ?? [], candidate.MaterialData.traits ?? []);
        CompareAbilities(blueprint.AbilitySet.abilities ?? [], candidate.AbilitySet.abilities ?? [], impact);
        impact.StabilityDelta = candidate.MaterialData.stability - blueprint.MaterialData.stability;
        impact.ComplexityDelta = candidate.MaterialData.complexity - blueprint.MaterialData.complexity;
        impact.AfterName = candidate.Name;
        impact.NameChanged = !string.Equals(blueprint.Name, candidate.Name, StringComparison.Ordinal);
        impact.AppearanceChangedPartCount = CountAppearanceChanges(blueprint.Appearance, candidate.Appearance);
        return impact;
    }

    private static ArtifactAtomEditOperation ResolveOperation(ArtifactAtomAsset atom, bool selected)
    {
        if (atom.category == ArtifactAtomCategory.Shape)
            return selected ? ArtifactAtomEditOperation.Current : ArtifactAtomEditOperation.ReplaceShape;
        return selected ? ArtifactAtomEditOperation.Remove : ArtifactAtomEditOperation.Add;
    }

    private static ArtifactAtomEntry[] BuildCandidateEntries(
        ArtifactAtomEntry[] current,
        ArtifactAtomAsset atom,
        ArtifactAtomEditOperation operation)
    {
        if (operation == ArtifactAtomEditOperation.Remove)
            return current.Where(entry => entry.atom_id != atom.id).ToArray();

        List<ArtifactAtomEntry> result = current.ToList();
        if (operation == ArtifactAtomEditOperation.ReplaceShape)
        {
            result.RemoveAll(entry =>
            {
                ArtifactAtomAsset currentAtom = Libraries.Manager.ArtifactAtomLibrary.get(entry.atom_id);
                return currentAtom?.category == ArtifactAtomCategory.Shape;
            });
        }
        result.Add(new ArtifactAtomEntry { atom_id = atom.id, strength = 1f });
        return result.ToArray();
    }

    private static ArtifactMaterialTrait[] CompareTraits(
        ArtifactMaterialTrait[] before,
        ArtifactMaterialTrait[] after)
    {
        Dictionary<string, float> values = new(StringComparer.Ordinal);
        for (int i = 0; i < before.Length; i++) values[before[i].key] = -before[i].value;
        for (int i = 0; i < after.Length; i++)
        {
            values.TryGetValue(after[i].key, out float current);
            values[after[i].key] = current + after[i].value;
        }
        return values
            .Where(pair => Mathf.Abs(pair.Value) > 0.0001f)
            .OrderByDescending(pair => Mathf.Abs(pair.Value))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ArtifactMaterialTrait { key = pair.Key, value = pair.Value })
            .ToArray();
    }

    private static void CompareAbilities(
        ArtifactAbilityInstance[] before,
        ArtifactAbilityInstance[] after,
        ArtifactAtomImpact impact)
    {
        Dictionary<string, ArtifactAbilityInstance> beforeById = before.ToDictionary(
            ability => ability.ability_id, StringComparer.Ordinal);
        Dictionary<string, ArtifactAbilityInstance> afterById = after.ToDictionary(
            ability => ability.ability_id, StringComparer.Ordinal);
        impact.AddedAbilityIds = afterById.Keys.Except(beforeById.Keys, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        impact.RemovedAbilityIds = beforeById.Keys.Except(afterById.Keys, StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        impact.ChangedAbilityIds = beforeById.Keys.Intersect(afterById.Keys, StringComparer.Ordinal)
            .Where(id => !AbilityEquals(beforeById[id], afterById[id]))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static bool AbilityEquals(ArtifactAbilityInstance left, ArtifactAbilityInstance right)
    {
        return ValuesEqual(left.parameters ?? [], right.parameters ?? []) &&
               ValuesEqual(left.initial_state ?? [], right.initial_state ?? []);
    }

    private static bool ValuesEqual(ArtifactAbilityValue[] left, ArtifactAbilityValue[] right)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
        {
            ArtifactAbilityValue a = left[i];
            ArtifactAbilityValue b = right[i];
            if (a.key != b.key || a.kind != b.kind || !Mathf.Approximately(a.number, b.number) ||
                a.integer != b.integer || a.boolean != b.boolean || a.asset_id != b.asset_id)
                return false;
        }
        return true;
    }

    private static int CountAppearanceChanges(ArtifactAppearance before, ArtifactAppearance after)
    {
        if (before.GetCacheKey() == after.GetCacheKey()) return 0;
        Dictionary<string, string> beforeParts = (before.parts ?? [])
            .ToDictionary(part => part.slot, part => part.GetCacheKey(), StringComparer.Ordinal);
        Dictionary<string, string> afterParts = (after.parts ?? [])
            .ToDictionary(part => part.slot, part => part.GetCacheKey(), StringComparer.Ordinal);
        return beforeParts.Keys.Union(afterParts.Keys, StringComparer.Ordinal)
            .Count(slot => !beforeParts.TryGetValue(slot, out string beforeKey) ||
                           !afterParts.TryGetValue(slot, out string afterKey) || beforeKey != afterKey);
    }
}
