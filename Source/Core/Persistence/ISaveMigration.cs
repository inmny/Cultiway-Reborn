using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Persistence;

/// <summary>
/// 将一个持久化文档的数据从指定版本迁移到紧邻的下一版本。
/// </summary>
public interface ISaveMigration
{
    int FromVersion { get; }

    JObject Apply(JObject data);
}
