using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>按父挂点布局汇入的完整目标，编译时复制，运行不再读取草稿。</summary>
internal sealed class CompiledGather
{
    /// <summary>初始操作的开始时点；条件操作由已经确认的事件决定开始。</summary>
    internal readonly float At;
    /// <summary>汇入布局需要的世界秒数。</summary>
    internal readonly float Seconds;
    /// <summary>最终位置在父挂点内的坐标。</summary>
    internal readonly Vector3 Target;
    /// <summary>最终局部朝向，不包含独立旋转操作。</summary>
    internal readonly Quaternion Rotation;

    /// <summary>在同一构造边界检查聚合目标、历时与朝向。</summary>
    /// <param name="source">当前操作。</param>
    /// <param name="target">已经读取的目标位置。</param>
    /// <param name="rotation">节点原朝向，供缺省值使用。</param>
    /// <param name="lifetime">整次存在上限。</param>
    /// <param name="path">当前部位路径。</param>
    internal CompiledGather(ConstructionOperation source, Vector3 target, Quaternion rotation, float lifetime, string path)
    {
        At = source.At; Seconds = source.Seconds; Target = target; Rotation = rotation;
        if (Seconds < .05f || Seconds > lifetime || source.When == null && At + Seconds > lifetime || target.magnitude > 10000f)
            throw new InvalidDataException(path + "：聚合至少需要0.05秒，目标须在父挂点一万格内，初始过程须在存在上限内完成");
        if (source.GatherRotation == null) return;
        float[] angles = source.GatherRotation;
        if (angles.Length != 3 || angles.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            throw new InvalidDataException(path + "：聚合朝向需要三个有限转角");
        Rotation = Quaternion.Euler(angles[0], angles[1], angles[2]);
    }
}
