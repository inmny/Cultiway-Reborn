using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence.Migrations;

/// <summary>
/// 将法术蓝图升级到显式施法资源需求版本。具体默认资源由已注册的实体资产在规范化阶段补齐。
/// </summary>
internal sealed class WanfaPavilionV2ToV3 : ISaveMigration
{
    private const int TargetBlueprintSchemaVersion = 3;

    public int FromVersion => 2;

    public JObject Apply(JObject data)
    {
        var blueprintsProperty = FindProperty(data, "Blueprints");
        if (blueprintsProperty?.Value is not JArray blueprints) return data;

        foreach (var blueprint in blueprints.OfType<JObject>())
        {
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
