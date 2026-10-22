using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧世界材料没有额外热量或燃烧，不改变原体积与来源。</summary>
internal sealed class WorldMaterialsV2ToV3 : ISaveMigration
{
    /// <summary>原材料区段版本。</summary>
    public int FromVersion => 2;

    /// <summary>显式补齐未加热状态，不能凭木材名称自动点火。</summary>
    /// <param name="data">存档层复制后的旧区段。</param>
    public JObject Apply(JObject data)
    {
        if (data["Records"] is JArray rows)
            foreach (JObject row in rows.Children<JObject>())
                if (row["Material"] is JObject material)
                    material["Burning"] = JValue.CreateNull();
        return data;
    }
}