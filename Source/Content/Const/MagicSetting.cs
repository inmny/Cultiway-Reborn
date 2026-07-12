namespace Cultiway.Content.Const;

public static class MagicSetting
{
    public const int   LevelNumber                 = 10;
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
    public const string StatsPath                  = "Content/Cultisys/Magic.csv";
}
