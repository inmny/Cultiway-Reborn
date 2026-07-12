using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Extensions;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门特质集合。
/// </summary>
[Dependency(typeof(SectTraitGroups), typeof(WorldboxGame.BaseStats))]
public class SectTraits : ExtendLibrary<SectTrait, SectTraits>
{
    /// <summary>
    /// 隐世山门：偏好山地和远离城市的传统宗门驻地。
    /// </summary>
    public static SectTrait SecludedMountainGate { get; private set; }

    /// <summary>
    /// 城坊别院：允许依附城市周边建立驻地。
    /// </summary>
    public static SectTrait CityAttachedBranch { get; private set; }

    /// <summary>
    /// 逐灵择地：优先追逐高灵气区域。
    /// </summary>
    public static SectTrait ResourceSeekingGate { get; private set; }

    /// <summary>
    /// 开疆立派：偏好宽阔空间并远离其他宗门。
    /// </summary>
    public static SectTrait TerritorialGate { get; private set; }

    /// <summary>
    /// 广开山门：降低入门筛选，扩大招揽范围。
    /// </summary>
    public static SectTrait OpenGate { get; private set; }

    /// <summary>
    /// 择才而录：更看重高境界候选人。
    /// </summary>
    public static SectTrait SelectiveAdmission { get; private set; }

    /// <summary>
    /// 师引入门：外部招揽需要能落实师承。
    /// </summary>
    public static SectTrait MasterIntroducedAdmission { get; private set; }

    /// <summary>
    /// 附庸纳士：更适合围绕城市招揽门人。
    /// </summary>
    public static SectTrait CityAttachedRecruitment { get; private set; }

    /// <summary>
    /// 师徒森严：自动安排师父更挑剔。
    /// </summary>
    public static SectTrait StrictLineage { get; private set; }

    /// <summary>
    /// 传功宽松：师承关系更容易建立。
    /// </summary>
    public static SectTrait LooseTransmission { get; private set; }

    /// <summary>
    /// 一脉单传：师徒容量更少但传功收益更高。
    /// </summary>
    public static SectTrait SingleLineage { get; private set; }

    /// <summary>
    /// 众师共授：讲法和公共传授更强。
    /// </summary>
    public static SectTrait CollectiveInstruction { get; private set; }

    /// <summary>
    /// 境界为尊：人事评分偏重境界。
    /// </summary>
    public static SectTrait RealmSupremacy { get; private set; }

    /// <summary>
    /// 功绩为先：人事评分偏重贡献。
    /// </summary>
    public static SectTrait MeritFirst { get; private set; }

    /// <summary>
    /// 资历有序：人事评分偏重入宗时长。
    /// </summary>
    public static SectTrait SeniorityOrder { get; private set; }

    /// <summary>
    /// 破格拔擢：整体晋升门槛更低。
    /// </summary>
    public static SectTrait ExceptionalPromotion { get; private set; }

    /// <summary>
    /// 执事治务：执事名额更多，事务层更发达。
    /// </summary>
    public static SectTrait DeaconGovernance { get; private set; }

    /// <summary>
    /// 长老执权：长老名额和讲法权重更高。
    /// </summary>
    public static SectTrait ElderAuthority { get; private set; }

    /// <summary>
    /// 弟子自治：内门及以上弟子可参与整理藏经阁。
    /// </summary>
    public static SectTrait DiscipleSelfGovernance { get; private set; }

    /// <summary>
    /// 层级森严：职司晋升门槛更高。
    /// </summary>
    public static SectTrait StrictHierarchy { get; private set; }

    /// <summary>
    /// 主脉独尊：更重视宗门主修功法。
    /// </summary>
    public static SectTrait DoctrineOrthodoxy { get; private set; }

    /// <summary>
    /// 百家兼修：更鼓励兼修多类藏书。
    /// </summary>
    public static SectTrait HundredSchools { get; private set; }

    /// <summary>
    /// 术法精研：更重视术法书。
    /// </summary>
    public static SectTrait SkillResearch { get; private set; }

    /// <summary>
    /// 丹鼎传承：更重视丹方。
    /// </summary>
    public static SectTrait ElixirInheritance { get; private set; }

    /// <summary>
    /// 藏法森严：权限外研读代价更高，传承事务奖励更高。
    /// </summary>
    public static SectTrait StrictScripture { get; private set; }

