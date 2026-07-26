using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 由形成状态实体持有的机制数据。持续时间和可驱散性由外层 StatusEffectAsset 负责，
/// 这里只保存护盾、储备、层数和相位等具体数值。
/// </summary>
public struct CoreFormationEffectState : IComponent
{
    /// <summary>延迟恢复、相位切换等机制使用的辅助计时器。</summary>
    public float auxiliary_timer;

    /// <summary>护盾、储备和累计伤害等机制的主数值。</summary>
    public float value;

    /// <summary>恢复速率、再次触发间隔等机制的次数值。</summary>
    public float secondary_value;

    /// <summary>连续命中、适应次数和龙威层数。</summary>
    public int counter;

    /// <summary>元素适应或五相轮转的当前相位。</summary>
    public int phase;

    /// <summary>凝元蓄力和灵台回响的可消费次数。</summary>
    public int charges;
}
