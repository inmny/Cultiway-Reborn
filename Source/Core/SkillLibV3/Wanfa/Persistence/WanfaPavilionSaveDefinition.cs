using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Wanfa.Persistence.Migrations;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

internal static class WanfaPavilionSaveDefinition
{
    public const string DocumentId = "wanfa_pavilion";
    public const int CurrentVersion = 4;

    public static SaveDocumentDefinition<WanfaPavilionData> Create()
    {
        return new SaveDocumentDefinition<WanfaPavilionData>(
            DocumentId,
            "Saves/global/wanfa_pavilion",
            CurrentVersion,
            () => new WanfaPavilionData(),
            new ISaveMigration[]
            {
                new WanfaPavilionV1ToV2(),
                new WanfaPavilionV2ToV3(),
                new WanfaPavilionV3ToV4()
            },
            Normalize,
            Validate,
            new[]
            {
                new LegacySaveSource("WanfaPavilion/library", new WanfaLegacyImporter())
            });
    }

    private static void Normalize(WanfaPavilionData data)
    {
        data.Blueprints ??= new List<SkillBlueprint>();
        data.Blueprints.RemoveAll(blueprint => blueprint == null);

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var blueprint in data.Blueprints)
        {
            blueprint.Modifiers ??= new List<SkillModifierSpec>();
            blueprint.Origin ??= new SkillBlueprintOriginData();
            if (blueprint.CastResourceRequirement == null || !blueprint.CastResourceRequirement.IsConfigured)
            {
                var entity = ModClass.I.SkillV3.SkillLib.get(blueprint.EntityAssetId);
                if (entity != null && entity.DefaultCastResourceRequirement != null)
                {
                    blueprint.CastResourceRequirement = entity.DefaultCastResourceRequirement.DeepClone();
                }
            }
            if (string.IsNullOrWhiteSpace(blueprint.Id) || !seenIds.Add(blueprint.Id))
            {
                var oldId = blueprint.Id;
                blueprint.Id = Guid.NewGuid().ToString("N");
                seenIds.Add(blueprint.Id);
                if (!string.IsNullOrWhiteSpace(oldId)) blueprint.Origin.SourceBlueprintId = oldId;
            }
            if (blueprint.Revision < 1) blueprint.Revision = 1;
            if (blueprint.CreatedAtUtcTicks <= 0) blueprint.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
            if (blueprint.UpdatedAtUtcTicks <= 0) blueprint.UpdatedAtUtcTicks = blueprint.CreatedAtUtcTicks;

            for (var i = 0; i < blueprint.Modifiers.Count; i++)
            {
                blueprint.Modifiers[i] ??= new SkillModifierSpec();
                blueprint.Modifiers[i].Parameters ??= new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        var validator = new SkillBlueprintValidator();
        data.Blueprints.RemoveAll(blueprint => !validator.Validate(blueprint).IsCompatible);
        seenIds.Clear();
        foreach (var blueprint in data.Blueprints) seenIds.Add(blueprint.Id);

        data.SelectedBlueprintIds ??= new List<string>();
        data.SelectedBlueprintIds = data.SelectedBlueprintIds
            .Where(seenIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        NormalizeSortOrder(data);
    }

    private static string Validate(WanfaPavilionData data)
    {
        if (data.Blueprints.Any(blueprint => blueprint.SchemaVersion != SkillBlueprint.CurrentSchemaVersion))
        {
            return "万法阁包含未迁移到当前版本的法术蓝图";
        }
        return null;
    }

    internal static void NormalizeSortOrder(WanfaPavilionData data)
    {
        data.Blueprints = data.Blueprints
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.Favorite)
            .ThenBy(item => item.CreatedAtUtcTicks)
            .ToList();
        for (var i = 0; i < data.Blueprints.Count; i++) data.Blueprints[i].SortOrder = i;
    }
}
