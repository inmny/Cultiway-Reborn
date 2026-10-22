using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>活动及留存木体共用的燃烧显示，只读真实热量与损耗。</summary>
internal static class FormBurningPresentation
{
    /// <summary>只有真正烧损的木体才变暗，显示染色不建立燃料。</summary>
    /// <param name="burning">当前燃烧量。</param><param name="volume">实际剩余材料。</param>
    internal static Color? Char(AbilityBurning burning, double volume) => burning.Burned <= 0
        ? null
        : Color.Lerp(new Color(.36f, .21f, .10f), new Color(.055f, .035f, .025f),
            (float)Math.Min(1, burning.Burned / Math.Max(1e-10, burning.Burned + volume)));

    /// <summary>真实点火处随主体移动，短暂冷却不创建独立攻击实体。</summary>
    /// <param name="output">共同显示集合。</param><param name="matrix">实际主体变换。</param>
    /// <param name="burning">真实热量与点火位置。</param><param name="volume">尚存燃料。</param>
    /// <param name="terrain">本轮共同地表。</param><param name="time">已完成的世界时钟。</param>
    internal static void Append(List<FormVisualInstance> output, Matrix4x4 matrix, AbilityBurning burning,
        double volume, AbilityHeightField terrain, double time)
    {
        if (!burning.Lit || burning.Heat <= 0 || volume <= 0) return;
        Vector3 point = matrix.MultiplyPoint3x4(new Vector3(burning.X, burning.Y, burning.Z));
        float size = Mathf.Clamp((float)Math.Pow(volume, 1d / 3) * .55f, .15f, 6f);
        float pulse = .94f + .06f * Mathf.Sin((float)(time % 1000) * 9);
        output.Add(new FormVisualInstance(FormResources.Load("fire_body"),
            Matrix4x4.TRS(point, Quaternion.identity, new Vector3(size, size, size * pulse)),
            tint: new Color(1f, .31f, .035f, .83f), terrain: terrain));
    }
}