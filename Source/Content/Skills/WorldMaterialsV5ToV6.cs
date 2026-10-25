using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧单资源本金转换为组成与逐资源累计，不重新报价或回写原档。</summary>
internal sealed class WorldMaterialsV5ToV6 : ISaveMigration
{
    /// <summary>上一个世界材料正式区段版本。</summary>
    public int FromVersion => 5;
    /// <summary>保留原份额及进度，只有表示方式改变。</summary>
    /// <param name="data">保存层提供的旧区段副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Records"] is JArray records)
            foreach (JObject row in records.Children<JObject>())
            {
                string resource = Canonical(row.Value<string>("Resource"));
                row["Funding"] = new JObject { ["Entries"] = resource == "none" ? new JArray() : new JArray(new JObject { ["Resource"] = resource, ["Rate"] = 1d }) };
                row["Credits"] = Book(row, resource);
                row.Remove("Resource"); row.Remove("Confirmed"); row.Remove("Fraction");
            }
        if (data["RecoveryGroups"] is JArray groups)
            foreach (JObject group in groups.Children<JObject>())
            {
                string resource = Canonical(group.Value<string>("Resource"));
                group["Credits"] = Book(group, resource);
                group["Completed"] = resource == "none" ? new JObject() : new JObject { [resource] = group["Completed"]?.DeepClone() ?? new JValue(0d) };
                group.Remove("Resource"); group.Remove("Confirmed"); group.Remove("Fraction");
            }
        return data;
    }
    /// <summary>旧短名只转换为同一实际通道的稳定编号。</summary>
    /// <param name="resource">旧保存编号。</param>
    private static string Canonical(string resource) => resource == null ? "" : ConstructionResources.Resolve(resource)?.id ?? resource;
    /// <summary>保留原来已经确认和未表示的金额。</summary>
    /// <param name="row">旧材料或共同过程。</param><param name="resource">实际稳定通道。</param>
    private static JObject Book(JObject row, string resource) => new()
    {
        ["Entries"] = resource == "none" ? new JObject() : new JObject
        {
            [resource] = new JObject
            { ["Confirmed"] = row["Confirmed"]?.DeepClone() ?? new JValue(0d), ["Fraction"] = row["Fraction"]?.DeepClone() ?? new JValue(0d) }
        }
    };
}
