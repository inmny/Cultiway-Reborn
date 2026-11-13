using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧单轴只在保存读取边界转换，当前运行统一使用真实三轴。</summary>
internal static class BodyOrganRotationSaveMigrations
{
    /// <summary>保留原单轴值和全部动作时点，不重做关键点。</summary>
    /// <param name="definition">旧构造或固定输入中的完整构造。</param>
    internal static void Convert(JObject definition)
    {
        if (definition?["Body"] is not JObject body || body["Keys"] is not JArray keys) return;
        foreach (JObject key in keys.Children<JObject>())
            if (key["PartAngle"] is JToken angle)
            {
                if (key["PartRotation"] == null) key["PartRotation"] = new JArray(angle.DeepClone(), 0f, 0f);
                key.Remove("PartAngle");
            }
    }
}

/// <summary>正式世界构造从单轴升级为三轴器官姿态。</summary>
internal sealed class ConstructionsV10ToV11 : ISaveMigration
{
    /// <summary>已有自动部位控制的旧版本。</summary>
    public int FromVersion => 10;
    /// <summary>逐份转换旧关键点，正常资料和其他部位保持不变。</summary>
    /// <param name="data">保存层提供的独立旧资料。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (JObject definition in definitions.Children<JObject>()) BodyOrganRotationSaveMigrations.Convert(definition);
        return data;
    }
}

/// <summary>待完成创法请求保留原单轴输入的实际含义。</summary>
internal sealed class CreationV7ToV8 : ISaveMigration
{
    /// <summary>已有自动部位缺省转换的请求版本。</summary>
    public int FromVersion => 7;
    /// <summary>转换来源和身体快照，不改变种子及生成规则。</summary>
    /// <param name="data">保存层提供的旧请求副本。</param>
    public JObject Apply(JObject data)
    {
        BodyOrganRotationSaveMigrations.Convert(data["Source"] as JObject);
        if (data["Resources"] is JObject resources && resources["Bodies"] is JArray bodies)
            foreach (JObject body in bodies.Children<JObject>()) BodyOrganRotationSaveMigrations.Convert(body);
        return data;
    }
}
