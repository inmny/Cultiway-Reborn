using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>占用改为自动推导后，旧资料缺省仍保留原来的全身限制。</summary>
internal static class BodyOccupancySaveMigrations
{
    /// <summary>只补旧缺省，不修改作者明确写入的部位和姿态。</summary>
    /// <param name="definition">旧完整构造或固定输入中的构造快照。</param>
    internal static void Preserve(JObject definition)
    {
        if (definition?["Body"] is JObject body && body["Occupancy"] == null) body["Occupancy"] = 5;
    }
}

/// <summary>正式世界构造改用实际部位推导，旧声明继续作为额外预留。</summary>
internal sealed class ConstructionsV9ToV10 : ISaveMigration
{
    /// <summary>已有阶段但尚未自动推导部位的正式格式。</summary>
    public int FromVersion => 9;
    /// <summary>转换旧缺省，运行只使用当前编译和控制入口。</summary>
    /// <param name="data">保存层提供的独立旧资料。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (JObject definition in definitions.Children<JObject>()) BodyOccupancySaveMigrations.Preserve(definition);
        return data;
    }
}

/// <summary>旧创法请求的来源及模型身体快照保留原占用，不改变其随机输入。</summary>
internal sealed class CreationV6ToV7 : ISaveMigration
{
    /// <summary>已有内部阶段创作的请求格式。</summary>
    public int FromVersion => 6;
    /// <summary>补齐旧身体缺省，原种子、规则号和队列状态保持原意。</summary>
    /// <param name="data">保存层提供的旧请求副本。</param>
    public JObject Apply(JObject data)
    {
        BodyOccupancySaveMigrations.Preserve(data["Source"] as JObject);
        if (data["Resources"] is JObject resources && resources["Bodies"] is JArray bodies)
            foreach (JObject body in bodies.Children<JObject>()) BodyOccupancySaveMigrations.Preserve(body);
        return data;
    }
}
