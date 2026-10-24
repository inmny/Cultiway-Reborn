using System.Linq;
using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>集合选择有独立格式边界，旧命名引用明确使用原单项含义。</summary>
internal sealed class ConstructionsV3ToV4 : ISaveMigration
{
    /// <summary>原单项输出绑定版本。</summary>
    public int FromVersion => 3;
    /// <summary>在副本补原单项选择，不更改真实部位或操作参数。</summary>
    /// <param name="data">框架提供的旧部分副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Definitions"] is JArray definitions)
            foreach (var definition in definitions.OfType<JObject>())
                if (definition["Steps"] is JArray steps)
                    foreach (var step in steps.OfType<JObject>())
                        if (step["Inputs"] is JObject inputs)
                            foreach (var reference in inputs.Properties().Select(p => p.Value).OfType<JObject>()) reference["Mode"] ??= 0;
        return data;
    }
}
