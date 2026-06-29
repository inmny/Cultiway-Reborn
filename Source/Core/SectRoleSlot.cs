namespace Cultiway.Core;

/// <summary>
/// 宗门角色槽位；一个成员可以同时拥有门阶、职司和头衔。
/// </summary>
public enum SectRoleSlot
{
    /// <summary>
    /// 门人层级，例如外门弟子、内门弟子、亲传弟子。
    /// </summary>
    Grade,

    /// <summary>
    /// 管理职司，例如执事、长老、掌门。
    /// </summary>
    Office,

    /// <summary>
    /// 特殊头衔，例如衣钵传人。
    /// </summary>
    Title
}
