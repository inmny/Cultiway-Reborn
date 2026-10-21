using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>一次已经完成的形体显示状态，两个视角只读取，不负责推进动作。</summary>
public readonly struct FormVisualInstance
{
    /// <summary>共用形体。</summary>
    public readonly FormResource Resource;
    /// <summary>地图位置、朝向和尺寸。</summary>
    public readonly Matrix4x4 Transform;
    /// <summary>各部件当前控制变换；为空时均使用单位变换。</summary>
    public readonly Matrix4x4[] Parts;
    /// <summary>明确覆盖显示表面；空值使用部件默认表面。</summary>
    public readonly string Surface;

    /// <summary>记录已经确定的姿态，发布后不修改部件数组。</summary>
    /// <param name="resource">共享资源。</param>
    /// <param name="transform">地图空间变换。</param>
    /// <param name="parts">与资源部件对应的控制变换。</param>
    /// <param name="surface">显示表面覆盖。</param>
    public FormVisualInstance(FormResource resource, Matrix4x4 transform, Matrix4x4[] parts = null, string surface = null)
    {
        Resource = resource; Transform = transform; Parts = parts; Surface = surface;
    }

    /// <summary>取得用于模型、挂点和作用形状的相同部位变换。</summary>
    /// <param name="part">部件索引。</param>
    public Matrix4x4 PartTransform(int part) => Transform * (Parts == null ? Matrix4x4.identity : Parts[part]);
}

/// <summary>保存最后一次完整发布的形体状态，不持有动作时钟或游戏修改入口。</summary>
public static class FormVisualScene
{
    /// <summary>各实际生产者的完整状态，避免能力更新覆盖开发探针或其他来源。</summary>
    private static readonly Dictionary<string, FormVisualInstance[]> sources = new(StringComparer.Ordinal);
    /// <summary>本轮可供两个显示方式读取的固定状态。</summary>
    public static IReadOnlyList<FormVisualInstance> Instances { get; private set; } = Array.Empty<FormVisualInstance>();

    /// <summary>发布完整状态，调用方之后不再修改数组和部件变换。</summary>
    /// <param name="instances">已经完成更新的全部形体。</param>
    public static void Publish(FormVisualInstance[] instances) => Publish("probe", instances);

    /// <summary>替换一个来源的整份状态，各来源不直接修改其他施放的状态。</summary>
    /// <param name="source">稳定的生产者编号。</param>
    /// <param name="instances">已经完成更新的形体。</param>
    public static void Publish(string source, FormVisualInstance[] instances)
    {
        if (instances.Length == 0)
        {
            if (!sources.Remove(source)) return;
        }
        else sources[source] = instances;
        var combined = new List<FormVisualInstance>();
        foreach (FormVisualInstance[] state in sources.Values) combined.AddRange(state);
        Instances = combined.ToArray();
    }

    /// <summary>切换世界时清理显示引用，共用资源继续复用。</summary>
    public static void Clear()
    {
        sources.Clear(); Instances = Array.Empty<FormVisualInstance>();
    }
}
