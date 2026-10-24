using System.Linq;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>为命名输出输入保留格式边界，旧构造明确没有新步骤。</summary>
internal sealed class ConstructionsV2ToV3 : ISaveMigration
{
    /// <summary>尚无命名步骤的正式构造部分。</summary>
    public int FromVersion => 2;
    /// <summary>只在独立副本补空步骤，旧版本也能明确拒绝新版执行资料。</summary>
    /// <param name="data">框架提供的独立部分副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (var definition in definitions.OfType<JObject>()) definition["Steps"] ??= new JArray();
        return data;
    }
}
