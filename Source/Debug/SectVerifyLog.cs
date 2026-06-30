using Cultiway.Core;
using Cultiway.Core.Logging;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;

namespace Cultiway.Debug;

/// <summary>
/// 宗门功能验证日志工具；统一前缀便于在 Player.log 中过滤完整链路。
/// </summary>
public static class SectVerifyLog
{
    private const string Prefix = "[SectVerify]";

    /// <summary>
    /// 是否输出宗门验证日志。
    /// </summary>
    public static bool Enabled { get; set; } = true;

    public static bool ShouldLog => Enabled && CultiLog.Sect.VerifyEnabled;

    /// <summary>
    /// 输出带宗门验证前缀的日志。
    /// </summary>
    public static void Log(string action, string message)
    {
        if (!ShouldLog) return;

        CultiLog.Sect.Verify(action, message);
    }

    /// <summary>
    /// 获取日志中的人物标识。
    /// </summary>
    public static string Actor(Actor actor)
    {
        return actor.isRekt() ? "null" : $"{actor.getName()}#{actor.data.id}";
    }

    /// <summary>
    /// 获取日志中的宗门标识。
    /// </summary>
    public static string Sect(Sect sect)
    {
        return sect == null || sect.isRekt() ? "null" : $"{sect.name}#{sect.getID()}";
    }

    /// <summary>
    /// 获取日志中的宗门角色标识。
    /// </summary>
    public static string Role(SectRoleAsset role)
    {
        return role == null ? "null" : role.id;
    }

    /// <summary>
    /// 获取日志中的师徒关系类型标识。
    /// </summary>
    public static string Relation(MasterApprenticeTypeAsset relation)
    {
        return relation == null ? "null" : relation.id;
    }

    /// <summary>
    /// 获取日志中的书籍标识。
    /// </summary>
    public static string Book(Book book)
    {
        if (book == null || book.isRekt()) return "null";
        return $"{book.data.name}#{book.id}/{book.getAsset()?.id ?? "unknown"}";
    }
}
