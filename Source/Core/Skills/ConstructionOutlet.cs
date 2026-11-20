using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只放出这一个已声明部位；多份材料分别声明，就能控制同一出口的放出次序和总量。</summary>
public sealed class ConstructionOutlet
{
    /// <summary>父部位开始后至少经过的世界秒数。</summary>
    public float After;

    /// <summary>出口朝向低于水平面的最小角度，须大于零且不超过九十度。</summary>
    public float DownDegrees = 15f;
}

/// <summary>在资料边界检查并换算一次，推进时只比较当前出口朝向。</summary>
internal sealed class CompiledOutlet
{
    /// <summary>父部位最早可用时点和实际向下方向比例。</summary>
    internal readonly float After, Downward;

    /// <summary>检查有限时点、角度及实际挂点。</summary>
    /// <param name="source">可编辑的出口条件。</param>
    /// <param name="anchored">确实有命名父挂点且仍使用它的位置。</param>
    /// <param name="lifetime">整次施放的存在上限。</param>
    /// <param name="path">资料位置。</param>
    internal CompiledOutlet(ConstructionOutlet source, bool anchored, float lifetime, string path)
    {
        if (!anchored || float.IsNaN(source.After) || source.After < 0f || source.After >= lifetime ||
            float.IsNaN(source.DownDegrees) || source.DownDegrees <= 0f || source.DownDegrees > 90f)
            throw new InvalidDataException(path + "：出流需要真实父挂点、有效时点及大于零至九十度的向下角度");
        After = source.After;
        Downward = Mathf.Sin(source.DownDegrees * Mathf.Deg2Rad);
    }
}
