using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>相对真实目标方向的偏转；改变追踪方向，不另外平移主体或到达位置。</summary>
public sealed class ConstructionFlightSteering
{
    /// <summary>横向偏转与向前分量的比例，正值沿目标方向左侧。</summary>
    public float Side;
    /// <summary>离地偏转与向前分量的比例，正值沿目标方向上方。</summary>
    public float Lift;
    /// <summary>每单位曲线进度的指数衰减率；零保留原幅度，循环曲线也使用完整累计进度。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float Decay;
    /// <summary>两轴偏转的相对变化，横坐标改变横偏，纵坐标改变离地偏转。</summary>
    public ConstructionPath Path;
}

/// <summary>准备时复制的偏转数值，运行与预测共用。</summary>
internal sealed class CompiledFlightSteering
{
    /// <summary>固定横向及离地偏转。</summary>
    internal readonly Vector2 Bias;
    /// <summary>横偏、离地偏转和曲线共同采用的非负衰减率。</summary>
    internal readonly float Decay;
    /// <summary>可选的普通曲线，只取横向与离地两个数值。</summary>
    internal readonly CompiledConstructionPath Path;

    /// <summary>在资料边界核对方向曲线含义，再复制给数值执行。</summary>
    /// <param name="source">当前可编辑偏转。</param><param name="path">具体错误位置。</param>
    internal CompiledFlightSteering(ConstructionFlightSteering source, string path)
    {
        if (!Finite(source.Side) || !Finite(source.Lift) || Mathf.Abs(source.Side) > 10000f || Mathf.Abs(source.Lift) > 10000f ||
            !Finite(source.Decay) || source.Decay < 0f || source.Decay > 10000f)
            throw new InvalidDataException(path + "：方向偏转需要一万以内的有限数值，衰减率必须非负");
        Bias = new Vector2(source.Side, source.Lift); Decay = source.Decay;
        if (source.Path == null) return;
        Path = new CompiledConstructionPath(source.Path, path + "/Path");
        if (source.Path.Space != ConstructionPathSpace.Local || source.Path.Forward != null ||
            source.Path.Keys.Any(key => key.Point[2] != 0f || key.In[2] != 0f || key.Out[2] != 0f))
            throw new InvalidDataException(path + "/Path：方向曲线只用横向和离地两个轴，不采用位置参照或模型朝向");
    }

    /// <summary>先叠加普通两轴曲线，再按完整累计进度衰减；循环不会重新放大。</summary>
    /// <param name="time">按世界时间或真实中心路程换算的累计进度。</param>
    internal Vector2 Sample(float time)
    {
        Vector2 bias = Bias;
        if (Path != null)
        {
            Path.Sample(time, out Vector3 change, out _);
            bias += new Vector2(change.x, change.y);
        }
        return Decay == 0f ? bias : bias * Mathf.Exp(-Decay * time);
    }

    /// <summary>外部数字的有限检查。</summary><param name="value">待输入的偏转。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
