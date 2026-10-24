using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>原版和三维共用固定大小的透明累加，不逐物体或逐像素建立排序列表。</summary>
internal static class FormTransparency
{
    /// <summary>颜色及权重累加目标。</summary>
    private static readonly int Accumulation = Shader.PropertyToID("_CultiwayTransparentAccumulation");
    /// <summary>仍可穿过透明体的背景份额。</summary>
    private static readonly int Transmission = Shader.PropertyToID("_CultiwayTransparentTransmission");
    /// <summary>共同双目标入口，重复使用标识而不为每个部件分配。</summary>
    private static readonly RenderTargetIdentifier[] Targets = { new(Accumulation), new(Transmission) };

    /// <summary>先分别清空两项，再绑定共同实体深度；每帧最多两张临时纹理。</summary>
    /// <param name="commands">当前相机命令。</param><param name="width">完整目标宽度。</param>
    /// <param name="height">完整目标高度。</param><param name="depth">当前场景真实深度。</param>
    internal static void Begin(CommandBuffer commands, int width, int height, RenderTargetIdentifier depth)
    {
        commands.GetTemporaryRT(Accumulation, width, height, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        commands.GetTemporaryRT(Transmission, width, height, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        commands.SetRenderTarget(Accumulation); commands.ClearRenderTarget(false, true, Color.clear);
        commands.SetRenderTarget(Transmission); commands.ClearRenderTarget(false, true, Color.white);
        commands.SetRenderTarget(Targets, depth);
        commands.SetViewport(new Rect(0, 0, width, height));
    }

    /// <summary>合成到原目标后立即释放工作区，恢复该相机的视口和深度。</summary>
    /// <param name="commands">当前命令。</param><param name="material">所属资源版本的合成材质。</param>
    /// <param name="color">原颜色目标。</param><param name="depth">原场景深度。</param>
    /// <param name="viewport">原视口。</param>
    internal static void Resolve(CommandBuffer commands, Material material, RenderTargetIdentifier color,
        RenderTargetIdentifier depth, Rect viewport)
    {
        commands.SetRenderTarget(color, depth); commands.SetViewport(viewport);
        commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        commands.ReleaseTemporaryRT(Accumulation); commands.ReleaseTemporaryRT(Transmission);
    }
}
