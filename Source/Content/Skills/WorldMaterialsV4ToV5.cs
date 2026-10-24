using System.Collections.Generic;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧分区按已有来源及实际材料补齐同份身份，不倒推不存在的转换历史。</summary>
internal sealed class WorldMaterialsV4ToV5 : ISaveMigration
{
    /// <summary>原世界材料区段版本。</summary>
    public int FromVersion => 4;

    /// <summary>共同分区共用已有编号，独立物件保留自己的编号，坏记录仍由恢复入口逐条处理。</summary>
    /// <param name="data">存档层复制的旧区段。</param>
    public JObject Apply(JObject data)
    {
        var identities = new Dictionary<(string, string, string, string), long>();
        long next = data.Value<long>("Next");
        if (data["Records"] is not JArray records) return data;
        foreach (JObject row in records.Children<JObject>())
        {
            if (row["Id"]?.Type != JTokenType.Integer || !long.TryParse(row["Id"].ToString(), out long id) || id <= 0 || id >= next)
            { row["Identity"] = row["Id"]?.DeepClone(); continue; }
            if (row["WorldGrid"]?.Type != JTokenType.Boolean || !row.Value<bool>("WorldGrid") || row["Material"] is not JObject material)
            { row["Identity"] = id; continue; }
            var key = (row["Lineage"]?.ToString(), material["Matter"]?.ToString(), material["State"]?.ToString(), material["ShapeHeld"]?.ToString());
            if (!identities.TryGetValue(key, out long identity)) identities.Add(key, identity = id);
            row["Identity"] = identity;
        }
        return data;
    }
}
