using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧世界时间安排仍从原有效时间零点开始，原进度原样接续。</summary>
internal sealed class WorldMaterialsV9ToV10 : ISaveMigration
{
    /// <summary>上一正式材料区段。</summary>
    public int FromVersion => 9;
    /// <summary>保留实际损耗及当时的时间安排。</summary>
    /// <param name="data">旧区段的独立副本。</param>
    public JObject Apply(JObject data) => data;
}
