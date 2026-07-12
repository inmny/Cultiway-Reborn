using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cultiway.Abstract;
using NeoModLoader.General.Game.extensions;

namespace Cultiway;

public partial class WorldboxGame
{
    public class BaseStats : ExtendLibrary<BaseStatAsset, BaseStats>
    {
        /// <summary>原版总护甲属性，元素护甲结算后会汇总到该属性。</summary>
        [GetOnly("armor")] public static BaseStatAsset Armor { get; private set; }

        /// <summary>抵抗金属性伤害的护甲。</summary>
        [AssetId(nameof(IronArmor))] public static BaseStatAsset IronArmor { get; private set; }

        /// <summary>抵抗木属性伤害的护甲。</summary>
        [AssetId(nameof(WoodArmor))] public static BaseStatAsset WoodArmor { get; private set; }

        /// <summary>抵抗水属性伤害的护甲。</summary>
        [AssetId(nameof(WaterArmor))] public static BaseStatAsset WaterArmor { get; private set; }

        /// <summary>抵抗火属性伤害的护甲。</summary>
        [AssetId(nameof(FireArmor))] public static BaseStatAsset FireArmor { get; private set; }

        /// <summary>抵抗土属性伤害的护甲。</summary>
        [AssetId(nameof(EarthArmor))] public static BaseStatAsset EarthArmor { get; private set; }

        /// <summary>抵抗阴属性伤害的护甲。</summary>
        [AssetId(nameof(NegArmor))] public static BaseStatAsset NegArmor { get; private set; }

        /// <summary>抵抗阳属性伤害的护甲。</summary>
        [AssetId(nameof(PosArmor))] public static BaseStatAsset PosArmor { get; private set; }

        /// <summary>抵抗混沌属性伤害的护甲。</summary>
        [AssetId(nameof(EntropyArmor))] public static BaseStatAsset EntropyArmor { get; private set; }

        /// <summary>提高金属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(IronMaster))] public static BaseStatAsset IronMaster { get; private set; }

        /// <summary>提高木属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(WoodMaster))] public static BaseStatAsset WoodMaster { get; private set; }

        /// <summary>提高水属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(WaterMaster))] public static BaseStatAsset WaterMaster { get; private set; }

        /// <summary>提高火属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(FireMaster))] public static BaseStatAsset FireMaster { get; private set; }

        /// <summary>提高土属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(EarthMaster))] public static BaseStatAsset EarthMaster { get; private set; }

        /// <summary>提高阴属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(NegMaster))] public static BaseStatAsset NegMaster { get; private set; }

        /// <summary>提高阳属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(PosMaster))] public static BaseStatAsset PosMaster { get; private set; }

        /// <summary>提高混沌属性攻击效果的掌握属性。</summary>
        [AssetId(nameof(EntropyMaster))] public static BaseStatAsset EntropyMaster { get; private set; }

        /// <summary>单位每次生命恢复结算能够恢复的生命值。</summary>
        [AssetId(nameof(HealthRegen))] public static BaseStatAsset HealthRegen { get; private set; }

        /// <summary>单位能够容纳的最大魂量。</summary>
        [AssetId(nameof(MaxSoul))] public static BaseStatAsset MaxSoul { get; private set; }

        /// <summary>单位能够容纳的最大气运。</summary>
        [AssetId(nameof(MaxQiyun))] public static BaseStatAsset MaxQiyun { get; private set; }

        /// <summary>单位用于控制法器及计算分念容量的神识。</summary>
        [AssetId(nameof(DivineSense))] public static BaseStatAsset DivineSense { get; private set; }

        /// <summary>标记宗门招募必须由师父引荐的统计标签。</summary>
        public const string TagRecruitRequiresMasterIntroduction = "sect_recruit_requires_master_introduction";

        /// <summary>标记宗门允许弟子参与整理藏经阁的统计标签。</summary>
        public const string TagAllowDiscipleOrganizeScripture = "sect_allow_disciple_organize_scripture";

        /// <summary>宗门招募距离相对基础值的倍率修正。</summary>
        [AssetId(nameof(RecruitRangeModifier))] public static BaseStatAsset RecruitRangeModifier { get; private set; }

