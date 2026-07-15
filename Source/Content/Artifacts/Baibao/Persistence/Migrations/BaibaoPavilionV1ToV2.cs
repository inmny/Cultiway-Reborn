using System;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts.Baibao.Persistence.Migrations;

/// <summary>
/// 将仅保存 atom ID 的旧蓝图迁移为带强度 atom 和材料语义的蓝图。
/// </summary>
internal sealed class BaibaoPavilionV1ToV2 : ISaveMigration
{
    public int FromVersion => 1;

    public JObject Apply(JObject data)
    {
        if (FindProperty(data, "Blueprints")?.Value is not JArray blueprints)
        {
            data["Blueprints"] = new JArray();
            return data;
        }

        foreach (JObject blueprint in blueprints.OfType<JObject>())
        {
            JObject atomData = FindProperty(blueprint, "AtomData")?.Value as JObject ?? new JObject();
            JArray oldIds = FindProperty(atomData, "atom_ids")?.Value as JArray ?? new JArray();
            JArray entries = new(oldIds
                .Values<string>()
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Select(id => new JObject
                {
                    ["atom_id"] = id,
                    ["strength"] = 1f,
                }));
            RemoveProperty(atomData, "atom_ids");
            SetProperty(atomData, "entries", entries);
            SetProperty(blueprint, "AtomData", atomData);

            int quality = ReadQuality(blueprint);
            int ingredientCount = Math.Max(1, entries.Count);
            JObject materialData = new()
            {
                ["materials"] = new JArray(),
                ["traits"] = new JArray
                {
                    Trait(ArtifactMaterialTraits.Quality, quality / 9f),
                    Trait(ArtifactMaterialTraits.Quantity, Log2(1f + ingredientCount)),
                    Trait(ArtifactMaterialTraits.Stability, 1f),
                },
                ["ingredient_count"] = ingredientCount,
                ["quality_budget"] = ingredientCount * (1f + quality / 9f),
                ["stability"] = 1f,
                ["complexity"] = Log2(1f + ingredientCount) * 0.06f +
                                 Math.Max(0, entries.Count - 1) * 0.06f,
            };
            SetProperty(blueprint, "MaterialData", materialData);
            SetProperty(blueprint, "SchemaVersion", ArtifactBlueprint.CurrentSchemaVersion);
        }
        return data;
    }

    private static int ReadQuality(JObject blueprint)
    {
        if (FindProperty(blueprint, "Level")?.Value is not JObject level) return 0;
        int stage = FindProperty(level, "Stage")?.Value.Value<int>() ?? 0;
        int value = FindProperty(level, "Level")?.Value.Value<int>() ?? 0;
        return Math.Max(0, Math.Min(35, stage * 9 + value));
    }

    private static JObject Trait(string key, float value)
    {
        return new JObject
        {
            ["key"] = key,
            ["value"] = value,
        };
    }

    private static float Log2(float value)
    {
        return (float)(Math.Log(Math.Max(value, 1f)) / Math.Log(2d));
    }

    private static void SetProperty(JObject root, string name, JToken value)
    {
        JProperty property = FindProperty(root, name);
        if (property == null) root[name] = value;
        else property.Value = value;
    }

    private static void RemoveProperty(JObject root, string name)
    {
        FindProperty(root, name)?.Remove();
    }

    private static JProperty FindProperty(JObject root, string name)
    {
        return root.Properties().FirstOrDefault(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
