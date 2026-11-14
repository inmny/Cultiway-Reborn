using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>飞行实际使用的数值目标，不读取人物或显示对象。</summary>
public enum ConstructionFlightAnchor
{
    /// <summary>本次目标，优先保留部位已经明确取得的独立目标。</summary>
    Target = 0,
    /// <summary>本次来源的实际位置，可用于原主体返回。</summary>
    Source = 1,
    /// <summary>这一次飞行开始时的固定出手方向，不把有限目标点当作无穷方向。</summary>
    Direction = 2
}

/// <summary>进入真实到达距离后的行程处理，与伤害和接触次数分开。</summary>
public enum ConstructionFlightArrival
{
    /// <summary>只选择目标，不处理到达。</summary>
    None = 0,
    /// <summary>不再追踪，保留已经取得的运动和曲线。</summary>
    Continue = 1,
    /// <summary>停止整体位移，内部动作和其他部位继续。</summary>
    Stop = 2,
    /// <summary>结束本部位及仍依赖它的后代，已脱离分支继续。</summary>
    End = 3
}

/// <summary>可保存的飞行目标；未配置时保留原来的持续追踪行为。</summary>
public sealed class ConstructionFlightGoal
{
    /// <summary>采用本次目标或本次来源。</summary>
    public ConstructionFlightAnchor Anchor;
    /// <summary>是否随实际输入更新；关闭时在这次飞行开始时固定。</summary>
    public bool Follow = true;
    /// <summary>真实到达后的处理。</summary>
    public ConstructionFlightArrival Arrival = ConstructionFlightArrival.Stop;
    /// <summary>部位运动中心进入此距离即到达，不参与伤害范围计算。</summary>
    public float Radius = .05f;
}

/// <summary>编译边界复制后的只读目标规则。</summary>
internal sealed class CompiledFlightGoal
{
    /// <summary>数值参照。</summary>
    internal readonly ConstructionFlightAnchor Anchor;
    /// <summary>是否持续更新。</summary>
    internal readonly bool Follow;
    /// <summary>到达处理。</summary>
    internal readonly ConstructionFlightArrival Arrival;
    /// <summary>明确的行程到达距离。</summary>
    internal readonly float Radius;

    /// <summary>在可编辑资料进入时一次核对，不在逐帧运行中检查枚举。</summary>
    /// <param name="source">外部规则。</param><param name="path">具体资料位置。</param>
    internal CompiledFlightGoal(ConstructionFlightGoal source, string path)
    {
        if (!Enum.IsDefined(typeof(ConstructionFlightAnchor), source.Anchor) ||
            !Enum.IsDefined(typeof(ConstructionFlightArrival), source.Arrival) ||
            float.IsNaN(source.Radius) || float.IsInfinity(source.Radius) || source.Radius < .01f || source.Radius > 10000f ||
            source.Anchor == ConstructionFlightAnchor.Direction && (source.Follow || source.Arrival != ConstructionFlightArrival.None))
            throw new InvalidDataException(path + "：飞行目标、到达处理或0.01至一万格内的距离无效");
        Anchor = source.Anchor; Follow = source.Follow; Arrival = source.Arrival; Radius = source.Radius;
    }
}
