using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧单资源构造明确为单项支付，不重新定价或改变原通道。</summary>
internal sealed class ConstructionsV6ToV7 : ISaveMigration
{
    /// <summary>上一个正式区段版本。</summary>
    public int FromVersion => 6;
    /// <summary>旧资料副本只补支付方式和空的额外通道。</summary>
    /// <param name="data">保存层提供的旧区段副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (JObject definition in definitions.Children<JObject>())
            { definition["ResourceMode"] = 0; definition["AdditionalResources"] = new JArray(); }
        return data;
    }
}
