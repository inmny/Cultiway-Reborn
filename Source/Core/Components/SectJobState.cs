using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 宗门岗位运行态。只表示单位当前领取的宗门事务，不进入 ActorData 持久化。
/// </summary>
public struct SectJobState : IComponent
{
    /// <summary>
    /// 当前领取的宗门岗位。
    /// </summary>
    public SectJobAsset Job;

    /// <summary>
    /// 分配该岗位的宗门 ID，用于释放占用和校验任务归属。
    /// </summary>
    public long SectId;
}
