using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>煞风劫的当前结算状态。</summary>
public enum BalefulWindTribulationOutcome : byte
{
    InProgress,
    Passed
}

/// <summary>记录元婴晋级化神时正在经历的九重煞风劫。</summary>
public struct BalefulWindTribulation : IComponent
{
    public const byte TotalWaves = 9;
    public const float InitialDelay = 1f;
    public const float WaveInterval = 2f;

    public byte waves_survived;
    public byte active_wave;
    public double started_at;
    public double next_wave_at;
    public BalefulWindTribulationOutcome outcome;

    public readonly bool IsPassed => outcome == BalefulWindTribulationOutcome.Passed;
}
