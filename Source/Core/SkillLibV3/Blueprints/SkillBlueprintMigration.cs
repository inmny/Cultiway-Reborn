using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Blueprints;

/// <summary>保留正式蓝图的相邻版本转换，转换后只运行当前技能。</summary>
public static class SkillBlueprintMigration
{
    /// <summary>只转换已知的相邻版本，保留编号、参数和旧变体。</summary>
    /// <param name="blueprint">当前部分的蓝图。</param>
    public static void Upgrade(JObject blueprint)
    {
        var version = blueprint.Properties().FirstOrDefault(p => string.Equals(p.Name, "SchemaVersion", StringComparison.OrdinalIgnoreCase));
        if (!JToken.DeepEquals(version?.Value, 3)) return;
        version.Value = 4;
        // 新字段的默认值就是原来的单一主体，不添加虚假的后续作用。
    }

    /// <summary>第四版转第五版，保留主体及通用参数，移除配方连接。</summary>
    /// <param name="blueprint">存档层复制后的蓝图。</param>
    /// <returns>丢弃后续作用时返回需要提示的原因。</returns>
    public static string RemoveRecipe(JObject blueprint)
    {
        var version = blueprint.Property("SchemaVersion", StringComparison.OrdinalIgnoreCase);
        if (!JToken.DeepEquals(version?.Value, 4)) return null;
        var recipe = blueprint.Property("Recipe", StringComparison.OrdinalIgnoreCase);
        bool removed = recipe?.Value is JObject data &&
            data.GetValue("Parts", StringComparison.OrdinalIgnoreCase) is JArray parts && parts.Count > 0;
        recipe?.Remove();
        version.Value = 5;
        return removed ? "组合配方已移除，保留原法术主体及参数，后续作用不再触发" : null;
    }
}
