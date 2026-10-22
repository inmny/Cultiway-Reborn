using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>体积和浅层流散的普通参数，沿用草稿、撤销及正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
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
