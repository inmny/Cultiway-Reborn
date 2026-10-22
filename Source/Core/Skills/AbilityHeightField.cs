using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>已经采集的不可变地表格点；计算线程只读取数字，不访问游戏或显示对象。</summary>
public sealed class AbilityHeightField
{
    /// <summary>横纵采样数量。</summary>
    public readonly int Width, Height;
    /// <summary>格点覆盖的地图尺寸；格点位于各格中心。</summary>
    public readonly Vector2 Size;
    /// <summary>世界边界以外的地表高度。</summary>
    public readonly float Outside;
    /// <summary>用于保守显示裁剪的最低和最高高度。</summary>
    public readonly float Minimum, Maximum;
    /// <summary>内部显示适配也读取相同数据，不修改它。</summary>
    internal readonly float[] Values;

    /// <summary>在采集边界复制并核对完整数值，之后不改变数组。</summary>
    /// <param name="width">横向格点数。</param>
    /// <param name="height">纵向格点数。</param>
    /// <param name="size">实际覆盖的地图尺寸。</param>
    /// <param name="values">按行排列的格点。</param>
    /// <param name="outside">地图外高度。</param>
    public AbilityHeightField(int width, int height, Vector2 size, float[] values, float outside = 0f)
    {
        if (width < 1 || height < 1 || (long)width * height > 4194304 || values?.Length != (long)width * height ||
            !Finite(size.x) || !Finite(size.y) || size.x <= 0 || size.y <= 0 || !Finite(outside))
            throw new InvalidDataException("地表快照需要有效尺寸及最多四百万个完整格点");
        Width = width; Height = height; Size = size; Outside = outside; Values = (float[])values.Clone();
        float min = outside, max = outside;
        foreach (float value in Values)
        {
            if (!Finite(value)) throw new InvalidDataException("地表快照包含无效高度");
            min = Mathf.Min(min, value); max = Mathf.Max(max, value);
        }
        Minimum = min; Maximum = max;
    }

    /// <summary>角色、形体与原版投影共同采用格中心双线性插值。</summary>
    /// <param name="x">地图横向位置。</param>
    /// <param name="y">地图纵向位置。</param>
    public float Sample(float x, float y)
    {
        if (x < 0 || y < 0 || x >= Size.x || y >= Size.y) return Outside;
        float u = x * Width / Size.x - .5f, v = y * Height / Size.y - .5f;
        int left = Mathf.FloorToInt(u), bottom = Mathf.FloorToInt(v);
        float a = Values[Mathf.Clamp(bottom, 0, Height - 1) * Width + Mathf.Clamp(left, 0, Width - 1)];
        float b = Values[Mathf.Clamp(bottom, 0, Height - 1) * Width + Mathf.Clamp(left + 1, 0, Width - 1)];
        float c = Values[Mathf.Clamp(bottom + 1, 0, Height - 1) * Width + Mathf.Clamp(left, 0, Width - 1)];
        float d = Values[Mathf.Clamp(bottom + 1, 0, Height - 1) * Width + Mathf.Clamp(left + 1, 0, Width - 1)];
        return Mathf.LerpUnclamped(Mathf.LerpUnclamped(a, b, u - left), Mathf.LerpUnclamped(c, d, u - left), v - bottom);
    }

    /// <summary>在对外采集边界拒绝非有限高度。</summary>
    /// <param name="value">采集值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
