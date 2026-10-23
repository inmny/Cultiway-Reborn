using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧档按当时已有规则补齐定形状态，不倒推没有保存的施放历史。</summary>
internal sealed class WorldMaterialsV3ToV4 : ISaveMigration
{
    /// <summary>原材料区段版本。</summary>
    public int FromVersion => 3;

    /// <summary>旧水冰保持基底，其他旧层沿用活动状态；刚性物件没有数值层。</summary>
    /// <param name="data">存档层复制后的旧区段。</param>
    public JObject Apply(JObject data)
    {
        if (data["Records"] is JArray rows)
            foreach (JObject row in rows.Children<JObject>())
                if (row["Material"] is JObject material)
                {
                    if (material["Flow"] is not JObject flow)
                    {
                        material["ShapeHeld"] = JValue.CreateNull();
                        continue;
                    }

                    bool held = (string)material["State"] == "solid" &&
                                (string)material["Matter"] is "water" or "ice";
                    material["ShapeHeld"] = held;
                    // 旧活动冰层可能保存了暂停前的内部冲势；凝固后不让它在解冻时重现。
                    if (held && flow["Water"] is JArray water)
                        for (int i = 0; i < water.Count; i++)
                            if (i % 3 != 0)
                                water[i] = 0;
                }

        return data;
    }
}