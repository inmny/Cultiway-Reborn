using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>保留旧侵蚀的时间和历史，新编号及本段计数使用缺省值。</summary>
internal sealed class WorldMaterialsV7ToV8 : ISaveMigration
{
    /// <summary>上一正式材料区段。</summary>
    public int FromVersion => 7;
    /// <summary>保持旧余量、累计历史及进度。</summary>
    /// <param name="data">旧区段的独立副本。</param>
    public JObject Apply(JObject data) => data;
}
