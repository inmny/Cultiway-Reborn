using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Combat;

/// <summary>旧版地面记录没有形体来源，保留原物品和数值，继续按当前物品外观恢复。</summary>
internal sealed class GroundWeaponsV1ToV2 : ISaveMigration
{
    /// <summary>上一版正式地面资料。</summary>
    public int FromVersion => 1;

    /// <summary>不能猜测已经缺失的主动形体，明确使用旧版物品重建。</summary>
    /// <param name="data">存档层复制后的资料。</param>
    public JObject Apply(JObject data)
    {
        if (data["Weapons"] is JArray weapons)
            foreach (JObject item in weapons.Children<JObject>()) item["AppearanceSource"] = "item";
        return data;
    }
}
