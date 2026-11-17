using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>旧材料没有侵蚀过程，保留原份额、身份及回收入账。</summary>
internal sealed class WorldMaterialsV6ToV7 : ISaveMigration
{
    /// <summary>上一正式世界材料区段。</summary>
    public int FromVersion => 6;
    /// <summary>新增可选过程，旧值原样交给当前表示。</summary>
    /// <param name="data">保存层提供的旧区段副本。</param>
    public JObject Apply(JObject data) => data;
}