        /// <summary>宗门可招募境界相对宗主境界上限的平铺加成。</summary>
        [AssetId(nameof(RecruitMaxLevelBonus))] public static BaseStatAsset RecruitMaxLevelBonus { get; private set; }

        /// <summary>境界因素在宗门招募评分中的权重倍率修正。</summary>
        [AssetId(nameof(RecruitRealmScoreModifier))] public static BaseStatAsset RecruitRealmScoreModifier { get; private set; }

        /// <summary>师父接纳弟子所需意愿门槛的倍率修正。</summary>
        [AssetId(nameof(MasterWillingnessThresholdModifier))] public static BaseStatAsset MasterWillingnessThresholdModifier { get; private set; }

        /// <summary>宗门成员收徒容量的倍率修正。</summary>
        [AssetId(nameof(MasterApprenticeCapacityModifier))] public static BaseStatAsset MasterApprenticeCapacityModifier { get; private set; }

        /// <summary>宗门教学行为产生收益的倍率修正。</summary>
        [AssetId(nameof(TeachingGainModifier))] public static BaseStatAsset TeachingGainModifier { get; private set; }

        /// <summary>单次宗门讲法最大听众数量的平铺加成。</summary>
        [AssetId(nameof(LectureMaxAudienceBonus))] public static BaseStatAsset LectureMaxAudienceBonus { get; private set; }

        /// <summary>境界在宗门人事评分中的权重倍率修正。</summary>
        [AssetId(nameof(PersonnelRealmScoreModifier))] public static BaseStatAsset PersonnelRealmScoreModifier { get; private set; }

        /// <summary>入宗资历在宗门人事评分中的权重倍率修正。</summary>
        [AssetId(nameof(PersonnelTenureScoreModifier))] public static BaseStatAsset PersonnelTenureScoreModifier { get; private set; }

        /// <summary>宗门贡献在人事评分中的权重倍率修正。</summary>
        [AssetId(nameof(PersonnelContributionScoreModifier))] public static BaseStatAsset PersonnelContributionScoreModifier { get; private set; }

        /// <summary>成员晋升所需人事评分门槛的倍率修正。</summary>
        [AssetId(nameof(PromotionScoreThresholdModifier))] public static BaseStatAsset PromotionScoreThresholdModifier { get; private set; }

        /// <summary>执事职位数量的倍率修正。</summary>
        [AssetId(nameof(DeaconSlotModifier))] public static BaseStatAsset DeaconSlotModifier { get; private set; }

        /// <summary>长老职位数量的倍率修正。</summary>
        [AssetId(nameof(ElderSlotModifier))] public static BaseStatAsset ElderSlotModifier { get; private set; }

        /// <summary>晋升执事所需评分门槛的倍率修正。</summary>
        [AssetId(nameof(DeaconThresholdModifier))] public static BaseStatAsset DeaconThresholdModifier { get; private set; }

        /// <summary>晋升长老所需评分门槛的倍率修正。</summary>
        [AssetId(nameof(ElderThresholdModifier))] public static BaseStatAsset ElderThresholdModifier { get; private set; }

        /// <summary>研读宗门正统功法时的候选评分倍率修正。</summary>
        [AssetId(nameof(DoctrineCultibookStudyModifier))] public static BaseStatAsset DoctrineCultibookStudyModifier { get; private set; }

        /// <summary>研读非宗门正统功法时的候选评分倍率修正。</summary>
        [AssetId(nameof(OtherCultibookStudyModifier))] public static BaseStatAsset OtherCultibookStudyModifier { get; private set; }

        /// <summary>研读技能书时的候选评分倍率修正。</summary>
        [AssetId(nameof(SkillbookStudyModifier))] public static BaseStatAsset SkillbookStudyModifier { get; private set; }

        /// <summary>研读丹方书时的候选评分倍率修正。</summary>
        [AssetId(nameof(ElixirbookStudyModifier))] public static BaseStatAsset ElixirbookStudyModifier { get; private set; }

