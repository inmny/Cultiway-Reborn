using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.UI.Prefab;

namespace Cultiway.UI;

/// <summary>用普通接触表单编辑真实解除、装备修复、讯号与状态条件。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>恢复和有限结果沿用一个指定解除规则。</summary>
    /// <param name="restoration">当前恢复资料。</param><param name="prefix">表单所属。</param>
    private void ContactRemovalRows(ConstructionRestoration restoration, string prefix)
    {
        StatusChoice(prefix + "RemoveStatus", restoration.RemoveStatus?.Id, id =>
        {
            if (id == null) restoration.RemoveStatus = null;
            else if (restoration.RemoveStatus == null) restoration.RemoveStatus = new ConstructionStatusRemoval { Id = id };
            else restoration.RemoveStatus.Id = id;
        });
        if (restoration.RemoveStatus is not { } removal) return;
        ContactInteger(prefix + "RemoveStatusMaximum", removal.Maximum, value => removal.Maximum = value);
        Toggle(prefix + "RemoveOwnStatusOnly", removal.OwnSourceOnly, value => removal.OwnSourceOnly = value);
    }

    /// <summary>从实际登记状态中选择，允许合适的可空字段使用同一清空入口。</summary>
    /// <param name="key">菜单标签。</param><param name="id">当前状态。</param><param name="set">草稿写入。</param>
    /// <param name="canClear">清除后是否仍有保留其他结果所需的条件。</param>
    private void StatusChoice(string key, string id, Action<string> set, bool canClear = true)
    {
        Choice(key, id == null ? T("NoContactStatus") : ModClass.L.StatusEffectLibrary.get(id)?.GetName() ?? id,
            () => menu.Show(T(key), (canClear ? new[] { new UiOptionMenuOption
            {
                Label = T("NoContactStatus"), Selected = id == null, Select = () => Change(() => set(null))
            } } : Array.Empty<UiOptionMenuOption>()).Concat(ModClass.L.StatusEffectLibrary.list
                .OrderBy(asset => asset.id, StringComparer.Ordinal).Select(asset => new UiOptionMenuOption
                {
                    Label = asset.GetName(), SearchText = asset.id + " " + asset.GetDescription(), Selected = id == asset.id,
                    Select = () => Change(() => set(asset.id))
                })).ToArray(), searchable: true));
    }

    /// <summary>不同作用可共存；修复只引用已声明的材料。</summary>
    /// <param name="impact">当前整招、身体或部位的独立作用。</param><param name="prefix">表单所属。</param>
    private void ContactApplicationRows(ConstructionImpact impact, string prefix)
    {
        ImpactOption(prefix, "ReleaseGrab", impact.ReleaseGrab, value => impact.ReleaseGrab = value);
        Row("ReleaseGrabRules", T("ReleaseGrabHint"));
        var inputs = session.Draft.Inventory ?? Array.Empty<ConstructionInventoryInput>();
        if (inputs.Length == 0 && impact.EquipmentRepair == null)
            Row("RepairNeedsInput", T("RepairNeedsInputHint"));
        else
        {
            Toggle(prefix + "EquipmentRepair", impact.EquipmentRepair != null, enabled => impact.EquipmentRepair = enabled
                ? new ConstructionEquipmentRepair { Slot = EquipmentType.Armor, MaterialInput = inputs[0].Id }
                : null);
            if (impact.EquipmentRepair is { } repair)
            {
                Choice(prefix + "RepairSlot", T("EquipmentSlot." + repair.Slot), () => menu.Show(T(prefix + "RepairSlot"),
                    Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>().Select(slot => new UiOptionMenuOption
                    {
                        Label = T("EquipmentSlot." + slot), Selected = slot == repair.Slot,
                        Select = () => Change(() => repair.Slot = slot)
                    }).ToArray()));
                Choice(prefix + "RepairMaterial", repair.MaterialInput, () => menu.Show(T(prefix + "RepairMaterial"),
                    inputs.Select(input => new UiOptionMenuOption
                    {
                        Label = input.Id + " · " + InventoryShapeName(input.Shape), Selected = input.Id == repair.MaterialInput,
                        Select = () => Change(() => repair.MaterialInput = input.Id)
                    }).ToArray()));
                ContactInteger(prefix + "RepairDurability", repair.Durability, value => repair.Durability = value);
                Row("RepairRules", T("RepairHint"));
            }
        }
        Toggle(prefix + "ContactSignal", impact.Signal != null, enabled => impact.Signal = enabled ? new ConstructionSignal() : null);
        if (impact.Signal is not { } signal) return;
        ImpactOption(prefix, "SignalKind", signal.Kind, value => signal.Kind = value);
        Number(prefix + "SignalDuration", signal.Duration, value => signal.Duration = value);
        Row("SignalRules", T("SignalHint"));
    }
}
