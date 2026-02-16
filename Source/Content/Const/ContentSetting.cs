namespace Cultiway.Content.Const;

public static class ContentSetting
{
    public static float MinFlyDist  = 16;
    public static float FlyHeight   = 8;
    public static float FlySpeedMod = 1;
    public static bool  AllXian     = true;
    public static float DirtyWakanToWakanRatio = 9;
    public static float ConstraintSpiritSpawnChance = 0.5f;

    public static float SkillEnhanceBaseChanceSmallSuccess = 0.75f;
    public static float SkillEnhanceBaseChanceSmallFailed = 0.5f;
    public static float SkillEnhanceBaseChanceLargeSuccess = 0.9f;
    public static float SkillEnhanceBaseChanceLargeFailed = 0.38f;
    public static float SkillEnhancePerModifierPenalty = 0.08f;
    public static float SkillEnhanceIntelligenceBonusFactor = 0.0012f;
    public static float SkillEnhanceChanceMin = 0.05f;
    public static float SkillEnhanceChanceMax = 0.95f;

    public static float SkillEnhanceLowLevelWeightFactor = 0.7f;
    public static float SkillEnhanceExistingModifierWeight = 0.35f;
    public static float SkillEnhanceNewModifierWeight = 1.1f;
    public static float SkillEnhanceSimilarityRejectThreshold = 0.72f;
    public static float SkillEnhanceSimilarityPenaltyScale = 0.85f;

    public static float SkillBranchBaseChanceOnEnhanceFail = 0.3f;
    public static float SkillBranchPerModifierBonus = 0.06f;
    public static float SkillBranchMaxChance = 0.85f;
    public static float SkillBranchDuplicateRejectThreshold = 0.95f;
}
