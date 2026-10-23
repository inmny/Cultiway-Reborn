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

    /// <summary>每块一千零二十四个采样，局部变化只复制实际改变的数据块。</summary>
    private const int BlockSize = 1024;

    /// <summary>块和块内数值一经发布便不再修改，不持有旧快照链。</summary>
    private readonly float[][] blocks;

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
        Width = width;
        Height = height;
        Size = size;
        Outside = outside;
        blocks = new float[(values.Length + BlockSize - 1) / BlockSize][];
        float min = outside, max = outside;
        for (int block = 0; block < blocks.Length; block++)
        {
            int start = block * BlockSize, length = Math.Min(BlockSize, values.Length - start);
            blocks[block] = new float[length];
            Array.Copy(values, start, blocks[block], 0, length);
            foreach (float value in blocks[block])
            {
                if (!Finite(value)) throw new InvalidDataException("地表快照包含无效高度");
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
        }
        Minimum = min; Maximum = max;
    }

    /// <summary>接收已经检查的局部副本，共享其余只读块，不复制整张地图。</summary>
    /// <param name="source">原快照的尺寸。</param><param name="values">独占的块目录。</param>
    /// <param name="minimum">保守最低高度。</param><param name="maximum">保守最高高度。</param>
    private AbilityHeightField(AbilityHeightField source, float[][] values, float minimum, float maximum)
    {
        Width = source.Width;
        Height = source.Height;
        Size = source.Size;
        Outside = source.Outside;
        blocks = values;
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>在主线程重新采集指定地图区域；相同值复用原快照，旧快照始终保持原样。</summary>
    /// <param name="area">已刷新数据在地图中的范围，右上边界不包含。</param>
    /// <param name="sample">实际环境高度入口，工作线程不调用。</param>
    public AbilityHeightField WithRegion(Rect area, Func<float, float, float> sample)
    {
        if (sample == null || !Finite(area.xMin) || !Finite(area.xMax) || !Finite(area.yMin) ||
            !Finite(area.yMax) || area.width < 0 || area.height < 0)
            throw new ArgumentException("局部地表采集需要有限区域及实际高度入口");
        int left = Mathf.Clamp(Mathf.CeilToInt(area.xMin * Width / Size.x - .5f), 0, Width);
        int right = Mathf.Clamp(Mathf.CeilToInt(area.xMax * Width / Size.x - .5f), 0, Width);
        int bottom = Mathf.Clamp(Mathf.CeilToInt(area.yMin * Height / Size.y - .5f), 0, Height);
        int top = Mathf.Clamp(Mathf.CeilToInt(area.yMax * Height / Size.y - .5f), 0, Height);
        float[][] changed = null;
        float minimum = Minimum, maximum = Maximum;
        for (int y = bottom; y < top; y++)
        for (int x = left; x < right; x++)
        {
            float value = sample((x + .5f) * Size.x / Width, (y + .5f) * Size.y / Height);
            if (!Finite(value)) throw new InvalidDataException("局部地表包含无效高度");
            int index = y * Width + x, block = index / BlockSize, offset = index % BlockSize;
            if (blocks[block][offset] == value) continue;
            changed ??= (float[][])blocks.Clone();
            if (ReferenceEquals(changed[block], blocks[block])) changed[block] = (float[])blocks[block].Clone();
            changed[block][offset] = value;
            minimum = Mathf.Min(minimum, value);
            maximum = Mathf.Max(maximum, value);
        }

        return changed == null ? this : new AbilityHeightField(this, changed, minimum, maximum);
    }

    /// <summary>只有显示实际上传新快照时才展开完整数组，不暴露可修改的共享数据。</summary>
    internal float[] CopyValues()
    {
        var values = new float[Width * Height];
        for (int i = 0; i < blocks.Length; i++) Array.Copy(blocks[i], 0, values, i * BlockSize, blocks[i].Length);
        return values;
    }

    /// <summary>读取已检查边界内的格点。</summary>
    /// <param name="index">线性格点编号。</param>
    private float Value(int index) => blocks[index / BlockSize][index % BlockSize];

    /// <summary>角色、形体与原版投影共同采用格中心双线性插值。</summary>
    /// <param name="x">地图横向位置。</param>
    /// <param name="y">地图纵向位置。</param>
    public float Sample(float x, float y)
    {
        if (x < 0 || y < 0 || x >= Size.x || y >= Size.y) return Outside;
        float u = x * Width / Size.x - .5f, v = y * Height / Size.y - .5f;
        int left = Mathf.FloorToInt(u), bottom = Mathf.FloorToInt(v);
        float a = Value(Mathf.Clamp(bottom, 0, Height - 1) * Width + Mathf.Clamp(left, 0, Width - 1));
        float b = Value(Mathf.Clamp(bottom, 0, Height - 1) * Width + Mathf.Clamp(left + 1, 0, Width - 1));
        float c = Value(Mathf.Clamp(bottom + 1, 0, Height - 1) * Width + Mathf.Clamp(left, 0, Width - 1));
        float d = Value(Mathf.Clamp(bottom + 1, 0, Height - 1) * Width + Mathf.Clamp(left + 1, 0, Width - 1));
        return Mathf.LerpUnclamped(Mathf.LerpUnclamped(a, b, u - left), Mathf.LerpUnclamped(c, d, u - left),
            v - bottom);
    }

    /// <summary>在对外采集边界拒绝非有限高度。</summary>
    /// <param name="value">采集值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
