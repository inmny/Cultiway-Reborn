namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门事务资产；描述一个宗门日常事务的准入权限、职位要求、执行任务和贡献奖励。
/// </summary>
public class SectAffairAsset : Asset
{
    /// <summary>
    /// 事务名称的本地化 key。
    /// </summary>
    public string nameKey;

    /// <summary>
    /// 事务说明的本地化 key。
    /// </summary>
    public string descriptionKey;

    /// <summary>
    /// 事务在任务或 UI 中使用的图标路径。
    /// </summary>
    public string iconPath;

    /// <summary>
    /// 执行该事务需要拥有的权限；为空表示不按权限限制。
    /// </summary>
    public SectPermissionAsset requiredPermission;

    /// <summary>
    /// 执行该事务需要的最低或精确职司；为空表示不按职司限制。
    /// </summary>
    public SectRoleAsset requiredOfficeRole;

    /// <summary>
    /// 为 true 时按最低职司判断，为 false 时要求职司完全一致。
    /// </summary>
    public bool requireOfficeAtLeast = true;

    /// <summary>
    /// 实际执行该事务的 ActorTask id。
    /// </summary>
    public string taskId;

    /// <summary>
    /// 事务成功完成后给予执行者的宗门贡献。
    /// </summary>
    public int contributionReward;

    /// <summary>
    /// 多个事务都可执行时的随机权重。
    /// </summary>
    public float weight = 1f;
}
