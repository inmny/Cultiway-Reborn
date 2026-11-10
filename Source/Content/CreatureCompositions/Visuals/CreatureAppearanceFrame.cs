using UnityEngine;

namespace Cultiway.Content.CreatureCompositions.Visuals;

/// <summary>最终合成帧与同一次叠加得到的像素归属，显示和接触共用这份资料。</summary>
internal sealed class CreatureAppearanceFrame
{
    /// <summary>真正交给原版绘制的合成图片。</summary>
    internal readonly Sprite Sprite;

    /// <summary>最终透明度；图片上传后不必为接触反复从显存读取。</summary>
    internal readonly byte[] Alpha;

    /// <summary>每个像素的归属组编号，透明处没有作用表面。</summary>
    internal readonly int[] Owners;

    /// <summary>每组仍可见的器官或装备层；空名称表示所在的原身体区域。</summary>
    internal readonly string[][] Layers;

    /// <summary>接收已经完成叠加的只读资料，不再单独重画一遍器官。</summary>
    /// <param name="sprite">实际最终图片。</param><param name="alpha">逐像素透明度。</param>
    /// <param name="owners">逐像素归属。</param><param name="layers">各归属组的层名称。</param>
    internal CreatureAppearanceFrame(Sprite sprite, byte[] alpha, int[] owners, string[][] layers)
    {
        Sprite = sprite;
        Alpha = alpha;
        Owners = owners;
        Layers = layers;
    }
}
