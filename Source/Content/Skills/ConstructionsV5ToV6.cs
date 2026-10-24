using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧构造没有未取整基准，保留已有费用和恢复时间，第一次明确改造才建立基准。</summary>
internal sealed class ConstructionsV5ToV6 : ISaveMigration
{
    /// <summary>已有统一命名输出的正式格式。</summary>
    public int FromVersion => 5;

    /// <summary>相邻版本不重新定价，不补写旧构造的自动计算结果。</summary>
    /// <param name="data">保存管理器提供的旧资料副本。</param>
    public JObject Apply(JObject data) => data;
}
