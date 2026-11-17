using System;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.UI;

/// <summary>在原操作表单内编辑真实控制目标，沿用共同历史和保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>只记录当前查看的控制，撤销后重新取得实际目标。</summary>
    private int deformationChoice;

    /// <summary>先编辑整段历时和间距，再选择实际命名控制的局部改动。</summary>
    /// <param name="node">当前真实模型。</param><param name="operation">当前独立草稿中的操作。</param>
    private void DeformationParameters(ConstructionNode node, ConstructionOperation operation)
    {
        ConstructionDeformation shape = operation.Deformation;
        Row("DeformationRules", T("DeformationRulesHint"));
        Number("ProcessSeconds", operation.Seconds, value => operation.Seconds = value);
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis;
            Number("DeformationSpacing" + new[] { "X", "Y", "Z" }[axis], shape.Spacing[axis], value => shape.Spacing[component] = value);
        }
        ActionRow("AddDeformationControl", "", () => menu.Show(T("AddDeformationControl"),
            FormResources.Load(node.Form).Description.Controls.Where(control => shape.Controls.All(target => target.Control != control.Id))
                .Select(control => new UiOptionMenuOption
                {
                    Label = control.Id, Select = () => Change(() =>
                    {
                        deformationChoice = shape.Controls.Length;
                        shape.Controls = shape.Controls.Concat(new[] { new ConstructionControlTarget { Control = control.Id } }).ToArray();
                    })
                }).ToArray(), true));
        if (shape.Controls.Length == 0) return;
        deformationChoice = Math.Min(deformationChoice, shape.Controls.Length - 1);
        ConstructionControlTarget selected = shape.Controls[deformationChoice];
        Choice("DeformationControl", selected.Control, () => menu.Show(T("DeformationControl"),
            shape.Controls.Select((target, index) => new UiOptionMenuOption
            {
                Label = target.Control, Selected = target == selected,
                Select = () => { deformationChoice = index; Refresh(); }
            }).ToArray(), true));
        ActionRow("RemoveDeformationControl", "", () => Change(() => shape.Controls = shape.Controls.Where(target => target != selected).ToArray()));
        for (int axis = 0; axis < 3; axis++)
        {
            int component = axis; string name = new[] { "X", "Y", "Z" }[axis];
            Number("DeformationOffset" + name, selected.Offset[axis], value => selected.Offset[component] = value);
            Number("DeformationRotation" + name, selected.Rotation[axis], value => selected.Rotation[component] = value);
        }
    }
}