        /// <summary>越权研读宗门藏书所需贡献成本的倍率修正。</summary>
        [AssetId(nameof(OutOfPermissionReadCostModifier))] public static BaseStatAsset OutOfPermissionReadCostModifier { get; private set; }

        /// <summary>选择宗门正统功法进行讲法时的权重倍率修正。</summary>
        [AssetId(nameof(DoctrineLectureWeightModifier))] public static BaseStatAsset DoctrineLectureWeightModifier { get; private set; }

        /// <summary>宗门杂务进入事务候选池时的权重倍率修正。</summary>
        [AssetId(nameof(ChoreAffairWeightModifier))] public static BaseStatAsset ChoreAffairWeightModifier { get; private set; }

        /// <summary>整理藏经阁进入事务候选池时的权重倍率修正。</summary>
        [AssetId(nameof(OrganizeScriptureAffairWeightModifier))] public static BaseStatAsset OrganizeScriptureAffairWeightModifier { get; private set; }

        /// <summary>宗门讲法进入事务候选池时的权重倍率修正。</summary>
        [AssetId(nameof(LectureAffairWeightModifier))] public static BaseStatAsset LectureAffairWeightModifier { get; private set; }

        /// <summary>完成宗门杂务获得贡献的倍率修正。</summary>
        [AssetId(nameof(ChoreContributionModifier))] public static BaseStatAsset ChoreContributionModifier { get; private set; }

        /// <summary>完成藏经阁整理获得贡献的倍率修正。</summary>
        [AssetId(nameof(OrganizeScriptureContributionModifier))] public static BaseStatAsset OrganizeScriptureContributionModifier { get; private set; }

        /// <summary>完成宗门讲法获得贡献的倍率修正。</summary>
        [AssetId(nameof(LectureContributionModifier))] public static BaseStatAsset LectureContributionModifier { get; private set; }

        /// <summary>参与宗门建筑修建获得贡献的倍率修正。</summary>
        [AssetId(nameof(BuildContributionModifier))] public static BaseStatAsset BuildContributionModifier { get; private set; }

        /// <summary>贡献或誊写宗门典籍获得贡献的倍率修正。</summary>
        [AssetId(nameof(WriteScriptureContributionModifier))] public static BaseStatAsset WriteScriptureContributionModifier { get; private set; }

        /// <summary>单位主动选择宗门研读工作的概率倍率修正。</summary>
        [AssetId(nameof(SectStudyJobChanceModifier))] public static BaseStatAsset SectStudyJobChanceModifier { get; private set; }

        /// <summary>单位主动选择宗门事务工作的概率倍率修正。</summary>
        [AssetId(nameof(SectAffairJobChanceModifier))] public static BaseStatAsset SectAffairJobChanceModifier { get; private set; }

        /// <summary>宗门藏宝阁能够容纳的物品容量。</summary>
        [AssetId(nameof(TreasureCapacity))] public static BaseStatAsset TreasureCapacity { get; private set; }

        /// <summary>按元素顺序排列的元素护甲属性 ID 集合。</summary>
        public static ReadOnlyCollection<string> ArmorStats  { get; private set; }

        /// <summary>按元素顺序排列的元素掌握属性 ID 集合。</summary>
        public static ReadOnlyCollection<string> MasterStats { get; private set; }
        private static readonly HashSet<string> SectStatIds = new()
        {
            nameof(RecruitRangeModifier),
            nameof(RecruitMaxLevelBonus),
            nameof(RecruitRealmScoreModifier),
            nameof(MasterWillingnessThresholdModifier),
            nameof(MasterApprenticeCapacityModifier),
            nameof(TeachingGainModifier),
            nameof(LectureMaxAudienceBonus),
            nameof(PersonnelRealmScoreModifier),
            nameof(PersonnelTenureScoreModifier),
            nameof(PersonnelContributionScoreModifier),
            nameof(PromotionScoreThresholdModifier),
            nameof(DeaconSlotModifier),
            nameof(ElderSlotModifier),
            nameof(DeaconThresholdModifier),
            nameof(ElderThresholdModifier),
            nameof(DoctrineCultibookStudyModifier),
            nameof(OtherCultibookStudyModifier),
            nameof(SkillbookStudyModifier),
            nameof(ElixirbookStudyModifier),
            nameof(OutOfPermissionReadCostModifier),
            nameof(DoctrineLectureWeightModifier),
            nameof(ChoreAffairWeightModifier),
            nameof(OrganizeScriptureAffairWeightModifier),
            nameof(LectureAffairWeightModifier),
            nameof(ChoreContributionModifier),
            nameof(OrganizeScriptureContributionModifier),
            nameof(LectureContributionModifier),
            nameof(BuildContributionModifier),
            nameof(WriteScriptureContributionModifier),
            nameof(SectStudyJobChanceModifier),
            nameof(SectAffairJobChanceModifier),
            nameof(TreasureCapacity)
        };
        private static Dictionary<string, string> _statsToModStats = new();

