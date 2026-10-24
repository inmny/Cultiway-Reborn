using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>原操作输出增加明确格式边界，旧定义无需猜测或补造操作名称。</summary>
internal sealed class ConstructionsV4ToV5 : ISaveMigration
{
    /// <summary>原命名步骤版本。</summary>
    public int FromVersion => 4;
    /// <summary>旧定义原样有效，新名称及部位操作引用均为可选资料。</summary>
    /// <param name="data">框架提供的旧部分副本。</param>
    public JObject Apply(JObject data) => data;
}
