using System.IO;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>逐段成形共同采用的截面规则；不保存施放状态或读取Unity资源。</summary>
public static class FormFormation
{
    /// <summary>前端从收拢到完整截面的进度跨度，制作端与绘制端保持一致。</summary>
    public const float TipSpan = .04f;

    /// <summary>在外部制作资料边界核对成形区间。</summary>
    /// <param name="source">两个有限进度，空值表示不限制。</param>
    /// <param name="path">具体资料名称。</param>
    internal static Vector2 Range(float[] source, string path)
    {
        if (source == null || source.Length == 0) return new Vector2(-1f, -1f);
        if (source.Length != 2 || !FormRig.Finite(source[0]) || !FormRig.Finite(source[1]) ||
            source[0] < 0f || source[1] > 1f || source[0] > source[1])
            throw new InvalidDataException(path + "：成形区间需要0至1的两个有序进度");
        return new Vector2(source[0], source[1]);
    }
}
