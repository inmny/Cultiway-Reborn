using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>施放前借用的真实库存材料，使用时再绑定实际物件。</summary>
public sealed class ConstructionInventoryInput
{
    /// <summary>本构造内供操作引用的名称。</summary>
    public string Id;
    /// <summary>原物品形状的稳定编号。</summary>
    public string Shape;
    /// <summary>实际独立物件的数量。</summary>
    public int Count = 1;
}

/// <summary>本次操作真正消耗的库存材料份数。</summary>
public sealed class ConstructionInventoryUse
{
    /// <summary>已经声明的材料输入。</summary>
    public string Input;
    /// <summary>本次使用的整件数量。</summary>
    public int Count = 1;
}

/// <summary>编译时固定的材料声明，工作线程只读取名称和数量。</summary>
internal sealed class CompiledInventoryInput
{
    /// <summary>构造内名称及实际物品形状。</summary>
    internal readonly string Id, Shape;
    /// <summary>本次施放所需的整件数量。</summary>
    internal readonly int Count;
    /// <summary>在外部资料边界检查，不把可编辑对象放入计划。</summary>
    /// <param name="source">完整声明。</param>
    internal CompiledInventoryInput(ConstructionInventoryInput source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.Id) || source.Id.Length > 128 ||
            string.IsNullOrWhiteSpace(source.Shape) || source.Shape.Length > 256 || source.Count < 1 || source.Count > 64)
            throw new InvalidDataException("Inventory：材料需要名称、真实形状及1至64件的数量");
        Id = source.Id; Shape = source.Shape; Count = source.Count;
    }
}

/// <summary>已经检查的单次消耗，不持有真实库存物件。</summary>
internal sealed class CompiledInventoryUse
{
    /// <summary>材料名称及本次使用件数。</summary>
    internal readonly string Input;
    /// <summary>独立物件的数量。</summary>
    internal readonly int Count;
    /// <summary>外部参数只检查一次，引用在完整编译后解析。</summary>
    /// <param name="source">实际参数。</param><param name="path">输入位置。</param>
    internal CompiledInventoryUse(ConstructionInventoryUse source, string path)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.Input) || source.Count < 1 || source.Count > 64)
            throw new InvalidDataException(path + "：消耗需要实际材料输入及1至64件的数量");
        Input = source.Input; Count = source.Count;
    }
}

public static partial class AbilityCompiler
{
    /// <summary>完整展开后核对名称和预算，同一输入可以在不同真实步骤中分次使用。</summary>
    /// <param name="definition">外部资料。</param><param name="operations">已经编译的实际操作。</param>
    /// <param name="impacts">已复制的人物作用声明。</param>
    internal static CompiledInventoryInput[] CompileInventory(AbilityDefinition definition, CompiledEventOperation[] operations,
        IEnumerable<CompiledConstructionImpact> impacts)
    {
        var source = definition.Inventory ?? Array.Empty<ConstructionInventoryInput>();
        if (source.Length > 16) throw new InvalidDataException("Inventory：单次构造最多16类材料");
        var inputs = source.Select(value => new CompiledInventoryInput(value)).ToArray();
        if (inputs.Sum(value => value.Count) > 64 || inputs.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != inputs.Length)
            throw new InvalidDataException("Inventory：材料名称重复或总量超过64件");
        foreach (var operation in operations.Where(operation => operation.Kind == ConstructionOperationKind.ConsumeInventory))
        {
            var use = (CompiledInventoryUse)operation.Parameters;
            var input = inputs.FirstOrDefault(value => value.Id == use.Input);
            if (input == null || use.Count > input.Count)
                throw new InvalidDataException(operation.Path + "：消耗引用不存在的材料输入或超过本次借用数量");
        }
        foreach (var impact in impacts)
            if (impact?.EquipmentRepair is { } repair && !inputs.Any(input => input.Id == repair.MaterialInput))
                throw new InvalidDataException(repair.Path + "：修复引用了不存在的材料输入 " + repair.MaterialInput);
        return inputs;
    }
}
