using System;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门岗位资产，用于描述宗门运行期可以分配给成员的临时工作。
/// </summary>
public class SectJobAsset : Asset
{
    /// <summary>
    /// 岗位名称的本地化 key。
    /// </summary>
    public string nameKey;

    /// <summary>
    /// 岗位说明的本地化 key。
    /// </summary>
    public string descriptionKey;

    /// <summary>
    /// 岗位在调试显示或 UI 中使用的图标路径。
    /// </summary>
    public string pathIcon;

    /// <summary>
    /// 被分配后实际切换到的 ActorJob id。
    /// </summary>
    public string actorJobId;

    /// <summary>
    /// 执行该岗位需要的宗门权限。
    /// </summary>
    public SectPermissionAsset requiredPermission;

    /// <summary>
    /// 多个岗位同时可用时的排序权重，数值越大越优先。
    /// </summary>
    public float priority;

    /// <summary>
    /// 是否纳入宗门通用岗位刷新。
    /// </summary>
    public bool commonJob = true;

    /// <summary>
    /// 根据宗门当前状态计算该岗位需要提供的名额。
    /// </summary>
    public Func<Sect, int> countJobs;

    /// <summary>
    /// 额外判断指定成员是否适合领取该岗位。
    /// </summary>
    public Func<Actor, Sect, bool> shouldBeAssigned;

    /// <summary>
    /// 获取岗位的本地化名称。
    /// </summary>
    public string GetName()
    {
        return (string.IsNullOrEmpty(nameKey) ? id : nameKey).Localize();
    }

    /// <summary>
    /// 获取岗位的本地化说明。
    /// </summary>
    public string GetDescription()
    {
        return (string.IsNullOrEmpty(descriptionKey) ? $"{id}.Info" : descriptionKey).Localize();
    }
}
