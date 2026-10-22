using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>体积和浅层流散的普通参数，沿用草稿、撤销及正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>定时和真实条件共用收束参数，不显示内部预算账本。</summary>
    /// <param name="operation">实际独立草稿。</param>
    private void RecoveryParameters(ConstructionOperation operation)
    {
        Number("RecoverySeconds", operation.Seconds, value => operation.Seconds = value);
        Number("RecoveryEfficiency", operation.Recovery?.Efficiency ?? .75f, value =>
        { operation.Recovery ??= new ConstructionRecovery(); operation.Recovery.Efficiency = value; });
    }

    /// <summary>只绑定实际参与数值计算的参数。</summary>
    /// <param name="flow">当前草稿中的独立参数。</param>
    private void FlowParameters(ConstructionFlow flow)
    {
        Number("FlowSize", flow.Size, value => flow.Size = value);
        Number("FlowCells", flow.Cells, value => flow.Cells = (int)value);
        Number("FlowGravity", flow.Gravity, value => flow.Gravity = value);
        Number("FlowDrag", flow.Drag, value => flow.Drag = value);
        Number("FlowEvaporation", flow.Evaporation, value => flow.Evaporation = value);
        Toggle("FlowOpen", flow.OpenBoundary, value => flow.OpenBoundary = value);
    }
}
