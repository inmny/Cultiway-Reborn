using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>材料转换共用普通参数，不为世界材料另建技能编辑器。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>定时和条件转换使用同一组输出、来源和体积容量。</summary>
    /// <param name="node">实际改造的主体。</param>
    /// <param name="operation">当前独立草稿中的真实操作。</param>
    private void MatterConversionParameters(ConstructionNode node, ConstructionOperation operation)
    {
        Choice("ProcessMatter", MaterialName(operation.Matter, operation.State), () =>
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
                        node.FollowMatter = true;
                    })
                }).ToArray(), true));
        Toggle("ConvertWorldMatter", operation.WorldMatter != null,
            enabled => operation.WorldMatter =
                enabled ? new ConstructionWorldMatter { FromMatter = "water", FromState = "liquid" } : null);
        if (operation.WorldMatter == null) return;
        WorldMatterParameters(operation.WorldMatter);
    }

    /// <summary>转换及侵蚀共用实际世界材料筛选，侵蚀候选使用已登记耐受。</summary>
    /// <param name="selection">当前草稿的真实筛选。</param><param name="erosion">是否只选择提供侵蚀耐受的材料。</param>
    private void WorldMatterParameters(ConstructionWorldMatter selection, bool erosion = false)
    {
        var materials = AbilityMatters.All.Where(matter => !erosion || matter.ErosionFactor > 0).ToArray();
        Row("WorldMatterHint", T("WorldMatterHintText"));
        Toggle("WorldMatterFilter", selection.FromMatter != null, enabled =>
        {
            selection.FromMatter = enabled ? erosion ? "wood" : "water" : null;
            selection.FromState = enabled ? erosion ? "solid" : "liquid" : null;
        });
        if (selection.FromMatter != null)
        {
            Choice("WorldMatterFrom",
                MaterialName(selection.FromMatter, selection.FromState), () =>
                    menu.Show(T("WorldMatterFrom"),
                        materials.Select(matter => new UiOptionMenuOption
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
                    enabled || !AbilityMatters.TryResolve(selection.FromMatter, null, out var standard) ? null : standard.State);
        }

        Toggle("WorldMatterOwned", selection.OwnedOnly, enabled => selection.OwnedOnly = enabled);
        Number("WorldMatterMaximum", selection.MaximumVolume, value => selection.MaximumVolume = value);
    }
}
