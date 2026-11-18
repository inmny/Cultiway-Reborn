using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>从实际过程开始计的一段世界时间。窗口外按常速推进。</summary>
public sealed class ConstructionProcessWindow
{
    /// <summary>开始前已经经过的世界秒数。</summary>
    public float At;

    /// <summary>窗口持续的世界秒数。</summary>
    public float Seconds;

    /// <summary>每个世界秒推进多少过程秒；零为暂停。</summary>
    public float Rate = 1f;
}

/// <summary>把世界时间换成所选过程的实际进度，不维护另外一套游戏时钟。</summary>
internal sealed class CompiledProcessTime
{
    private readonly (double Start, double End, double Rate)[] windows;

    /// <summary>当前过程按完整安排走完所需的世界秒数。</summary>
    internal readonly float Completion;

    internal CompiledProcessTime(ConstructionProcessWindow[] source, float work, float limit, string path)
    {
        if (source.Length > 8) throw new InvalidDataException(path + "：一个过程最多有八个变速时段");
        windows = new (double, double, double)[source.Length];
        double last = 0d;
        for (int i = 0; i < source.Length; i++)
        {
            var window = source[i];
            float end = window == null ? float.NaN : window.At + window.Seconds;
            if (window == null || !(window.At >= last) || !(end > window.At && end <= limit) ||
                !(window.Rate >= 0f && window.Rate <= 8f))
                throw new InvalidDataException(path + "：变速时段须按开始排序、不重叠并位于实际截止内，倍率须为零至八");
            windows[i] = (window.At, end, window.Rate);
            last = end;
        }

        double remaining = work, cursor = 0d;
        foreach (var (start, end, rate) in windows)
        {
            double idle = start - cursor;
            if (remaining <= idle) break;
            remaining -= idle;
            cursor = start;
            double effective = (end - start) * rate;
            if (remaining <= effective)
            {
                cursor += remaining / rate;
                remaining = 0d;
                break;
            }
            remaining -= effective;
            cursor = end;
        }
        Completion = (float)(cursor + remaining);
    }

    /// <summary>累计已推进的过程秒数；暂停跳过该时段、不在恢复后补发。</summary>
    internal double WorkAt(double elapsed)
    {
        double work = elapsed;
        foreach (var (start, end, rate) in windows)
        {
            if (elapsed <= start) break;
            work += (Math.Min(end, elapsed) - start) * (rate - 1d);
        }
        return work;
    }

    /// <summary>在倍率切换和真实完成时拆步，避免把两个形体状态当作同一次接触。</summary>
    internal float NextBoundary(float origin, float age)
    {
        float endAt = origin + Completion;
        foreach (var (start, end, _) in windows)
        {
            float begin = origin + (float)start;
            if (begin > age) return Math.Min(begin, endAt);
            float finish = origin + (float)end;
            if (finish > age) return Math.Min(finish, endAt);
        }
        return endAt;
    }
}
