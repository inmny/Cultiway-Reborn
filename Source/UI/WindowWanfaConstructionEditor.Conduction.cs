using Cultiway.Core.Skills;

namespace Cultiway.UI;

/// <summary>有界导电使用普通操作参数和相同编译入口。</summary>
public sealed partial class WindowWanfaConstructionEditor
{
    /// <summary>定时和条件操作共用真实水路、消耗及上限。</summary>
    /// <param name="settings">独立草稿的传播资料。</param>
    private void ConductionParameters(ConstructionConduction settings)
    {
        Row("ConductionHint", T("ConductionHintText"));
        Number("ConductionPulses", settings.Pulses, v => settings.Pulses = (int)v);
        Number("ConductionInterval", settings.Interval, v => settings.Interval = v);
        Number("ConductionRange", settings.Range, v => settings.Range = v);
        Number("ConductionDepth", settings.MinimumDepth, v => settings.MinimumDepth = v);
        Number("ConductionTravel", settings.TravelCost, v => settings.TravelCost = v);
        Number("ConductionDamage", settings.DamagePerTarget, v => settings.DamagePerTarget = v);
        Number("ConductionCells", settings.MaximumCells, v => settings.MaximumCells = (int)v);
        Number("ConductionTargets", settings.MaximumTargets, v => settings.MaximumTargets = (int)v);
    }
}