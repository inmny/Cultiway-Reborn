using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧独立水层保留原事实，首次正常推进再按实际容量转入共同格网。</summary>
internal sealed class WorldMaterialsV1ToV2 : ISaveMigration
{
    /// <summary>原区段版本。</summary>
    public int FromVersion => 1;

    /// <summary>补齐来源链，不修改旧水深、支付份额及收束累计。</summary>
    /// <param name="data">存档层已经复制的旧区段。</param>
    public JObject Apply(JObject data)
    {
        if (data["Records"] is JArray rows)
            foreach (JObject row in rows.Children<JObject>())
            {
                row["Lineage"] = row["Id"]?.DeepClone();
                row["WorldGrid"] = false;
            }

        data["RecoveryGroups"] = new JArray();
        return data;
    }
}