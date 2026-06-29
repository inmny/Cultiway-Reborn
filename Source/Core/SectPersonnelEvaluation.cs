using Cultiway.Core.Libraries;

namespace Cultiway.Core;

/// <summary>
/// 宗门人事评定结果，表示成员在各角色槽位上的目标晋升。
/// </summary>
public readonly struct SectPersonnelEvaluation
{
    /// <summary>
    /// 创建一次人事评定结果。
    /// </summary>
    public SectPersonnelEvaluation(SectRoleAsset grade, SectRoleAsset office, SectRoleAsset title)
    {
        Grade = grade;
        Office = office;
        Title = title;
    }

    /// <summary>
    /// 评定后的目标门阶；为 null 表示不调整门阶。
    /// </summary>
    public SectRoleAsset Grade { get; }

    /// <summary>
    /// 评定后的目标职司；为 null 表示不调整职司。
    /// </summary>
    public SectRoleAsset Office { get; }

    /// <summary>
    /// 评定后的目标头衔；为 null 表示不调整头衔。
    /// </summary>
    public SectRoleAsset Title { get; }

    /// <summary>
    /// 是否存在任一需要应用的角色调整。
    /// </summary>
    public bool HasTarget => Grade != null || Office != null || Title != null;
}
