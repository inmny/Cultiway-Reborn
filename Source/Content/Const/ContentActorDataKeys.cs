// ReSharper disable InconsistentNaming

namespace Cultiway.Content.Const;

public static class ContentActorDataKeys
{
    /// <summary>
    /// 单次普通闭关修炼剩余时间
    /// </summary>
    public const string CultivateTime_float = "cw.content.cultivate_time";
    /// <summary>
    /// 功法生成状态: -1 未开始，0 已结束，1 正在生成
    /// </summary>
    public const string WaitingForCultibookCreation_int = "cw.content.waiting_for_cultibook_creation";
    /// <summary>
    /// 功法改进状态: -1 未开始，0 已结束，1 正在改进
    /// </summary>
    public const string WaitingForCultibookImprovement_int = "cw.content.waiting_for_cultibook_improvement";
    public const string IsFlying_flag = "cw.content.is_flying";
    /// <summary>
    /// 地缚灵的行为
    /// </summary>
    public const string ConstraintSpiritJob_string = "cw.content.constraint_spirit_job";
    public const string ConstraintSpiritCitizenJob_string = "cw.content.constraint_spirit_citizen_job";
}