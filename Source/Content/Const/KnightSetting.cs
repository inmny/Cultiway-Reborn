namespace Cultiway.Content.Const;

/// <summary>骑士体系的可调常量。</summary>
public static class KnightSetting
{
    /// <summary>骑士体系的等级数量，对应 0-9 级。</summary>
    public const int LevelNumber = 10;

    /// <summary>合格士兵每月觉醒为骑士的概率。</summary>
    public const float AcquisitionChancePerMonth = 0.005f;

    /// <summary>突破前置检查要求的当前斗气占最大斗气比例。</summary>
    public const float CommonPreUpgradeVigorRatio = 0.8f;

    /// <summary>每次有意义击杀获得的斗气占最大斗气的基础比例（再按对手战力缩放，强敌最多 ×2）。</summary>
    public const float KillVigorGainRatio = 0.10f;

    /// <summary>被击中时获得斗气的系数，参与 斗气 = damage × 该系数 × 攻击者战力。</summary>
    public const float BeAttackedVigorGainRatio = 0.012f;

    /// <summary>和平期操练每月获得的斗气占最大斗气的比例（比战斗慢；纯练习也能蓄满，只是更久）。</summary>
    public const float PracticeVigorGainRatioPerMonth = 0.05f;

    /// <summary>弱敌战力低于自身的该比例时不给斗气（防刷弱怪）。</summary>
    public const float WeakFoePowerRatio = 0.3f;

    /// <summary>相对于 Mod 根目录的骑士等级属性表路径。</summary>
    public const string StatsPath = "Content/Cultisys/Knight.csv";

    /// <summary>
    /// 各级自然突破的成功率。BreakthroughSuccessChance[i] 为 i 级 → i+1 级的成功率，
    /// 随等级升高而下降，使 9 级（血脉始祖）稀有且来之不易。
    /// </summary>
    public static readonly float[] BreakthroughSuccessChance =
    {
        0.98f, 0.95f, 0.92f, 0.88f, 0.82f, 0.74f, 0.62f, 0.45f, 0.20f
    };

    /// <summary>血脉基础系数：亲子获得始祖快照的该比例。</summary>
    public const float BloodlineBaseCoefficient = 0.3f;

    /// <summary>血脉每远一代的衰减系数。</summary>
    public const float BloodlineDecayPerGen = 0.7f;

    /// <summary>血脉回溯的最大世代数。</summary>
    public const int BloodlineMaxGenerations = 5;
}
