using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
///     境界视觉表现组件，仅保留渲染所需的核心状态。
///     具体的视觉参数（颜色、贴图等）由 RealmVisualManager 根据 definition_index 提供。
/// </summary>
public struct RealmVisual : IComponent
{
    /// <summary>
    ///     RealmVisualDefinition 的索引。如果为 byte.MaxValue，表示当前无可用的视觉定义。
    /// </summary>
    public byte definition_index;

    /// <summary>
    ///     当前境界阶段，用于调试或在游戏中显示。
    /// </summary>
    public byte realm_stage;

    /// <summary>
    ///     视觉状态（0=默认，预留扩展：1=战斗，2=修炼，3=突破，4=受伤）。
    /// </summary>
    public byte visual_state;

    /// <summary>
    ///     指示需要展示的境界标识位。bit0=金丹，bit1=元婴。
    /// </summary>
    public byte indicator_flags;

    /// <summary>
    ///     是否拥有灵根，用于粒子效果判定。
    /// </summary>
    public bool has_element_root;

    public bool HasDefinition => definition_index != byte.MaxValue;

    public const byte IndicatorFlagJindan = 1 << 0;
    public const byte IndicatorFlagYuanying = 1 << 1;
    public const byte VisualStateDefault = 0;
    public const byte VisualStateBattle = 1;
    public const byte VisualStateCultivate = 2;
    public const byte VisualStateBreakthrough = 3;
    public const byte VisualStateHurt = 4;
}
