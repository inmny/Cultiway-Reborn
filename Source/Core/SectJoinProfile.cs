using Cultiway.Core.Libraries;

namespace Cultiway.Core;

/// <summary>
/// 入宗时一次性授予的宗门角色组合。
/// </summary>
public readonly struct SectJoinProfile
{
    /// <summary>
    /// 创建入宗角色组合；未指定的槽位会保留当前角色或使用默认角色。
    /// </summary>
    public SectJoinProfile(SectRoleAsset grade, SectRoleAsset office = null, SectRoleAsset title = null)
    {
        Grade = grade;
        Office = office;
        Title = title;
    }

    /// <summary>
    /// 入宗后授予的门阶。
    /// </summary>
    public SectRoleAsset Grade { get; }

    /// <summary>
    /// 入宗后授予的职司。
    /// </summary>
    public SectRoleAsset Office { get; }

    /// <summary>
    /// 入宗后授予的头衔。
    /// </summary>
    public SectRoleAsset Title { get; }
}