        /// <summary>基础属性 ID 到对应乘算修正属性 ID 的只读映射。</summary>
        public static ReadOnlyDictionary<string, string> StatsToModStats { get; private set; } =
            new(_statsToModStats);
        protected override bool AutoRegisterAssets() => true;
        protected override void OnInit()
        {
            Armor.normalize = false;
            DivineSense.icon = $"cultiway/icons/stats/{nameof(DivineSense)}";
            IronArmor.icon = $"cultiway/icons/stats/{nameof(IronArmor)}";
            WoodArmor.icon = $"cultiway/icons/stats/{nameof(WoodArmor)}";
            WaterArmor.icon = $"cultiway/icons/stats/{nameof(WaterArmor)}";
            FireArmor.icon = $"cultiway/icons/stats/{nameof(FireArmor)}";
            EarthArmor.icon = $"cultiway/icons/stats/{nameof(EarthArmor)}";
            NegArmor.icon = $"cultiway/icons/stats/{nameof(NegArmor)}";
            PosArmor.icon = $"cultiway/icons/stats/{nameof(PosArmor)}";
            EntropyArmor.icon = $"cultiway/icons/stats/{nameof(EntropyArmor)}";
            IronMaster.icon = $"cultiway/icons/stats/{nameof(IronMaster)}";
            WoodMaster.icon = $"cultiway/icons/stats/{nameof(WoodMaster)}";
            WaterMaster.icon = $"cultiway/icons/stats/{nameof(WaterMaster)}";
            FireMaster.icon = $"cultiway/icons/stats/{nameof(FireMaster)}";
            EarthMaster.icon = $"cultiway/icons/stats/{nameof(EarthMaster)}";
            NegMaster.icon = $"cultiway/icons/stats/{nameof(NegMaster)}";
            PosMaster.icon = $"cultiway/icons/stats/{nameof(PosMaster)}";
            EntropyMaster.icon = $"cultiway/icons/stats/{nameof(EntropyMaster)}";

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

            ArmorStats = new ReadOnlyCollection<string>(new List<string>
            {
                IronArmor.id,
                WoodArmor.id,
                WaterArmor.id,
                FireArmor.id,
                EarthArmor.id,
                NegArmor.id,
                PosArmor.id,
                EntropyArmor.id
            });
            MasterStats = new ReadOnlyCollection<string>(new List<string>
            {
                IronMaster.id,
                WoodMaster.id,
                WaterMaster.id,
                FireMaster.id,
                EarthMaster.id,
                NegMaster.id,
                PosMaster.id,
                EntropyMaster.id
            });
        }

        protected override BaseStatAsset Add(BaseStatAsset asset)
        {
            asset.translation_key = asset.id;
            if (asset.multiplier || IsSectStat(asset.id)) return base.Add(asset);
            var mod_asset = Add(new BaseStatAsset
            {
                id = $"Mod{asset.id}",
                main_stat_to_multiply = asset.id,
                multiplier = true,
                show_as_percents = true,
                tooltip_multiply_for_visual_number = 100
            });
            _statsToModStats[asset.id] = mod_asset.id;
            return base.Add(asset);
        }

        /// <summary>
        /// 判断属性是否属于宗门聚合属性；宗门建筑只向宗门提供这一类属性。
        /// </summary>
        public static bool IsSectStat(string statId)
        {
            return statId != null && SectStatIds.Contains(statId);
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
}
