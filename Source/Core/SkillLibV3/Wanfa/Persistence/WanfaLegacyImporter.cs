using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence;

/// <summary>
/// 导入统一持久化文档协议引入前的万法阁 library.json。
/// </summary>
internal sealed class WanfaLegacyImporter : ISaveLegacyImporter
{
    public bool TryImport(JObject root, out int dataVersion, out JObject data)
    {
        var blueprints = FindProperty(root, "Blueprints");
        if (blueprints == null)
        {
            dataVersion = 0;
            data = null;
            return false;
        }

        var versionProperty = FindProperty(root, "SchemaVersion");
        dataVersion = versionProperty?.Value.Value<int>() ?? 1;
        data = (JObject)root.DeepClone();
        var clonedVersion = FindProperty(data, "SchemaVersion");
        clonedVersion?.Remove();
        return true;
    }

    private static JProperty FindProperty(JObject root, string name)
    {
        return root.Properties().FirstOrDefault(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
