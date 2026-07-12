namespace Cultiway.Content.Const;

public static class MagicSetting
{
    /// <summary>魔法体系的等级数量，对应 0-9 级。</summary>
    public const int   LevelNumber                 = 10;

    /// <summary>突破前置检查要求的当前精神力占最大精神力比例。</summary>
    public const float CommonPreUpgradeSpiritRatio = 0.8f;

    /// <summary>单轮冥想闭关月数的随机倍率下限（实际月数 = (等级+1) × random[Min,Max]）</summary>
    public const float MeditateSessionMinMonths    = 1f;

    /// <summary>单轮冥想闭关月数的随机倍率上限</summary>
    public const float MeditateSessionMaxMonths    = 3f;

    /// <summary>mana 护盾：每豁免 1 点伤害消耗的 mana（默认0.1，即豁免10伤害耗1 mana）</summary>
    public const float ManaShieldCostRatio         = 0.1f;

    /// <summary>动态法术连续未被访问多少年后从魔网下架。</summary>
    public const float MagicWebExpirationYears     = 50f;

    /// <summary>魔网检查过期法术的时间间隔，单位为游戏年。</summary>
    public const float MagicWebSweepIntervalYears  = 1f;

    /// <summary>魔法师研究法术所需的最低元素加权亲和度。</summary>
    public const float MagicStudyAffinityThreshold = 0.25f;

    /// <summary>单次研究选取最多从魔网取得的候选条目数量。</summary>
    public const int   MagicStudyQueryLimit         = 48;

    /// <summary>研究难度基础值，实际难度为该值乘以 (环位+1) 的平方。</summary>
    public const float MagicStudyBaseDifficulty     = 8f;

    /// <summary>一轮研究未完成后再次继续研究前的等待年数。</summary>
    public const float MagicStudyRetryYears         = 0.5f;

    /// <summary>成功学会法术后再次发起魔网研究前的冷却年数。</summary>
    public const float MagicStudySuccessCooldownYears = 2f;

    /// <summary>没有可研究候选时再次查询魔网前的退避年数。</summary>
    public const float MagicStudyNoCandidateBackoffYears = 5f;

    /// <summary>容量已满时，新候选分数相对被替换法术分数所需达到的倍率。</summary>
    public const float MagicReplacementScoreRatio   = 1.2f;

    /// <summary>零环且尚未改进的法术所需的基础使用积累。</summary>
    public const float MagicSpellImprovementBaseUses = 32f;

    /// <summary>每一环增加的改进使用需求倍率，参与 (1+环位×该值) 计算。</summary>
    public const float MagicSpellImprovementRingUseFactor = 0.5f;

    /// <summary>每次既有改进增加的使用需求倍率，参与 (1+改进次数×该值) 计算。</summary>
    public const float MagicSpellImprovementCountUseFactor = 0.75f;

    /// <summary>智力降低改进需求时的缩放值，实际除数为 sqrt(1+智力/该值)。</summary>
    public const float MagicSpellImprovementIntelligenceScale = 20f;

    /// <summary>改进成功或候选生成失败后，再次尝试改进前的等待年数。</summary>
    public const float MagicSpellImprovementRetryYears = 1f;

    /// <summary>升级法术已有词条时使用的候选权重倍率。</summary>
    public const float MagicSpellImprovementExistingModifierWeight = 0.5f;

    /// <summary>为法术添加新词条时使用的候选权重倍率。</summary>
    public const float MagicSpellImprovementNewModifierWeight = 1f;

    /// <summary>相对于 Mod 根目录的魔法等级属性表路径。</summary>
    public const string StatsPath                  = "Content/Cultisys/Magic.csv";
}
