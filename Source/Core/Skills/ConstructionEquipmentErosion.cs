using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>真实人物接触后，对明确装备栏中的原物品施加腐蚀。</summary>
public sealed class ConstructionEquipmentErosion
{
    /// <summary>本次腐蚀接受的明确装备栏，最多六项且互不重复。</summary>
    public EquipmentType[] Slots;
    /// <summary>每次接触投入的最大耐久份额，实际扣除还经过当前材料耐受。</summary>
    public float Fraction = .75f;
}

/// <summary>编译时复制的装备腐蚀声明，不持有任何原物品。</summary>
public sealed class CompiledEquipmentErosion
{
    /// <summary>已经核对并独立复制的装备栏。</summary>
    public IReadOnlyList<EquipmentType> Slots { get; }
    /// <summary>每次实际接触投入的份额。</summary>
    public float Fraction { get; }

    /// <summary>只在构造资料边界检查完整声明。</summary>
    /// <param name="source">外部声明。</param><param name="path">错误位置。</param>
    internal CompiledEquipmentErosion(ConstructionEquipmentErosion source, string path)
    {
        if (source.Slots == null || source.Slots.Length < 1 || source.Slots.Length > 6 ||
            source.Slots.Any(slot => !Enum.IsDefined(typeof(EquipmentType), slot)) ||
            source.Slots.Distinct().Count() != source.Slots.Length ||
            float.IsNaN(source.Fraction) || float.IsInfinity(source.Fraction) || source.Fraction <= 0 || source.Fraction > 1)
            throw new InvalidDataException(path + "：装备腐蚀需要一至六个互异有效装备栏，以及大于零且不超过一的份额");
        Slots = Array.AsReadOnly((EquipmentType[])source.Slots.Clone());
        Fraction = source.Fraction;
    }
}
