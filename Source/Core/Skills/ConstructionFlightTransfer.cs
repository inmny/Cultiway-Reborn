using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>同一飞行主体按间隔转移，空隙不形成接触路径。</summary>
public sealed class ConstructionFlightTransfer
{
    /// <summary>相邻转移的世界秒数，首步在第一个间隔。</summary>
    public float Interval = .025f;
    /// <summary>每步至少前进的距离；原运动速度乘间隔更大时采用后者。</summary>
    public float Distance = 2.5f;
    /// <summary>距离最后一步之外仍允许直接到达的余量。</summary>
    public float ArrivalSlack = 1.2f;
    /// <summary>相对本次方向的最大水平横偏。</summary>
    public float SideJitter = .45f;
    /// <summary>明确保存的偏移种子，同一步号始终取得相同结果。</summary>
    public int Seed;
}

/// <summary>经过资料边界检查的间隔运动参数。</summary>
internal sealed class CompiledFlightTransfer
{
    /// <summary>间隔、最低步距、到达余量及最大横偏。</summary>
    internal readonly float Interval, Distance, ArrivalSlack, SideJitter;
    /// <summary>可重复的偏移输入。</summary>
    internal readonly int Seed;

    /// <summary>一次复制并核对数值，不把可编辑对象交给工作线程。</summary>
    /// <param name="source">保存的参数。</param><param name="path">资料位置。</param>
    internal CompiledFlightTransfer(ConstructionFlightTransfer source, string path)
    {
        Interval = source.Interval; Distance = source.Distance; ArrivalSlack = source.ArrivalSlack; SideJitter = source.SideJitter; Seed = source.Seed;
        if (float.IsNaN(Interval + Distance + ArrivalSlack + SideJitter) || float.IsInfinity(Interval + Distance + ArrivalSlack + SideJitter) ||
            Interval < .01f || Interval > 3600f || Distance <= 0f || Distance > 10000f || ArrivalSlack < 0f || ArrivalSlack > 10000f || SideJitter < 0f || SideJitter > 10000f)
            throw new InvalidDataException(path + "：转移间隔须为0.01至3600秒，步距为正数，步距和偏移不超过一万格");
    }

    /// <summary>只由已保存种子及步号产生偏移，不读取游戏状态或共享随机数。</summary>
    /// <param name="index">本次飞行的转移步号。</param>
    internal float Offset(int index)
    {
        unchecked
        {
            uint value = (uint)Seed ^ ((uint)index * 747796405u + 2891336453u);
            value = (value ^ (value >> 16)) * 2246822519u;
            value = (value ^ (value >> 13)) * 3266489917u;
            value ^= value >> 16;
            return ((value >> 8) / 16777215f * 2f - 1f) * SideJitter;
        }
    }
}
