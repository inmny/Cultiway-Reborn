namespace Cultiway.Core.Performance;

/// <summary>
/// 允许单个 ECS 系统在保持系统顺序的前提下跨帧推进。
/// </summary>
internal interface ICooperativeSystemStep
{
    string CooperativePhaseName { get; }

    /// <summary>
    /// 推进一步；返回 true 表示当前系统已经完成，可以进入下一个系统。
    /// </summary>
    bool StepCooperatively();
}
