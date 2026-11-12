using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>正式格式声明结果恢复；未设规则的旧动作仍按其原制作过程结束。</summary>
internal sealed class ConstructionsV7ToV8 : ISaveMigration
{
    /// <summary>上一正式构造版本。</summary>
    public int FromVersion => 7;
    /// <summary>只补明确的空规则，不覆盖已经保存的恢复姿态或秒数。</summary>
    /// <param name="data">保存层提供的旧资料副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (JObject definition in definitions.Children<JObject>())
                if (definition["Body"] is JObject body && body["Responses"] == null) body["Responses"] = new JArray();
        return data;
    }
}

/// <summary>新请求能够改造收势，旧请求继续使用已保存的规则号和随机输入。</summary>
internal sealed class CreationV4ToV5 : ISaveMigration
{
    /// <summary>已经显式保存规则号的请求格式。</summary>
    public int FromVersion => 4;
    /// <summary>不重新抽取、不把旧规则号改为当前规则；升版让旧程序明确拒绝未来请求。</summary>
    /// <param name="data">保存层提供的旧请求副本。</param>
    public JObject Apply(JObject data) => data;
}
