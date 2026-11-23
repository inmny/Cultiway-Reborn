using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>同一视角一帧内共用材质入口，实例色和姿态仍随部件提交。</summary>
internal sealed class FormFrameSurfaces(bool native)
{
    /// <summary>按完整资源版本区分旧场景表面和刚替换的新表面。</summary>
    private readonly Dictionary<(FormResourceGeneration, string, bool), Sample> samples = new();

    /// <summary>共同着色时间，不在各实例上反复写材质。</summary>
    private float time;

    /// <summary>开始一帧，解除之前资源的引用。</summary>
    /// <param name="worldTime">已经确认的世界时间。</param>
    internal void Begin(float worldTime)
    {
        samples.Clear();
        time = worldTime;
    }

    /// <summary>一次解析同一表面及深度资料，透明实例仍按实际覆盖处理。</summary>
    /// <param name="resource">完整资源版本。</param>
    /// <param name="surface">实际显示表面。</param>
    /// <param name="translucent">该实例是否淡出或带半透明色。</param>
    internal Sample Get(FormResource resource, string surface, bool translucent)
    {
        bool overrideDepth = native && translucent;
        var key = (resource.Generation, surface, overrideDepth);
        if (!samples.TryGetValue(key, out Sample sample))
        {
            Material material = resource.Surface(surface, native, overrideDepth);
            material.SetFloat("_WorldTime", time);
            sample = new Sample(material, material.GetColor("_Color"), material.GetFloat("_ZWrite") < .5f,
                native && Mathf.Abs(material.GetFloat("_Kind") - 29f) < .5f);
            samples.Add(key, sample);
        }
        return sample;
    }

    /// <summary>本帧已读取的共用表面、默认色及遮挡规则。</summary>
    /// <param name="material">原版或三维对应的实际材质。</param>
    /// <param name="color">默认表面色。</param>
    /// <param name="transparent">本表面需要共用透明层。</param>
    /// <param name="flatNative">着色器把薄霜压到原版地图平面。</param>
    internal readonly struct Sample(Material material, Color color, bool transparent, bool flatNative)
    {
        /// <summary>按实际视角及深度设置取得的共享材质。</summary>
        internal readonly Material Material = material;
        /// <summary>准备时读取的默认表面色。</summary>
        internal readonly Color Color = color;
        /// <summary>此表面不写入实体深度。</summary>
        internal readonly bool Transparent = transparent;
        /// <summary>原版采用特殊贴地投影。</summary>
        internal readonly bool FlatNative = flatNative;
    }
}
