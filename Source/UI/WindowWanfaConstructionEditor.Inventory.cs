using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;

namespace Cultiway.UI;

/// <summary>在原参数和步骤表单内声明真实库存材料，不保存本次物件身份。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前查看的材料声明，撤销后重新绑定。</summary>
    private int inventoryChoice;

    /// <summary>先声明名称、形状及件数，原步骤仍引用同一名称。</summary>
    private void InventoryRows()
    {
        Row("InventoryRules", T("InventoryRulesHint"));
        ActionRow("AddInventoryInput", "", () => menu.Show(T("InventoryShape"),
            ModClass.L.ItemShapeLibrary.list.OrderBy(shape => shape.id, StringComparer.Ordinal).Select(shape => new UiOptionMenuOption
            {
                Label = InventoryShapeName(shape.id), Select = () => EditInventory(candidate =>
                {
                    var inputs = candidate.Inventory ?? Array.Empty<ConstructionInventoryInput>();
                    string id = Enumerable.Range(0, inputs.Length + 1).Select(index => "material" + index).First(name => inputs.All(input => input.Id != name));
                    candidate.Inventory = inputs.Concat(new[] { new ConstructionInventoryInput { Id = id, Shape = shape.id } }).ToArray();
                    inventoryChoice = inputs.Length;
                })
            }).ToArray(), true));
        var declarations = session.Draft.Inventory ?? Array.Empty<ConstructionInventoryInput>();
        if (declarations.Length == 0) return;
        inventoryChoice = Math.Min(inventoryChoice, declarations.Length - 1);
        var selected = declarations[inventoryChoice];
        Choice("InventoryInput", selected.Id + " · " + InventoryShapeName(selected.Shape), () => menu.Show(T("InventoryInput"),
            declarations.Select((input, index) => new UiOptionMenuOption
            {
                Label = input.Id + " · " + InventoryShapeName(input.Shape), Selected = input == selected,
                Select = () => { inventoryChoice = index; Refresh(); }
            }).ToArray()));
        NameParameter("InventoryName", selected.Id, value => EditInventory(candidate =>
        {
            candidate.Inventory[inventoryChoice].Id = value;
            foreach (var operation in ConstructionContactEdits.Parts(candidate.Root).SelectMany(part => part.Node.Operations)
                         .Concat(candidate.Steps.Select(step => step.Operation)))
                if (operation.InventoryUse?.Input == selected.Id) operation.InventoryUse.Input = value;
            foreach (var impact in ConstructionContactEdits.Parts(candidate.Root).Select(part => part.Node.Impact)
                         .Concat(candidate.Body?.Stages?.Select(stage => stage.Impact) ?? Enumerable.Empty<ConstructionImpact>())
                         .Append(candidate.Impact).Append(candidate.Body?.Impact))
                if (impact?.EquipmentRepair?.MaterialInput == selected.Id) impact.EquipmentRepair.MaterialInput = value;
        }));
        Choice("InventoryShape", InventoryShapeName(selected.Shape), () => menu.Show(T("InventoryShape"),
            ModClass.L.ItemShapeLibrary.list.OrderBy(shape => shape.id, StringComparer.Ordinal).Select(shape => new UiOptionMenuOption
            {
                Label = InventoryShapeName(shape.id), Selected = shape.id == selected.Shape,
                Select = () => EditInventory(candidate => candidate.Inventory[inventoryChoice].Shape = shape.id)
            }).ToArray(), true));
        ContactInteger("InventoryCount", selected.Count, value => EditInventory(candidate => candidate.Inventory[inventoryChoice].Count = value), draftChange: false);
        ActionRow("RemoveInventoryInput", T("InventoryRemoveHint"), () => EditInventory(candidate =>
        {
            var inputs = candidate.Inventory.Where(input => input.Id != selected.Id).ToArray();
            candidate.Inventory = inputs.Length == 0 ? null : inputs;
        }));
    }

    /// <summary>完整候选通过共同编译才改变草稿，保留冲突的原步骤。</summary>
    /// <param name="edit">当前修改。</param>
    private void EditInventory(Action<AbilityDefinition> edit) => ApplyConstructionEdit(() =>
    {
        var candidate = ConstructionEditing.Copy(session.Draft); edit(candidate);
        AbilityCompiler.Compile(candidate, false); return candidate;
    });

    /// <summary>只提供当前真实声明，件数仍由共同编译检查。</summary>
    /// <param name="operation">当前消耗步骤。</param>
    private void InventoryUseParameters(ConstructionOperation operation)
    {
        Choice("InventoryUseInput", operation.InventoryUse.Input, () => menu.Show(T("InventoryUseInput"),
            (session.Draft.Inventory ?? Array.Empty<ConstructionInventoryInput>()).Select(input => new UiOptionMenuOption
            {
                Label = input.Id + " · " + InventoryShapeName(input.Shape), Selected = input.Id == operation.InventoryUse.Input,
                Select = () => Change(() => operation.InventoryUse.Input = input.Id)
            }).ToArray()));
        ContactInteger("InventoryUseCount", operation.InventoryUse.Count, value => operation.InventoryUse.Count = value);
        Row("InventoryUseRules", T("InventoryUseRulesHint"));
    }

    /// <summary>使用物品原名称，缺少该语言时提供短名称。</summary>
    /// <param name="shape">物品稳定形状编号。</param>
    private static string InventoryShapeName(string shape) => LMTools.GetOrFallback(shape, shape.Substring(shape.LastIndexOf('.') + 1));
}
