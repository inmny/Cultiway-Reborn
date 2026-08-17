// ReSharper disable InconsistentNaming

namespace Cultiway.Content.Const;

public static class ContentActorDataKeys
{
    /// <summary>
    /// 单次普通闭关修炼剩余时间
    /// </summary>
    public const string CultivateTime_float = "cw.content.cultivate_time";
    /// <summary>
    /// 魔法冥想单轮闭关剩余时间（秒）；-SecPerMonth 表示未开始
    /// </summary>
    public const string MagicMeditateTime_float = "cw.content.magic_meditate_time";
    /// <summary>
    /// 野外闭关修炼结束的世界时间
    /// </summary>
    public const string OutdoorCultivationEndTime_float = "cw.content.outdoor_cultivation_end_time";
    /// <summary>
    /// 下一次允许显示野外闭关修炼特效的世界时间
    /// </summary>
    public const string NextOutdoorCultivationEffectTime_float = "cw.content.next_outdoor_cultivation_effect_time";
    public const string IsFlying_flag = "cw.content.is_flying";
    public const string ManualControlledFlight_flag = "cw.content.manual_controlled_flight";
    /// <summary>
    /// 地缚灵的行为
    /// </summary>
    public const string ConstraintSpiritJob_string = "cw.content.constraint_spirit_job";
    public const string ConstraintSpiritCitizenJob_string = "cw.content.constraint_spirit_citizen_job";
    /// <summary>
    /// 丹药数据增益叠加计数前缀
    /// </summary>
    public const string ElixirDataGainStackPrefix = "cw.content.datagain.stack.";
}
