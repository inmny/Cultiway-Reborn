using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Persistence;

/// <summary>把世界物件中的旧技能转换为可组合蓝图，不改变其余物件。</summary>
internal sealed class WorldObjectsV1ToV2 : ISaveMigration
{
    /// <summary>原世界物件版本。</summary>
    public int FromVersion => 1;
    /// <summary>保持世界与全局库使用相同的蓝图转换。</summary>
    /// <param name="data">由存档层复制、允许转换的资料。</param>
    public JObject Apply(JObject data)
    {
        var objects = data.Properties().FirstOrDefault(p => string.Equals(p.Name, "Objects", StringComparison.OrdinalIgnoreCase))?.Value as JArray;
        if (objects != null) foreach (JObject record in objects.OfType<JObject>())
            {
                var skill = record.Properties().FirstOrDefault(p => string.Equals(p.Name, "Skill", StringComparison.OrdinalIgnoreCase))?.Value as JObject;
                if (skill != null) SkillBlueprintMigration.Upgrade(skill);
            }
        return data;
    }
}
