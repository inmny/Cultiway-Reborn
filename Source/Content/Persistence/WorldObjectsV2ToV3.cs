using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Persistence;

/// <summary>世界技能去掉组合连接，其余物件与关系保持原资料。</summary>
internal sealed class WorldObjectsV2ToV3 : ISaveMigration
{
    /// <summary>转换前的世界物件版本。</summary>
    public int FromVersion => 2;

    /// <summary>保留单招主体，记录后续作用的局部丢弃供恢复报告提示。</summary>
    /// <param name="data">存档层复制后的资料。</param>
    public JObject Apply(JObject data)
    {
        if (data.GetValue("Objects", StringComparison.OrdinalIgnoreCase) is JArray objects)
            foreach (JObject record in objects.OfType<JObject>())
                if (record.GetValue("Skill", StringComparison.OrdinalIgnoreCase) is JObject skill)
                {
                    string warning = SkillBlueprintMigration.RemoveRecipe(skill);
                    if (warning != null) record["LoadWarning"] = warning;
                }
        return data;
    }
}
