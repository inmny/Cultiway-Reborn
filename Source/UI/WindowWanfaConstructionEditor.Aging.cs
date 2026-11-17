using System;
using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>原接触表单内编辑原人物及原物件的实际损耗。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>当前展开的装备栏及库存形状，刷新后重新约束到实际列表。</summary>
    private int agingEquipmentSlot, agingInventoryShape;

    /// <summary>整招、身体、部位与身体阶段共用同一组正式声明。</summary>
    /// <param name="impact">独立接触资料。</param><param name="prefix">所属表单前缀。</param>
    private void ContactAgingRows(ConstructionImpact impact, string prefix)
    {
        Row("AgingRules", T("AgingRulesHint"));
        ContactInteger(prefix + "AgeYears", impact.AgeYears, value => impact.AgeYears = value);
        Toggle(prefix + "EquipmentErosion", impact.EquipmentErosion != null, value => impact.EquipmentErosion = value
            ? new ConstructionEquipmentErosion { Slots = new[] { EquipmentType.Armor } }
            : null);
        if (impact.EquipmentErosion != null) EquipmentErosionRows(impact.EquipmentErosion, prefix);
        Toggle(prefix + "InventoryAging", impact.InventoryAging != null, value => impact.InventoryAging = value
            ? new ConstructionInventoryAging()
            : null);
        if (impact.InventoryAging == null) return;
        ConstructionInventoryAging aging = impact.InventoryAging;
        Number(prefix + "InventoryAgingSeconds", aging.Seconds, value => aging.Seconds = value);
        ContactInteger(prefix + "InventoryAgingMaximum", aging.MaximumItems, value => aging.MaximumItems = value);
        Toggle(prefix + "InventoryAgingFilter", aging.Shapes?.Length > 0, value => aging.Shapes = value
            ? new[] { ModClass.L.ItemShapeLibrary.list.First().id }
            : null);
        if (aging.Shapes?.Length > 0) InventoryAgingShapes(aging, prefix);
    }

    /// <summary>明确选择装备栏，列表保持互异，删除最后一栏通过关闭该作用完成。</summary>
    /// <param name="erosion">当前声明。</param><param name="prefix">所属表单。</param>
    private void EquipmentErosionRows(ConstructionEquipmentErosion erosion, string prefix)
    {
        Number(prefix + "EquipmentErosionFraction", erosion.Fraction, value => erosion.Fraction = value);
        agingEquipmentSlot = Math.Min(agingEquipmentSlot, erosion.Slots.Length - 1);
        Choice(prefix + "EquipmentErosionSlot", T("EquipmentSlot." + erosion.Slots[agingEquipmentSlot]), () =>
            menu.Show(T(prefix + "EquipmentErosionSlot"), erosion.Slots.Select((slot, index) => new UiOptionMenuOption
            {
                Label = T("EquipmentSlot." + slot), Selected = index == agingEquipmentSlot,
                Select = () => { agingEquipmentSlot = index; Refresh(); }
            }).ToArray()));
        if (erosion.Slots.Length < 6)
            ActionRow(prefix + "EquipmentErosionAddSlot", "", () => menu.Show(T(prefix + "EquipmentErosionAddSlot"),
                Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>().Except(erosion.Slots).Select(slot => new UiOptionMenuOption
                {
                    Label = T("EquipmentSlot." + slot), Select = () => Change(() =>
                    {
                        agingEquipmentSlot = erosion.Slots.Length;
                        erosion.Slots = erosion.Slots.Concat(new[] { slot }).ToArray();
                    })
                }).ToArray()));
        if (erosion.Slots.Length > 1)
            ActionRow(prefix + "EquipmentErosionRemoveSlot", "", () => Change(() =>
                erosion.Slots = erosion.Slots.Where((_, index) => index != agingEquipmentSlot).ToArray()));
    }

    /// <summary>只展开一项形状，以原有可搜索菜单修改或增加真实筛选。</summary>
    /// <param name="aging">当前声明。</param><param name="prefix">所属表单。</param>
    private void InventoryAgingShapes(ConstructionInventoryAging aging, string prefix)
    {
        agingInventoryShape = Math.Min(agingInventoryShape, aging.Shapes.Length - 1);
        Choice(prefix + "InventoryAgingShape", InventoryShapeName(aging.Shapes[agingInventoryShape]), () =>
            menu.Show(T(prefix + "InventoryAgingShape"), aging.Shapes.Select((shape, index) => new UiOptionMenuOption
            {
                Label = InventoryShapeName(shape), Selected = index == agingInventoryShape,
                Select = () => { agingInventoryShape = index; Refresh(); }
            }).ToArray(), true));
        ActionRow(prefix + "InventoryAgingChangeShape", "", () => ChooseAgingShape(aging, prefix, false));
        if (aging.Shapes.Length < 32) ActionRow(prefix + "InventoryAgingAddShape", "", () => ChooseAgingShape(aging, prefix, true));
        if (aging.Shapes.Length > 1) ActionRow(prefix + "InventoryAgingRemoveShape", "", () => Change(() =>
            aging.Shapes = aging.Shapes.Where((_, index) => index != agingInventoryShape).ToArray()));
    }

    /// <summary>以正式形状目录提供互异筛选，菜单直接写入当前独立草稿。</summary>
    /// <param name="aging">当前声明。</param><param name="prefix">所属表单。</param><param name="add">增加或修改当前项。</param>
    private void ChooseAgingShape(ConstructionInventoryAging aging, string prefix, bool add) =>
        menu.Show(T(prefix + (add ? "InventoryAgingAddShape" : "InventoryAgingChangeShape")),
            ModClass.L.ItemShapeLibrary.list.Where(shape => !aging.Shapes.Contains(shape.id) ||
                !add && aging.Shapes[agingInventoryShape] == shape.id).OrderBy(shape => shape.id, StringComparer.Ordinal)
                .Select(shape => new UiOptionMenuOption
                {
                    Label = InventoryShapeName(shape.id), SearchText = shape.id,
                    Selected = !add && aging.Shapes[agingInventoryShape] == shape.id,
                    Select = () => Change(() =>
                    {
                        if (add)
                        {
                            agingInventoryShape = aging.Shapes.Length;
                            aging.Shapes = aging.Shapes.Concat(new[] { shape.id }).ToArray();
                        }
                        else aging.Shapes[agingInventoryShape] = shape.id;
                    })
                }).ToArray(), true);
}
