using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>用明确借用的一件材料修复实际命中人物的原装备。</summary>
public sealed class ConstructionEquipmentRepair
{
    /// <summary>唯一允许修复的装备栏。</summary>
    public EquipmentType Slot;
    /// <summary>本构造声明的库存输入名。</summary>
    public string MaterialInput;
    /// <summary>一件材料在完整接触下最多恢复的整数耐久。</summary>
    public int Durability = 10;
}

/// <summary>编译完成的装备修复规则，不持有运行中的物品或人物。</summary>
public sealed class CompiledEquipmentRepair
{
    /// <summary>唯一允许修复的装备栏。</summary>
    public EquipmentType Slot { get; }
    /// <summary>本构造声明的库存输入名。</summary>
    public string MaterialInput { get; }
    /// <summary>一件材料最多恢复的耐久。</summary>
    public int Durability { get; }
    /// <summary>引用出错时的声明位置。</summary>
    internal string Path { get; }

    /// <summary>在资料边界复制规则，材料引用由整招统一核对。</summary>
    /// <param name="source">外部声明。</param><param name="path">错误位置。</param>
    internal CompiledEquipmentRepair(ConstructionEquipmentRepair source, string path)
    {
        if (!Enum.IsDefined(typeof(EquipmentType), source.Slot) || string.IsNullOrWhiteSpace(source.MaterialInput) ||
            source.Durability < 1 || source.Durability > 10000)
            throw new InvalidDataException(path + "：装备修复需要有效的单个装备栏、材料输入和1至10000点耐久");
        Path = path;
        Slot = source.Slot;
        MaterialInput = source.MaterialInput;
        Durability = source.Durability;
    }
}
