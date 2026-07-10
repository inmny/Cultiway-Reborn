using Cultiway.Abstract;

namespace Cultiway.Content;

/// <summary>
/// 宗门制度使用的通用统计项；宗门特质通过 base_stats 填写这些数值，宗门规则从 Sect.base_stats 读取聚合结果。
/// </summary>
public class SectStats : ExtendLibrary<BaseStatAsset, SectStats>
{
    public const string TagRecruitRequiresMasterIntroduction = "sect_recruit_requires_master_introduction";
    public const string TagAllowDiscipleOrganizeScripture = "sect_allow_disciple_organize_scripture";

    [AssetId(nameof(RecruitRangeModifier))] public static BaseStatAsset RecruitRangeModifier { get; private set; }
    [AssetId(nameof(RecruitMaxLevelBonus))] public static BaseStatAsset RecruitMaxLevelBonus { get; private set; }
    [AssetId(nameof(RecruitRealmScoreModifier))] public static BaseStatAsset RecruitRealmScoreModifier { get; private set; }
    [AssetId(nameof(MasterWillingnessThresholdModifier))] public static BaseStatAsset MasterWillingnessThresholdModifier { get; private set; }
    [AssetId(nameof(MasterApprenticeCapacityModifier))] public static BaseStatAsset MasterApprenticeCapacityModifier { get; private set; }
    [AssetId(nameof(TeachingGainModifier))] public static BaseStatAsset TeachingGainModifier { get; private set; }
    [AssetId(nameof(LectureMaxAudienceBonus))] public static BaseStatAsset LectureMaxAudienceBonus { get; private set; }
    [AssetId(nameof(PersonnelRealmScoreModifier))] public static BaseStatAsset PersonnelRealmScoreModifier { get; private set; }
    [AssetId(nameof(PersonnelTenureScoreModifier))] public static BaseStatAsset PersonnelTenureScoreModifier { get; private set; }
    [AssetId(nameof(PersonnelContributionScoreModifier))] public static BaseStatAsset PersonnelContributionScoreModifier { get; private set; }
    [AssetId(nameof(PromotionScoreThresholdModifier))] public static BaseStatAsset PromotionScoreThresholdModifier { get; private set; }
    [AssetId(nameof(DeaconSlotModifier))] public static BaseStatAsset DeaconSlotModifier { get; private set; }
    [AssetId(nameof(ElderSlotModifier))] public static BaseStatAsset ElderSlotModifier { get; private set; }
    [AssetId(nameof(DeaconThresholdModifier))] public static BaseStatAsset DeaconThresholdModifier { get; private set; }
    [AssetId(nameof(ElderThresholdModifier))] public static BaseStatAsset ElderThresholdModifier { get; private set; }
    [AssetId(nameof(DoctrineCultibookStudyModifier))] public static BaseStatAsset DoctrineCultibookStudyModifier { get; private set; }
    [AssetId(nameof(OtherCultibookStudyModifier))] public static BaseStatAsset OtherCultibookStudyModifier { get; private set; }
    [AssetId(nameof(SkillbookStudyModifier))] public static BaseStatAsset SkillbookStudyModifier { get; private set; }
    [AssetId(nameof(ElixirbookStudyModifier))] public static BaseStatAsset ElixirbookStudyModifier { get; private set; }
    [AssetId(nameof(OutOfPermissionReadCostModifier))] public static BaseStatAsset OutOfPermissionReadCostModifier { get; private set; }
    [AssetId(nameof(DoctrineLectureWeightModifier))] public static BaseStatAsset DoctrineLectureWeightModifier { get; private set; }
    [AssetId(nameof(ChoreAffairWeightModifier))] public static BaseStatAsset ChoreAffairWeightModifier { get; private set; }
    [AssetId(nameof(OrganizeScriptureAffairWeightModifier))] public static BaseStatAsset OrganizeScriptureAffairWeightModifier { get; private set; }
    [AssetId(nameof(LectureAffairWeightModifier))] public static BaseStatAsset LectureAffairWeightModifier { get; private set; }
    [AssetId(nameof(ChoreContributionModifier))] public static BaseStatAsset ChoreContributionModifier { get; private set; }
    [AssetId(nameof(OrganizeScriptureContributionModifier))] public static BaseStatAsset OrganizeScriptureContributionModifier { get; private set; }
    [AssetId(nameof(LectureContributionModifier))] public static BaseStatAsset LectureContributionModifier { get; private set; }
    [AssetId(nameof(BuildContributionModifier))] public static BaseStatAsset BuildContributionModifier { get; private set; }
    [AssetId(nameof(WriteScriptureContributionModifier))] public static BaseStatAsset WriteScriptureContributionModifier { get; private set; }
    [AssetId(nameof(SectStudyJobChanceModifier))] public static BaseStatAsset SectStudyJobChanceModifier { get; private set; }
    [AssetId(nameof(SectAffairJobChanceModifier))] public static BaseStatAsset SectAffairJobChanceModifier { get; private set; }
    [AssetId(nameof(TreasureCapacity))] public static BaseStatAsset TreasureCapacity { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        SetupPercentModifier(RecruitRangeModifier, 910);
        SetupFlatBonus(RecruitMaxLevelBonus, 905);
        SetupPercentModifier(RecruitRealmScoreModifier, 900);
        SetupPercentModifier(MasterWillingnessThresholdModifier, 895);
        SetupPercentModifier(MasterApprenticeCapacityModifier, 890);
        SetupPercentModifier(TeachingGainModifier, 885);
        SetupFlatBonus(LectureMaxAudienceBonus, 880);
        SetupPercentModifier(PersonnelRealmScoreModifier, 875);
        SetupPercentModifier(PersonnelTenureScoreModifier, 870);
        SetupPercentModifier(PersonnelContributionScoreModifier, 865);
        SetupPercentModifier(PromotionScoreThresholdModifier, 860);
        SetupPercentModifier(DeaconSlotModifier, 855);
        SetupPercentModifier(ElderSlotModifier, 850);
        SetupPercentModifier(DeaconThresholdModifier, 845);
        SetupPercentModifier(ElderThresholdModifier, 840);
        SetupPercentModifier(DoctrineCultibookStudyModifier, 835);
        SetupPercentModifier(OtherCultibookStudyModifier, 830);
        SetupPercentModifier(SkillbookStudyModifier, 825);
        SetupPercentModifier(ElixirbookStudyModifier, 820);
        SetupPercentModifier(OutOfPermissionReadCostModifier, 815);
        SetupPercentModifier(DoctrineLectureWeightModifier, 810);
        SetupPercentModifier(ChoreAffairWeightModifier, 805);
        SetupPercentModifier(OrganizeScriptureAffairWeightModifier, 800);
        SetupPercentModifier(LectureAffairWeightModifier, 795);
        SetupPercentModifier(ChoreContributionModifier, 790);
        SetupPercentModifier(OrganizeScriptureContributionModifier, 785);
        SetupPercentModifier(LectureContributionModifier, 780);
        SetupPercentModifier(BuildContributionModifier, 775);
        SetupPercentModifier(WriteScriptureContributionModifier, 770);
        SetupPercentModifier(SectStudyJobChanceModifier, 765);
        SetupPercentModifier(SectAffairJobChanceModifier, 760);
        SetupFlatBonus(TreasureCapacity, 755);
    }

    private static void SetupPercentModifier(BaseStatAsset stat, int sortRank)
    {
        stat.translation_key = stat.id;
        stat.show_as_percents = true;
        stat.tooltip_multiply_for_visual_number = 100f;
        stat.sort_rank = sortRank;
    }

    private static void SetupFlatBonus(BaseStatAsset stat, int sortRank)
    {
        stat.translation_key = stat.id;
        stat.sort_rank = sortRank;
    }
}
