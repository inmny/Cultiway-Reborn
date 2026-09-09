namespace Cultiway.Content.Const;

/// <summary>妖兽玩法的可调数值。全部是玩法平衡常量，不承载规则分支。</summary>
public static class YaoSetting
{
    /// <summary>允许启灵的原版物种编号。</summary>
    public static readonly string[] AwakeningSpeciesIds =
    {
        "snake", "wolf", "bear", "turtle", "fox", "crocodile",
        "frog", "rabbit", "rat", "chicken", "sheep", "penguin", "dragon",
    };

    /// <summary>启灵要求的最低总分：灵气接触、生存与捕食共同累加。</summary>
    public const float AwakeningMinScore = 20f;

    /// <summary>单次评估按地块灵气浓度最多获得的接触分（浓度取满时）。</summary>
    public const float ExposureMaxGainPerEvaluation = 0.5f;

    /// <summary>一次濒死幸存获得的生存分。</summary>
    public const float SurvivalScoreGain = 4f;

    /// <summary>一次捕食获得的捕食分。</summary>
    public const float HuntScoreGain = 4f;

    /// <summary>濒死幸存判定之间的最短间隔（世界秒）。</summary>
    public const float SurvivalScoreCooldown = 30f;

    /// <summary>启灵候选系统两次评估之间的间隔（真实秒）。</summary>
    public const float AwakeningEvaluationInterval = 5f;

    /// <summary>每次评估最多处理的候选数量。</summary>
    public const int AwakeningCandidatesPerBatch = 8;

    /// <summary>启灵时的初始妖力。</summary>
    public const float InitialYaoPower = 10f;

    /// <summary>启灵时的初始身体稳定度。</summary>
    public const float InitialBodyStability = 80f;

    /// <summary>身体稳定度的固定范围上限。</summary>
    public const float MaximumBodyStability = 100f;

    /// <summary>身体稳定度低于该值时暂停器官炼化与凝丹准备。</summary>
    public const float BodyStabilityLowThreshold = 20f;

    /// <summary>妖躯阶段最多增加的结构容量单位。</summary>
    public const int MaximumOrganCapacityBonus = 2;

    /// <summary>炼血小层次数量。</summary>
    public const int QuenchBloodSteps = 3;

    /// <summary>妖兽境界数量：启灵、炼血、妖躯、妖丹、化形。</summary>
    public const int RealmCount = 5;

    /// <summary>妖力恢复间隔（真实秒），对齐原版每月节奏。</summary>
    public const float YaoPowerRestoreInterval = 5f;

    /// <summary>妖力恢复时允许恢复到的上限比例。</summary>
    public const float YaoPowerRestoreLimit = 0.9f;
}
