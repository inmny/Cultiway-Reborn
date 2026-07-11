using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence.Migrations;

/// <summary>
/// 为旧法术蓝图补充实体动画索引。
/// </summary>
internal sealed class WanfaPavilionV1ToV2 : ISaveMigration
{
    private const int TargetBlueprintSchemaVersion = 2;

    public int FromVersion => 1;

    public JObject Apply(JObject data)
    {
        var blueprintsProperty = FindProperty(data, "Blueprints");
        if (blueprintsProperty?.Value is not JArray blueprints)
        {
            data["Blueprints"] = new JArray();
            return data;
        }

        foreach (var blueprint in blueprints.OfType<JObject>())
        {
            var schemaProperty = FindProperty(blueprint, "SchemaVersion");
            var schemaVersion = schemaProperty?.Value.Value<int>() ?? 1;
            if (schemaVersion >= TargetBlueprintSchemaVersion) continue;

            SetProperty(blueprint, "AnimationIndex", 0);
            SetProperty(blueprint, "SchemaVersion", TargetBlueprintSchemaVersion);
        }
        return data;
    }

    private static void SetProperty(JObject root, string name, JToken value)
    {
        var property = FindProperty(root, name);
        if (property == null) root[name] = value;
        else property.Value = value;
    }

    private static JProperty FindProperty(JObject root, string name)
    {
        return root.Properties().FirstOrDefault(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
