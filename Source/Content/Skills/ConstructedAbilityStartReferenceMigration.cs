using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>就绪操作有明确的正式格式边界，旧构造保持完整资料。</summary>
internal sealed class ConstructionsV11ToV12 : ISaveMigration
{
    /// <summary>原正式世界构造版本。</summary>
    public int FromVersion => 11;
    /// <summary>旧定义不追补就绪标记，不改变已保存的开始条件。</summary><param name="data">旧区段独立副本。</param>
    public JObject Apply(JObject data) => data;
}

/// <summary>旧创法请求继续原放置引用规则，不重抽选择或结果。</summary>
internal sealed class CreationV22ToV23 : ISaveMigration
{
    /// <summary>原正式人物请求版本。</summary>
    public int FromVersion => 22;
    /// <summary>仅升版；原规则号、完整输入及费用继续保持。</summary><param name="data">旧请求独立副本。</param>
    public JObject Apply(JObject data) => data;
}
