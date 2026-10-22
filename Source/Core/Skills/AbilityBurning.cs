using System;

namespace Cultiway.Core.Skills;

/// <summary>明确可燃材料的有限燃料和热量，不读取颜色或游戏对象。</summary>
public struct AbilityBurning
{
    /// <summary>是否已经点燃，冷却至零后停止。</summary>
    public bool Lit;

    /// <summary>当前实际保存的热量。</summary>
    public double Heat;

    /// <summary>本轮起燃时的剩余燃料，用于确定有限消耗速率。</summary>
    public double Fuel;

    /// <summary>已经真正烧损的累计体积，不再参与回收。</summary>
    public double Burned;

    /// <summary>最后一次真实加热的原来源，点火及保存保留稳定编号。</summary>
    public long Source;

    /// <summary>最后加热接触点在主体局部空间的位置，不序列化Unity对象。</summary>
    public float X, Y, Z;

    /// <summary>点燃一份木料需要的作用量。</summary>
    internal const double IgnitionPerVolume = .125;

    /// <summary>完整起燃燃料在四世界秒内消耗。</summary>
    internal const double Seconds = 4;

    /// <summary>当前实际材料是否已经明确登记可燃。</summary>
    /// <param name="matter">真实材料及物态。</param>
    internal static bool CanBurn(AbilityMatter matter) => matter.Combustible && matter.State == "solid";

    /// <summary>当前尚缺的真实点火量，已燃烧时不重复补满。</summary>
    /// <param name="volume">当前真实材料量。</param>
    internal double Needed(double volume) => Lit ? 0 : Math.Max(0, volume * IgnitionPerVolume - Heat);

    /// <summary>只允许传出超过自身维持量的部分，不能反复复制热量。</summary>
    /// <param name="volume">当前尚有的真实燃料。</param>
    internal double Transferable(double volume) => Lit ? Math.Max(0, Heat - volume * IgnitionPerVolume) : 0;

    /// <summary>接收有限作用量，真正达到点火量后才开始消耗木料。</summary>
    /// <param name="volume">当前燃料。</param><param name="amount">已从来源扣除的作用量。</param>
    /// <param name="source">实际加热来源。</param>
    internal void AddHeat(double volume, double amount, long source)
    {
        Heat += amount;
        Source = source;
        if (!Lit && volume > 0 && Heat >= volume * IgnitionPerVolume * (1 - 1e-6))
        {
            Lit = true;
            Fuel = volume;
        }
    }

    /// <summary>推进有限燃料损耗，产生的热量与真正烧损量相同。</summary>
    /// <param name="seconds">真实世界时间。</param><param name="volume">当前尚存木料。</param>
    internal double Advance(float seconds, double volume)
    {
        if (!Lit || seconds <= 0 || volume <= 0) return 0;
        double burned = Math.Min(volume, Fuel * seconds / Seconds);
        Burned += burned;
        Heat += burned;
        return burned;
    }

    /// <summary>扣除已经传出或被水吸收的真实热量。</summary>
    /// <param name="amount">实际移除量。</param>
    internal void Cool(double amount)
    {
        Heat = Math.Max(0, Heat - amount);
        if (Heat < Math.Max(1e-12, amount * 1e-6))
        {
            Heat = 0;
            Lit = false;
            Fuel = 0;
        }
    }

    /// <summary>材料已变为不可燃时解除旧火，历史损耗仍保留。</summary>
    internal void Extinguish()
    {
        Lit = false;
        Heat = Fuel = 0;
    }

    /// <summary>真实拆分或收束时按材料份额转交，不能复制整份热量。</summary>
    /// <param name="ratio">当前主体保留比例。</param>
    internal void Retain(double ratio)
    {
        Heat *= ratio;
        Fuel *= ratio;
        Burned *= ratio;
    }
}