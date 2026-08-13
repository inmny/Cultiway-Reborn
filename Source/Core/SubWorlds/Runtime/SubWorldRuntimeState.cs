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
/// 标记由小世界逻辑产生、等待 Runtime 在固定 tick 内派发的事件。
/// </summary>
internal interface ISubWorldEvent
{
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

    /// <summary>目标已达成，正在完成当前 tick。</summary>
    Completing,

    /// <summary>实例已完成，只保留结果供读取。</summary>
    Completed,

    /// <summary>实例资源已经销毁。</summary>
    Collapsed
}

/// <summary>
/// 保存第一阶段测试目标的运行状态。
/// </summary>
internal sealed class SubWorldObjectiveState
{
    /// <summary>测试目标是否已经完成。</summary>
    internal bool IsCompleted { get; set; }
}
