using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>从真实接触形状进入连通水域的一次有界导电。</summary>
public sealed class ConstructionConduction
{
    /// <summary>沿真实水路最多传播的地图格数。</summary>
    public float Range = 24;

    /// <summary>允许导电的最低真实水深。</summary>
    public float MinimumDepth = .02f;

    /// <summary>每传播一地图格实际消耗的力量。</summary>
    public float TravelCost = .1f;

    /// <summary>每个不同人物消耗的完整力量，再由原有倍率和抗性结算。</summary>
    public float DamagePerTarget = 5;

    /// <summary>最多到达的实际水格数。</summary>
    public int MaximumCells = 512;

    /// <summary>最多作用的不同人物数。</summary>
    public int MaximumTargets = 16;
}

/// <summary>仅在构造资料边界检查的传播参数，计算期间不读取可编辑对象。</summary>
internal sealed class CompiledConduction
{
    /// <summary>已检查的独立参数副本。</summary>
    internal readonly float Range, MinimumDepth, TravelCost, DamagePerTarget;

    /// <summary>同部位操作的原资料先后顺序。</summary>
    internal readonly int Order;

    /// <summary>明确的本次格数及人物上限。</summary>
    internal readonly int MaximumCells, MaximumTargets;

    /// <summary>复制及检查有效传播、消耗和上限。</summary>
    /// <param name="source">可选普通参数。</param><param name="path">资料错误位置。</param><param name="order">原资料顺序。</param>
    internal CompiledConduction(ConstructionConduction source, string path, int order = 0)
    {
        source ??= new ConstructionConduction();
        bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        if (!Positive(source.Range) || source.Range > 4096 || !Positive(source.MinimumDepth) ||
            source.MinimumDepth > 100 ||
            !Positive(source.TravelCost) || source.TravelCost > 1000 || !Positive(source.DamagePerTarget) ||
            source.DamagePerTarget < 1 || source.DamagePerTarget > 1000000 ||
            source.MaximumCells < 1 || source.MaximumCells > 262144 || source.MaximumTargets < 1 ||
            source.MaximumTargets > 65536)
            throw new InvalidDataException(path + "：导电需要正数距离、水深和沿途消耗，人物消耗须至少为1，格数须在1至262144，人物数须在1至65536内");
        Order = order;
        Range = source.Range;
        MinimumDepth = source.MinimumDepth;
        TravelCost = source.TravelCost;
        DamagePerTarget = source.DamagePerTarget;
        MaximumCells = source.MaximumCells;
        MaximumTargets = source.MaximumTargets;
    }
}