    /// <summary>
    /// 勤务井然：杂务和整理藏经阁更常见。
    /// </summary>
    public static SectTrait OrderlyChores { get; private set; }

    /// <summary>
    /// 讲法成风：讲法更常见、收益更高。
    /// </summary>
    public static SectTrait LectureCulture { get; private set; }

    /// <summary>
    /// 兴土木：建设贡献更高。
    /// </summary>
    public static SectTrait ConstructionZeal { get; private set; }

    /// <summary>
    /// 清修少事：减少事务倾向，降低研读成本。
    /// </summary>
    public static SectTrait QuietCultivation { get; private set; }

    /// <summary>
    /// 赏罚分明：贡献奖励更高，贡献也更影响人事。
    /// </summary>
    public static SectTrait RewardAndPunishment { get; private set; }

    /// <summary>
    /// 重奖传承：典籍、整理和讲法奖励更高。
    /// </summary>
    public static SectTrait TransmissionReward { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.Trait";

    protected override void OnInit()
    {
        SetupResidenceStrategies();
        SetupEntrancePolicies();
        SetupMasterPolicies();
        SetupPromotionPolicies();
        SetupOfficePolicies();
        SetupTransmissionPolicies();
        SetupAffairPolicies();
    }

    private static void SetupResidenceStrategies()
    {
        SetupResidenceStrategy(
            SecludedMountainGate,
            iconPath: "cultiway/icons/sect_traits/secluded_mountain_gate",
            foundingWeight: 45,
            wakanWeight: 1.2f,
            terrainWeight: 1.35f,
            cityDistanceWeight: 1.15f,
            buildSpaceWeight: 0.85f,
            sectDistanceWeight: 1f);

        SetupResidenceStrategy(
            CityAttachedBranch,
            iconPath: "cultiway/icons/sect_traits/city_attached_branch",
            foundingWeight: 15,
            wakanWeight: 0.8f,
            terrainWeight: 0.55f,
            cityDistanceWeight: 1f,
            buildSpaceWeight: 1.15f,
            sectDistanceWeight: 0.85f,
            allowCityZones: true,
            preferCityProximity: true);

        SetupResidenceStrategy(
            ResourceSeekingGate,
            iconPath: "cultiway/icons/sect_traits/resource_seeking_gate",
            foundingWeight: 30,
            wakanWeight: 1.7f,
            terrainWeight: 0.8f,
            cityDistanceWeight: 0.65f,
            buildSpaceWeight: 1f,
            sectDistanceWeight: 0.9f);

        SetupResidenceStrategy(
            TerritorialGate,
            iconPath: "cultiway/icons/sect_traits/territorial_gate",
            foundingWeight: 10,
            wakanWeight: 1f,
            terrainWeight: 1f,
            cityDistanceWeight: 0.85f,
            buildSpaceWeight: 1.35f,
            sectDistanceWeight: 1.8f);
    }

    private static void SetupEntrancePolicies()
    {
        SetupPolicy(OpenGate, SectTraitGroups.EntranceSystem, "cultiway/icons/sect_traits/open_gate", 35);
        SetModifier(OpenGate, WorldboxGame.BaseStats.RecruitRangeModifier, 1.35f);
        SetModifier(OpenGate, WorldboxGame.BaseStats.RecruitRealmScoreModifier, 0.85f);

        SetupPolicy(SelectiveAdmission, SectTraitGroups.EntranceSystem, "cultiway/icons/sect_traits/selective_admission", 25);
        SetModifier(SelectiveAdmission, WorldboxGame.BaseStats.RecruitRangeModifier, 0.8f);
        SetBonus(SelectiveAdmission, WorldboxGame.BaseStats.RecruitMaxLevelBonus, 1);
        SetModifier(SelectiveAdmission, WorldboxGame.BaseStats.RecruitRealmScoreModifier, 1.6f);

        SetupPolicy(MasterIntroducedAdmission, SectTraitGroups.EntranceSystem, "cultiway/icons/sect_traits/master_introduced_admission", 20);
        MasterIntroducedAdmission.base_stats.addTag(WorldboxGame.BaseStats.TagRecruitRequiresMasterIntroduction);
        SetModifier(MasterIntroducedAdmission, WorldboxGame.BaseStats.MasterWillingnessThresholdModifier, 0.9f);
        SetModifier(MasterIntroducedAdmission, WorldboxGame.BaseStats.MasterApprenticeCapacityModifier, 1.15f);

        SetupPolicy(CityAttachedRecruitment, SectTraitGroups.EntranceSystem, "cultiway/icons/sect_traits/city_attached_recruitment", 20);
        SetModifier(CityAttachedRecruitment, WorldboxGame.BaseStats.RecruitRangeModifier, 1.2f);
        SetModifier(CityAttachedRecruitment, WorldboxGame.BaseStats.DeaconSlotModifier, 1.1f);
    }

    private static void SetupMasterPolicies()
    {
        SetupPolicy(StrictLineage, SectTraitGroups.MasterSystem, "cultiway/icons/sect_traits/strict_lineage", 25);
        SetModifier(StrictLineage, WorldboxGame.BaseStats.MasterWillingnessThresholdModifier, 1.35f);
        SetModifier(StrictLineage, WorldboxGame.BaseStats.MasterApprenticeCapacityModifier, 0.75f);
        SetModifier(StrictLineage, WorldboxGame.BaseStats.TeachingGainModifier, 1.15f);
        SetModifier(StrictLineage, WorldboxGame.BaseStats.PromotionScoreThresholdModifier, 1.05f);

        SetupPolicy(LooseTransmission, SectTraitGroups.MasterSystem, "cultiway/icons/sect_traits/loose_transmission", 30);
        SetModifier(LooseTransmission, WorldboxGame.BaseStats.MasterWillingnessThresholdModifier, 0.65f);
        SetModifier(LooseTransmission, WorldboxGame.BaseStats.MasterApprenticeCapacityModifier, 1.4f);

        SetupPolicy(SingleLineage, SectTraitGroups.MasterSystem, "cultiway/icons/sect_traits/single_lineage", 15);
        SetModifier(SingleLineage, WorldboxGame.BaseStats.MasterWillingnessThresholdModifier, 1.1f);
        SetModifier(SingleLineage, WorldboxGame.BaseStats.MasterApprenticeCapacityModifier, 0.5f);
        SetModifier(SingleLineage, WorldboxGame.BaseStats.TeachingGainModifier, 1.3f);
        SetModifier(SingleLineage, WorldboxGame.BaseStats.DoctrineLectureWeightModifier, 1.2f);

        SetupPolicy(CollectiveInstruction, SectTraitGroups.MasterSystem, "cultiway/icons/sect_traits/collective_instruction", 30);
        SetModifier(CollectiveInstruction, WorldboxGame.BaseStats.MasterWillingnessThresholdModifier, 0.8f);
        SetModifier(CollectiveInstruction, WorldboxGame.BaseStats.MasterApprenticeCapacityModifier, 1.2f);
        SetModifier(CollectiveInstruction, WorldboxGame.BaseStats.TeachingGainModifier, 1.15f);
        SetBonus(CollectiveInstruction, WorldboxGame.BaseStats.LectureMaxAudienceBonus, 1);
    }

    private static void SetupPromotionPolicies()
    {
        SetupPolicy(RealmSupremacy, SectTraitGroups.PromotionEvaluation, "cultiway/icons/sect_traits/realm_supremacy", 30);
        SetModifier(RealmSupremacy, WorldboxGame.BaseStats.PersonnelRealmScoreModifier, 1.35f);
        SetModifier(RealmSupremacy, WorldboxGame.BaseStats.PersonnelTenureScoreModifier, 0.75f);
        SetModifier(RealmSupremacy, WorldboxGame.BaseStats.PersonnelContributionScoreModifier, 0.75f);

        SetupPolicy(MeritFirst, SectTraitGroups.PromotionEvaluation, "cultiway/icons/sect_traits/merit_first", 30);
        SetModifier(MeritFirst, WorldboxGame.BaseStats.PersonnelRealmScoreModifier, 0.9f);
        SetModifier(MeritFirst, WorldboxGame.BaseStats.PersonnelTenureScoreModifier, 0.75f);
        SetModifier(MeritFirst, WorldboxGame.BaseStats.PersonnelContributionScoreModifier, 1.5f);

        SetupPolicy(SeniorityOrder, SectTraitGroups.PromotionEvaluation, "cultiway/icons/sect_traits/seniority_order", 20);
        SetModifier(SeniorityOrder, WorldboxGame.BaseStats.PersonnelRealmScoreModifier, 0.9f);
        SetModifier(SeniorityOrder, WorldboxGame.BaseStats.PersonnelTenureScoreModifier, 2f);
        SetModifier(SeniorityOrder, WorldboxGame.BaseStats.PersonnelContributionScoreModifier, 0.8f);

        SetupPolicy(ExceptionalPromotion, SectTraitGroups.PromotionEvaluation, "cultiway/icons/sect_traits/exceptional_promotion", 20);
        SetModifier(ExceptionalPromotion, WorldboxGame.BaseStats.PersonnelRealmScoreModifier, 1.1f);
        SetModifier(ExceptionalPromotion, WorldboxGame.BaseStats.PersonnelContributionScoreModifier, 1.1f);
        SetModifier(ExceptionalPromotion, WorldboxGame.BaseStats.PromotionScoreThresholdModifier, 0.8f);
    }

    private static void SetupOfficePolicies()
    {
        SetupPolicy(DeaconGovernance, SectTraitGroups.OfficeAppointment, "cultiway/icons/sect_traits/deacon_governance", 30);
        SetModifier(DeaconGovernance, WorldboxGame.BaseStats.DeaconSlotModifier, 1.75f);
        SetModifier(DeaconGovernance, WorldboxGame.BaseStats.DeaconThresholdModifier, 0.85f);
        SetModifier(DeaconGovernance, WorldboxGame.BaseStats.OrganizeScriptureAffairWeightModifier, 1.25f);

        SetupPolicy(ElderAuthority, SectTraitGroups.OfficeAppointment, "cultiway/icons/sect_traits/elder_authority", 25);
        SetModifier(ElderAuthority, WorldboxGame.BaseStats.DeaconSlotModifier, 0.9f);
        SetModifier(ElderAuthority, WorldboxGame.BaseStats.ElderSlotModifier, 1.5f);
        SetModifier(ElderAuthority, WorldboxGame.BaseStats.ElderThresholdModifier, 0.9f);
        SetModifier(ElderAuthority, WorldboxGame.BaseStats.LectureAffairWeightModifier, 1.25f);

        SetupPolicy(DiscipleSelfGovernance, SectTraitGroups.OfficeAppointment, "cultiway/icons/sect_traits/disciple_self_governance", 25);
        SetModifier(DiscipleSelfGovernance, WorldboxGame.BaseStats.DeaconSlotModifier, 0.7f);
        SetModifier(DiscipleSelfGovernance, WorldboxGame.BaseStats.ElderSlotModifier, 0.7f);
        DiscipleSelfGovernance.base_stats.addTag(WorldboxGame.BaseStats.TagAllowDiscipleOrganizeScripture);
        SetModifier(DiscipleSelfGovernance, WorldboxGame.BaseStats.ChoreAffairWeightModifier, 1.2f);

        SetupPolicy(StrictHierarchy, SectTraitGroups.OfficeAppointment, "cultiway/icons/sect_traits/strict_hierarchy", 20);
        SetModifier(StrictHierarchy, WorldboxGame.BaseStats.DeaconThresholdModifier, 1.1f);
        SetModifier(StrictHierarchy, WorldboxGame.BaseStats.ElderThresholdModifier, 1.15f);
        SetModifier(StrictHierarchy, WorldboxGame.BaseStats.ElderSlotModifier, 0.8f);
        SetModifier(StrictHierarchy, WorldboxGame.BaseStats.PromotionScoreThresholdModifier, 1.05f);
    }

    private static void SetupTransmissionPolicies()
    {
        SetupPolicy(DoctrineOrthodoxy, SectTraitGroups.TransmissionDirection, "cultiway/icons/sect_traits/doctrine_orthodoxy", 30);
        SetModifier(DoctrineOrthodoxy, WorldboxGame.BaseStats.DoctrineCultibookStudyModifier, 1.5f);
        SetModifier(DoctrineOrthodoxy, WorldboxGame.BaseStats.OtherCultibookStudyModifier, 0.5f);
        SetModifier(DoctrineOrthodoxy, WorldboxGame.BaseStats.SkillbookStudyModifier, 0.85f);
        SetModifier(DoctrineOrthodoxy, WorldboxGame.BaseStats.ElixirbookStudyModifier, 0.85f);
        SetModifier(DoctrineOrthodoxy, WorldboxGame.BaseStats.DoctrineLectureWeightModifier, 1.6f);
        SetModifier(DoctrineOrthodoxy, WorldboxGame.BaseStats.SectStudyJobChanceModifier, 1.1f);

        SetupPolicy(HundredSchools, SectTraitGroups.TransmissionDirection, "cultiway/icons/sect_traits/hundred_schools", 25);
        SetModifier(HundredSchools, WorldboxGame.BaseStats.OtherCultibookStudyModifier, 1.3f);
        SetModifier(HundredSchools, WorldboxGame.BaseStats.SkillbookStudyModifier, 1.2f);
        SetModifier(HundredSchools, WorldboxGame.BaseStats.ElixirbookStudyModifier, 1.2f);
        SetModifier(HundredSchools, WorldboxGame.BaseStats.OutOfPermissionReadCostModifier, 0.85f);
        SetModifier(HundredSchools, WorldboxGame.BaseStats.SectStudyJobChanceModifier, 1.15f);

        SetupPolicy(SkillResearch, SectTraitGroups.TransmissionDirection, "cultiway/icons/sect_traits/skill_research", 18);
        SetModifier(SkillResearch, WorldboxGame.BaseStats.DoctrineCultibookStudyModifier, 0.9f);
        SetModifier(SkillResearch, WorldboxGame.BaseStats.OtherCultibookStudyModifier, 0.85f);
        SetModifier(SkillResearch, WorldboxGame.BaseStats.SkillbookStudyModifier, 1.8f);
        SetModifier(SkillResearch, WorldboxGame.BaseStats.SectStudyJobChanceModifier, 1.15f);

        SetupPolicy(ElixirInheritance, SectTraitGroups.TransmissionDirection, "cultiway/icons/sect_traits/elixir_inheritance", 18);
        SetModifier(ElixirInheritance, WorldboxGame.BaseStats.DoctrineCultibookStudyModifier, 0.9f);
        SetModifier(ElixirInheritance, WorldboxGame.BaseStats.SkillbookStudyModifier, 0.85f);
        SetModifier(ElixirInheritance, WorldboxGame.BaseStats.ElixirbookStudyModifier, 1.8f);
        SetModifier(ElixirInheritance, WorldboxGame.BaseStats.SectStudyJobChanceModifier, 1.15f);

        SetupPolicy(StrictScripture, SectTraitGroups.TransmissionDirection, "cultiway/icons/sect_traits/strict_scripture", 9);
        SetModifier(StrictScripture, WorldboxGame.BaseStats.OutOfPermissionReadCostModifier, 1.8f);
        SetModifier(StrictScripture, WorldboxGame.BaseStats.OrganizeScriptureContributionModifier, 1.2f);
        SetModifier(StrictScripture, WorldboxGame.BaseStats.WriteScriptureContributionModifier, 1.25f);
        SetModifier(StrictScripture, WorldboxGame.BaseStats.SectStudyJobChanceModifier, 0.85f);
    }

    private static void SetupAffairPolicies()
    {
        SetupPolicy(OrderlyChores, SectTraitGroups.SectAffairPolicy, "cultiway/icons/sect_traits/orderly_chores", 25);
        SetModifier(OrderlyChores, WorldboxGame.BaseStats.ChoreAffairWeightModifier, 1.6f);
        SetModifier(OrderlyChores, WorldboxGame.BaseStats.OrganizeScriptureAffairWeightModifier, 1.25f);
        SetModifier(OrderlyChores, WorldboxGame.BaseStats.ChoreContributionModifier, 1.2f);
        SetModifier(OrderlyChores, WorldboxGame.BaseStats.OrganizeScriptureContributionModifier, 1.1f);
        SetModifier(OrderlyChores, WorldboxGame.BaseStats.SectAffairJobChanceModifier, 1.2f);

        SetupPolicy(LectureCulture, SectTraitGroups.SectAffairPolicy, "cultiway/icons/sect_traits/lecture_culture", 20);
        SetModifier(LectureCulture, WorldboxGame.BaseStats.LectureAffairWeightModifier, 2f);
        SetModifier(LectureCulture, WorldboxGame.BaseStats.LectureContributionModifier, 1.2f);
        SetModifier(LectureCulture, WorldboxGame.BaseStats.TeachingGainModifier, 1.15f);
        SetBonus(LectureCulture, WorldboxGame.BaseStats.LectureMaxAudienceBonus, 1);
        SetModifier(LectureCulture, WorldboxGame.BaseStats.SectAffairJobChanceModifier, 1.15f);

        SetupPolicy(ConstructionZeal, SectTraitGroups.SectAffairPolicy, "cultiway/icons/sect_traits/construction_zeal", 20);
        SetModifier(ConstructionZeal, WorldboxGame.BaseStats.ChoreAffairWeightModifier, 1.2f);
        SetModifier(ConstructionZeal, WorldboxGame.BaseStats.BuildContributionModifier, 1.5f);
        SetModifier(ConstructionZeal, WorldboxGame.BaseStats.SectAffairJobChanceModifier, 1.1f);

        SetupPolicy(QuietCultivation, SectTraitGroups.SectAffairPolicy, "cultiway/icons/sect_traits/quiet_cultivation", 15);
        SetModifier(QuietCultivation, WorldboxGame.BaseStats.ChoreAffairWeightModifier, 0.5f);
        SetModifier(QuietCultivation, WorldboxGame.BaseStats.OrganizeScriptureAffairWeightModifier, 0.5f);
        SetModifier(QuietCultivation, WorldboxGame.BaseStats.LectureAffairWeightModifier, 0.5f);
        SetModifier(QuietCultivation, WorldboxGame.BaseStats.OutOfPermissionReadCostModifier, 0.85f);
        SetModifier(QuietCultivation, WorldboxGame.BaseStats.SectStudyJobChanceModifier, 1.2f);
        SetModifier(QuietCultivation, WorldboxGame.BaseStats.SectAffairJobChanceModifier, 0.5f);

        SetupPolicy(RewardAndPunishment, SectTraitGroups.SectAffairPolicy, "cultiway/icons/sect_traits/reward_and_punishment", 12);
        SetModifier(RewardAndPunishment, WorldboxGame.BaseStats.ChoreContributionModifier, 1.35f);
        SetModifier(RewardAndPunishment, WorldboxGame.BaseStats.OrganizeScriptureContributionModifier, 1.35f);
        SetModifier(RewardAndPunishment, WorldboxGame.BaseStats.LectureContributionModifier, 1.35f);
        SetModifier(RewardAndPunishment, WorldboxGame.BaseStats.BuildContributionModifier, 1.35f);
        SetModifier(RewardAndPunishment, WorldboxGame.BaseStats.PersonnelContributionScoreModifier, 1.2f);
        SetModifier(RewardAndPunishment, WorldboxGame.BaseStats.SectAffairJobChanceModifier, 1.1f);

        SetupPolicy(TransmissionReward, SectTraitGroups.SectAffairPolicy, "cultiway/icons/sect_traits/transmission_reward", 8);
        SetModifier(TransmissionReward, WorldboxGame.BaseStats.OrganizeScriptureContributionModifier, 1.3f);
        SetModifier(TransmissionReward, WorldboxGame.BaseStats.LectureContributionModifier, 1.3f);
        SetModifier(TransmissionReward, WorldboxGame.BaseStats.WriteScriptureContributionModifier, 1.6f);
        SetModifier(TransmissionReward, WorldboxGame.BaseStats.SectAffairJobChanceModifier, 1.05f);
    }

    private static void SetupResidenceStrategy(
        SectTrait trait,
        string iconPath,
        int foundingWeight,
        float wakanWeight,
        float terrainWeight,
        float cityDistanceWeight,
        float buildSpaceWeight,
        float sectDistanceWeight,
        bool allowCityZones = false,
        bool preferCityProximity = false)
    {
        trait.group_id = SectTraitGroups.ResidenceStrategy.id;
        trait.path_icon = iconPath;
        trait.needs_to_be_explored = false;
        trait.show_in_knowledge_window = false;
        trait.can_be_given = false;
        trait.can_be_removed = false;
        trait.isResidenceStrategy = true;
        trait.foundingWeight = foundingWeight;
        trait.allowCityResidenceZones = allowCityZones;
        trait.preferCityProximity = preferCityProximity;
        trait.residenceWakanScoreWeight = wakanWeight;
        trait.residenceTerrainScoreWeight = terrainWeight;
        trait.residenceCityDistanceScoreWeight = cityDistanceWeight;
        trait.residenceBuildSpaceScoreWeight = buildSpaceWeight;
        trait.residenceSectDistanceScoreWeight = sectDistanceWeight;
    }

    private static void SetupPolicy(SectTrait trait, SectTraitGroupAsset group, string iconPath, int foundingWeight)
    {
        trait.group_id = group.id;
        trait.path_icon = iconPath;
        trait.needs_to_be_explored = false;
        trait.show_in_knowledge_window = false;
        trait.isFoundingPolicy = true;
        trait.policyFoundingWeight = foundingWeight;
    }

    private static void SetModifier(SectTrait trait, BaseStatAsset stat, float multiplier)
    {
        trait.base_stats[stat.id] = multiplier - 1f;
    }

    private static void SetBonus(SectTrait trait, BaseStatAsset stat, float value)
    {
        trait.base_stats[stat.id] = value;
    }

    /// <summary>
    /// 判断指定建宗者是否至少存在一种可用的驻地策略。
    /// </summary>
    public static bool HasFoundingResidenceStrategy(Actor founder)
    {
        List<SectTrait> strategies = ModClass.L.SectTraitLibrary.GetResidenceStrategies();
        for (int i = 0; i < strategies.Count; i++)
        {
            SectTrait strategy = strategies[i];
            if (strategy.foundingWeight <= 0) continue;
            if (SectResidencePlanner.HasFoundingSite(founder, strategy))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从所有当前可用的驻地策略中按权重抽取一个建宗策略。
    /// </summary>
    public static bool TryPickFoundingResidenceStrategy(Actor founder, out SectTrait trait)
    {
        trait = null;
        List<SectTrait> candidates = GetViableResidenceStrategies(founder);
        if (candidates.Count == 0) return false;

        trait = PickWeighted(candidates, candidate => candidate.foundingWeight);
        return trait != null;
    }

    /// <summary>
    /// 为新宗门从每个制度分组中各抽取一项初始制度。
    /// </summary>
    public static List<SectTrait> PickFoundingPolicies()
    {
        List<SectTrait> result = new();
        AddFoundingPolicy(result, SectTraitGroups.EntranceSystem);
        AddFoundingPolicy(result, SectTraitGroups.MasterSystem);
        AddFoundingPolicy(result, SectTraitGroups.PromotionEvaluation);
        AddFoundingPolicy(result, SectTraitGroups.OfficeAppointment);
        AddFoundingPolicy(result, SectTraitGroups.TransmissionDirection);
        AddFoundingPolicy(result, SectTraitGroups.SectAffairPolicy);
        return result;
    }

    private static void AddFoundingPolicy(List<SectTrait> result, SectTraitGroupAsset group)
    {
        List<SectTrait> candidates = ModClass.L.SectTraitLibrary.GetFoundingPolicies(group.id);
        SectTrait picked = PickWeighted(candidates, candidate => candidate.policyFoundingWeight);
        if (picked != null)
        {
            result.Add(picked);
        }
    }

    private static List<SectTrait> GetViableResidenceStrategies(Actor founder)
    {
        List<SectTrait> result = new();
        List<SectTrait> strategies = ModClass.L.SectTraitLibrary.GetResidenceStrategies();
        for (int i = 0; i < strategies.Count; i++)
        {
            SectTrait strategy = strategies[i];
            if (strategy.foundingWeight <= 0) continue;
            if (SectResidencePlanner.HasFoundingSite(founder, strategy))
            {
                result.Add(strategy);
            }
        }

        return result;
    }

    private static SectTrait PickWeighted(List<SectTrait> candidates, System.Func<SectTrait, int> getWeight)
    {
        if (candidates.Count == 0) return null;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += System.Math.Max(0, getWeight(candidates[i]));
        }

        if (totalWeight <= 0) return null;

        int roll = Randy.randomInt(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            SectTrait candidate = candidates[i];
            roll -= System.Math.Max(0, getWeight(candidate));
            if (roll < 0)
            {
                return candidate;
            }
        }

        return candidates[candidates.Count - 1];
    }
}
