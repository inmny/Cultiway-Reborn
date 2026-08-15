namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>
/// 表示只能经由指定小世界实例边界提交的命令。
/// </summary>
internal interface ISubWorldCommand
{
    /// <summary>命令所属的小世界实例 ID。</summary>
    long InstanceId { get; }
}

/// <summary>
/// 描述小世界 Runtime 从创建到销毁的生命周期状态。
/// </summary>
internal enum SubWorldRuntimeState
{
    /// <summary>对象已构造，但尚未开始推进。</summary>
    Created,

    /// <summary>实例可以接收命令并执行固定 tick。</summary>
    Running,

    /// <summary>实例资源已经销毁。</summary>
    Collapsed
}
