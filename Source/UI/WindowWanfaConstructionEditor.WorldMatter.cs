using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>材料转换共用普通参数，不为世界材料另建技能编辑器。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>定时和条件转换使用同一组输出、来源和体积容量。</summary>
    /// <param name="operation">当前独立草稿中的真实操作。</param>
    private void MatterConversionParameters(ConstructionOperation operation)
    {
        Choice("ProcessMatter", MaterialName(AbilityMatters.Resolve(operation.Matter, operation.State, "过程输出")), () =>
            menu.Show(T("ProcessMatter"),
                AbilityMatters.All.Select(matter => new UiOptionMenuOption
                {
                    Label = MaterialName(matter),
                    Selected = operation.Matter == matter.Id &&
                               (operation.State == null || operation.State == matter.State),
                    Select = () => Change(() =>
                    {
                        operation.Matter = matter.Id;
                        operation.State = matter.State;
                    })
                }).ToArray(), true));
        Toggle("ConvertWorldMatter", operation.WorldMatter != null,
            enabled => operation.WorldMatter =
                enabled ? new ConstructionWorldMatter { FromMatter = "water", FromState = "liquid" } : null);
        if (operation.WorldMatter == null) return;
        ConstructionWorldMatter selection = operation.WorldMatter;
        Row("WorldMatterHint", T("WorldMatterHintText"));
        Toggle("WorldMatterFilter", selection.FromMatter != null, enabled =>
        {
            selection.FromMatter = enabled ? "water" : null;
            selection.FromState = enabled ? "liquid" : null;
        });
        if (selection.FromMatter != null)
        {
            Choice("WorldMatterFrom",
                MaterialName(AbilityMatters.Resolve(selection.FromMatter, selection.FromState, "来源筛选")), () =>
                    menu.Show(T("WorldMatterFrom"),
                        AbilityMatters.All.Select(matter => new UiOptionMenuOption
                        {
                            Label = MaterialName(matter),
                            Selected = selection.FromMatter == matter.Id &&
                                       (selection.FromState == null || selection.FromState == matter.State),
                            Select = () => Change(() =>
                            {
                                selection.FromMatter = matter.Id;
                                selection.FromState = matter.State;
                            })
                        }).ToArray(), true));
            Toggle("WorldMatterAnyState", selection.FromState == null,
                enabled => selection.FromState =
                    enabled ? null : AbilityMatters.Resolve(selection.FromMatter, null, "来源物态").State);
        }

        Toggle("WorldMatterOwned", selection.OwnedOnly, enabled => selection.OwnedOnly = enabled);
        Number("WorldMatterMaximum", selection.MaximumVolume, value => selection.MaximumVolume = value);
    }
}