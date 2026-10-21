using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.SkillLibV3.Blueprints;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence.Migrations;

/// <summary>升级库中的蓝图；源文件由统一存档层保留。</summary>
internal sealed class WanfaPavilionV4ToV5 : ISaveMigration
{
    /// <summary>转换前的万法阁资料版本。</summary>
    public int FromVersion => 4;
    /// <summary>旧蓝图保留为当前格式的单一主体。</summary>
    /// <param name="data">由存档层复制、允许转换的资料。</param>
    public JObject Apply(JObject data)
    {
        var list = data.Properties().FirstOrDefault(p => string.Equals(p.Name, "Blueprints", StringComparison.OrdinalIgnoreCase))?.Value as JArray;
        if (list != null) foreach (JObject blueprint in list.OfType<JObject>()) SkillBlueprintMigration.Upgrade(blueprint);
        return data;
    }
}
