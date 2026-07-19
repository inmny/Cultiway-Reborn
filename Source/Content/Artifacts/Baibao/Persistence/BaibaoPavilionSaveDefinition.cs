using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Artifacts.Baibao.Persistence.Migrations;
using Cultiway.Core.Persistence;

namespace Cultiway.Content.Artifacts.Baibao.Persistence;

internal static class BaibaoPavilionSaveDefinition
{
    public const string DocumentId = "baibao_pavilion";
    public const int CurrentVersion = 2;

    public static SaveDocumentDefinition<BaibaoPavilionData> Create()
    {
        return new SaveDocumentDefinition<BaibaoPavilionData>(
            DocumentId,
            "Saves/global/baibao_pavilion",
            CurrentVersion,
            () => new BaibaoPavilionData(),
            new ISaveMigration[] { new BaibaoPavilionV1ToV2() },
            Normalize,
            Validate);
    }

    private static void Normalize(BaibaoPavilionData data)
    {
        data.Blueprints ??= new List<ArtifactBlueprint>();
        data.Blueprints.RemoveAll(blueprint => blueprint == null);

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ArtifactBlueprint blueprint in data.Blueprints)
        {
            if (string.IsNullOrWhiteSpace(blueprint.Id) || !ids.Add(blueprint.Id))
            {
                blueprint.Id = Guid.NewGuid().ToString("N");
                ids.Add(blueprint.Id);
            }
            if (blueprint.CreatedAtUtcTicks <= 0) blueprint.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
            if (blueprint.UpdatedAtUtcTicks <= 0) blueprint.UpdatedAtUtcTicks = blueprint.CreatedAtUtcTicks;

            blueprint.AtomData.entries ??= [];
            blueprint.MaterialData.materials ??= [];
            blueprint.MaterialData.traits ??= [];
            blueprint.AtomData.entries = blueprint.AtomData.entries
                .OrderBy(entry => entry.atom_id, StringComparer.Ordinal)
                .ToArray();
            blueprint.MaterialData.materials = blueprint.MaterialData.materials
                .OrderBy(material => material.GetIdentityKey(), StringComparer.Ordinal)
                .ToArray();
            blueprint.MaterialData.traits = blueprint.MaterialData.traits
                .OrderBy(trait => trait.key, StringComparer.Ordinal)
                .ToArray();
            blueprint.Appearance.color_roles ??= [];
            blueprint.Appearance.parts ??= [];
            for (int i = 0; i < blueprint.Appearance.parts.Length; i++)
            {
                ArtifactAppearancePart part = blueprint.Appearance.parts[i];
                part.colors ??= [];
                blueprint.Appearance.parts[i] = part;
            }

            blueprint.AbilitySet.abilities ??= [];
            for (int i = 0; i < blueprint.AbilitySet.abilities.Length; i++)
            {
                ArtifactAbilityInstance ability = blueprint.AbilitySet.abilities[i];
                ability.parameters ??= [];
                ability.initial_state ??= [];
                blueprint.AbilitySet.abilities[i] = ability;
            }

            blueprint.Extensions ??= new List<ArtifactBlueprintExtensionData>();
            blueprint.Extensions.RemoveAll(extension => extension == null ||
                string.IsNullOrWhiteSpace(extension.ExtensionId));
            blueprint.Extensions = blueprint.Extensions
                .GroupBy(extension => extension.ExtensionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        data.SelectedBlueprintIds ??= new List<string>();
        data.SelectedBlueprintIds = data.SelectedBlueprintIds
            .Where(ids.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        NormalizeSortOrder(data);
    }

    private static string Validate(BaibaoPavilionData data)
    {
        return data.Blueprints.Any(blueprint => blueprint.SchemaVersion != ArtifactBlueprint.CurrentSchemaVersion)
            ? "百宝阁包含未迁移到当前版本的法宝蓝图"
            : null;
    }

    internal static void NormalizeSortOrder(BaibaoPavilionData data)
    {
        data.Blueprints = data.Blueprints
            .OrderBy(blueprint => blueprint.SortOrder)
            .ThenByDescending(blueprint => blueprint.Favorite)
            .ThenBy(blueprint => blueprint.CreatedAtUtcTicks)
            .ToList();
        for (int i = 0; i < data.Blueprints.Count; i++) data.Blueprints[i].SortOrder = i;
    }
}
