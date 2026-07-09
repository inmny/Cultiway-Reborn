namespace Cultiway.Content.Const;

public static class MagicSetting
{
    public const int   LevelNumber                 = 10;
    public const float CommonPreUpgradeSpiritRatio = 0.8f;
    public const float MeditateBaseGain            = 0.1f;
    /// <summary>mana 护盾：每豁免 1 点伤害消耗的 mana（默认0.1，即豁免10伤害耗1 mana）</summary>
    public const float ManaShieldCostRatio         = 0.1f;
    public const string StatsPath                  = "Content/Cultisys/Magic.csv";
}
