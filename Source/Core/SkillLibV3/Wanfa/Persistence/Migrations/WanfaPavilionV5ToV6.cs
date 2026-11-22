using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence.Migrations;

/// <summary>去掉全局库中的组合连接，保留普通法术主体并记录局部丢弃。</summary>
internal sealed class WanfaPavilionV5ToV6 : ISaveMigration
{
    /// <summary>转换前的万法阁资料版本。</summary>
    public int FromVersion => 5;

    /// <summary>转换蓝图副本，提示由读取方统一显示。</summary>
    /// <param name="data">存档层复制后的资料。</param>
    public JObject Apply(JObject data)
    {
        var warnings = new JArray();
        if (data.GetValue("Blueprints", StringComparison.OrdinalIgnoreCase) is JArray list)
            foreach (JObject blueprint in list.OfType<JObject>())
            {
                string reason = SkillBlueprintMigration.RemoveRecipe(blueprint);
                var asset = blueprint.GetValue("EntityAssetId", StringComparison.OrdinalIgnoreCase);
                if (asset?.Type == JTokenType.String &&
                    asset.Value<string>() is "Cultiway.RecipeField" or "Cultiway.RecipeBurst" or "Cultiway.RecipePalm")
                    reason = "组合专用主体已移除，该法术将跳过";
                if (reason != null) warnings.Add($"{blueprint.GetValue("Id", StringComparison.OrdinalIgnoreCase)}：{reason}");
            }
        if (warnings.Count > 0) data["MigrationWarnings"] = warnings;
        return data;
    }
}
