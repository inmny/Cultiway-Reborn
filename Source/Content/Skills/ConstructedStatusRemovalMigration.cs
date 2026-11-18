using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>原恢复声明未指定解除，保留完整旧资料、原行为和费用。</summary>
internal sealed class ConstructionsV15ToV16 : ISaveMigration
{
    /// <summary>上一版正式世界构造区段。</summary>
    public int FromVersion => 15;

    /// <summary>保留旧构造完整资料，不为旧恢复添加解除。</summary>
    /// <param name="data">旧区段的独立副本。</param>
    public JObject Apply(JObject data) => data;
}
