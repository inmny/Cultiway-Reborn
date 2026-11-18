using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧材料保留有效侵蚀进度，缺省没有变速时段。</summary>
internal sealed class WorldMaterialsV8ToV9 : ISaveMigration
{
    /// <summary>上一正式材料区段。</summary>
    public int FromVersion => 8;
    /// <summary>保留原结果、剩余历时和等待编号。</summary>
    /// <param name="data">旧区段的独立副本。</param>
    public JObject Apply(JObject data) => data;
}
