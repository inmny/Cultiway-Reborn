using System.Linq;
using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>真实触地和有限传播使用既有普通表单、撤销及正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>从现有身体动作生成普通触地分支，姿态仍由玩家实际编辑。</summary>
    private void PropagateBody() => ApplyConstructionEdit(() =>
        BodyConstructionEdits.Propagate(session.Draft, new ConstructionPropagation()));

    /// <summary>修改已选主体上的定时过程，条件过程在动作页复用相同参数。</summary>
    /// <param name="node">当前独立草稿部位。</param>
    private void PropagationRows(ConstructionNode node)
    {
        foreach (ConstructionOperation operation in node.Operations.Where(o =>
                     o.Kind == ConstructionOperationKind.Propagate && o.When == null))
        {
            Number("PropagationAt", operation.At, value => operation.At = value);
            PropagationParameters(operation.Propagation ??= new ConstructionPropagation());
            ActionRow("RemovePropagation", "", () => Change(() =>
                node.Operations = node.Operations.Where(item => item != operation).ToArray()));
        }
        if (!node.Operations.Any(o => o.Kind == ConstructionOperationKind.Propagate))
            ActionRow("AddPropagation", T("PropagationHint"), () => Change(() =>
                node.Operations = node.Operations.Concat(new[]
                {
                    new ConstructionOperation
                    { Kind = ConstructionOperationKind.Propagate, Propagation = new ConstructionPropagation() }
                }).ToArray()));
    }

    /// <summary>传播的显示和作用共同使用这些参数。</summary>
    /// <param name="settings">当前草稿独占的实际参数。</param>
    private void PropagationParameters(ConstructionPropagation settings)
    {
        Row("PropagationRules", T("PropagationHint"));
        Number("PropagationRange", settings.Range, value => settings.Range = value);
        Number("PropagationSpeed", settings.Speed, value => settings.Speed = value);
        Number("PropagationSeconds", settings.Seconds, value => settings.Seconds = value);
        Number("PropagationHeight", settings.Height, value => settings.Height = value);
        Number("PropagationInner", settings.InnerRadius, value => settings.InnerRadius = value);
        Number("PropagationGroundReach", settings.GroundReach, value => settings.GroundReach = value);
        Number("PropagationTilt", settings.TiltDegrees, value => settings.TiltDegrees = value);
        Number("PropagationAngle", settings.Angle, value => settings.Angle = value);
        Number("PropagationAttenuation", settings.Attenuation, value => settings.Attenuation = value);
        Number("PropagationStep", settings.MaximumStep, value => settings.MaximumStep = value);
        Number("PropagationCost", settings.CellCost, value => settings.CellCost = value);
        Number("PropagationCells", settings.MaximumCells, value => settings.MaximumCells = (int)value);
    }
}
