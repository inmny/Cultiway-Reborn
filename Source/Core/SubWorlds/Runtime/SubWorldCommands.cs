namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>
/// 请求指定实体移动到目标格子；Runtime 在边界验证后将其交给移动领域队列。
/// </summary>
internal sealed class MoveToTileCommand : ISubWorldCommand
{
    /// <summary>
    /// 创建移动请求。
    /// </summary>
    /// <param name="instanceId">目标小世界实例 ID。</param>
    /// <param name="revision">提交命令时观察到的 Runtime Revision。</param>
    /// <param name="entityId">待移动实体在目标 Runtime Store 中的 ID。</param>
    /// <param name="targetTileIndex">目标格子的 row-major 索引。</param>
    internal MoveToTileCommand(long instanceId, long revision, int entityId, int targetTileIndex)
    {
        InstanceId = instanceId;
        Revision = revision;
        EntityId = entityId;
        TargetTileIndex = targetTileIndex;
    }

    /// <inheritdoc />
    public long InstanceId { get; }

    /// <summary>提交命令时观察到的 Runtime Revision。</summary>
    internal long Revision { get; }

    /// <summary>待移动实体在目标 Runtime Store 中的 ID。</summary>
    internal int EntityId { get; }

    /// <summary>目标格子的 row-major 索引。</summary>
    internal int TargetTileIndex { get; }
}

/// <summary>
/// 设置指定小世界实例的局部暂停状态。
/// </summary>
internal sealed class PauseCommand : ISubWorldCommand
{
    /// <summary>
    /// 创建局部暂停命令。
    /// </summary>
    /// <param name="instanceId">目标小世界实例 ID。</param>
    /// <param name="paused">是否暂停；恢复时回到暂停前的非零局部速度。</param>
    internal PauseCommand(long instanceId, bool paused)
    {
        InstanceId = instanceId;
        Paused = paused;
    }

    /// <inheritdoc />
    public long InstanceId { get; }

    /// <summary>要设置的局部暂停状态。</summary>
    internal bool Paused { get; }
}

/// <summary>
/// 设置指定小世界实例的局部时间速度。
/// </summary>
internal sealed class SetLocalSpeedCommand : ISubWorldCommand
{
    /// <summary>
    /// 创建局部速度命令。
    /// </summary>
    /// <param name="instanceId">目标小世界实例 ID。</param>
    /// <param name="localSpeed">时钟配置允许的局部速度倍率。</param>
    internal SetLocalSpeedCommand(long instanceId, float localSpeed)
    {
        InstanceId = instanceId;
        LocalSpeed = localSpeed;
    }

    /// <inheritdoc />
    public long InstanceId { get; }

    /// <summary>要设置的局部速度倍率。</summary>
    internal float LocalSpeed { get; }
}
