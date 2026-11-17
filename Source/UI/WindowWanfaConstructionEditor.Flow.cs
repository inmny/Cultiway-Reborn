using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>体积和浅层流散的普通参数，沿用草稿、撤销及正式保存。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>定时和真实条件共用收束参数，不显示内部预算账本。</summary>
    /// <param name="operation">实际独立草稿。</param>
    private void RecoveryParameters(ConstructionOperation operation)
    {
        Toggle("RecoveryWorldMaterials", operation.Recovery?.WorldMaterials ?? false, value =>
        {
            operation.Recovery ??= new ConstructionRecovery();
            operation.Recovery.WorldMaterials = value;
        });
        Number("RecoverySeconds", operation.Seconds, value => operation.Seconds = value);
        Number("RecoveryEfficiency", operation.Recovery?.Efficiency ?? .75f, value =>
        { operation.Recovery ??= new ConstructionRecovery(); operation.Recovery.Efficiency = value;
        });
    }

    /// <summary>留置时长是世界独立规则，不依附本次施放的结束时间。</summary>
    /// <param name="operation">当前真实草稿。</param>
    private void ResidueParameters(ConstructionOperation operation)
    {
        Number("ResidueLifetime", operation.Residue?.Lifetime ?? 0, value =>
        {
            operation.Residue ??= new ConstructionResidue();
            operation.Residue.Lifetime = value; });
        Toggle("ResidueErosion", operation.Residue?.Erosion != null, value =>
        {
            operation.Residue ??= new ConstructionResidue();
            operation.Residue.Erosion = value ? new ConstructionErosion() : null;
            operation.Residue.ErosionSeconds = value ? 4f : 0f;
        });
        if (operation.Residue?.Erosion != null)
        {
            Number("ResidueErosionSeconds", operation.Residue.ErosionSeconds, value => operation.Residue.ErosionSeconds = value);
            Number("ResidueErosionFraction", operation.Residue.Erosion.Fraction, value => operation.Residue.Erosion.Fraction = value);
        }
    }

    /// <summary>普通部位和命名步骤共用侵蚀历时、份额及世界材料选择。</summary>
    /// <param name="operation">当前独立草稿中的操作。</param>
    private void ErosionParameters(ConstructionOperation operation)
    {
        Row("ErosionRules", T("ErosionRulesHint"));
        Number("ErosionSeconds", operation.Seconds, value => operation.Seconds = value);
        Number("ErosionFraction", operation.Erosion?.Fraction ?? .75f, value =>
        {
            operation.Erosion ??= new ConstructionErosion();
            operation.Erosion.Fraction = value;
        });
        Toggle("ErosionWorldMaterials", operation.WorldMatter != null, value => operation.WorldMatter = value
            ? new ConstructionWorldMatter { FromMatter = "wood", FromState = "solid" }
            : null);
        if (operation.WorldMatter != null) WorldMatterParameters(operation.WorldMatter, true);
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
