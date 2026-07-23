using Cultiway.Core.Persistence;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.SkillLibV3.Wanfa.Persistence.Migrations;

/// <summary>
/// 启用严格运行形态契约。旧 ID 不做别名映射，由规范化阶段删除不再兼容的蓝图。
/// </summary>
internal sealed class WanfaPavilionV3ToV4 : ISaveMigration
{
    public int FromVersion => 3;

    public JObject Apply(JObject data)
    {
        return data;
    }
}
