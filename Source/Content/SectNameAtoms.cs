using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Content.Sects;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

/// <summary>
/// 宗门命名原子集合。
/// </summary>
[Dependency(typeof(SectTraits), typeof(CultivateMethods), typeof(CultivationSemantics))]
public sealed class SectNameAtoms : ExtendLibrary<SectNameAtomAsset, SectNameAtoms>
{
    private static readonly string[] ElementPatterns = ["{element}{suffix}"];
    private static readonly string[] CultivationPatterns =
        ["{doctrine}{suffix}", "{element}{theme}{suffix}", "{doctrine_short}{theme}{suffix}"];
    private static readonly string[] ResidencePatterns =
        ["{residence}{suffix}", "{residence}{doctrine_short}{suffix}", "{theme}{suffix}", "{residence_full}"];
    private static readonly string[] PolicyPatterns =
        ["{theme}{suffix}", "{doctrine_short}{theme}{suffix}", "{residence}{theme}{suffix}", "{element}{theme}{suffix}"];
    private static readonly string[] GenericPatterns =
        ["{doctrine}{suffix}", "{residence}{suffix}", "{element}{suffix}", "{theme}{suffix}"];

    public static SectNameAtomAsset Iron { get; private set; }
    public static SectNameAtomAsset Wood { get; private set; }
    public static SectNameAtomAsset Water { get; private set; }
    public static SectNameAtomAsset Fire { get; private set; }
    public static SectNameAtomAsset Earth { get; private set; }
    public static SectNameAtomAsset Yin { get; private set; }
    public static SectNameAtomAsset Yang { get; private set; }
    public static SectNameAtomAsset Entropy { get; private set; }

    public static SectNameAtomAsset StandardPath { get; private set; }
    public static SectNameAtomAsset NaturalPath { get; private set; }
    public static SectNameAtomAsset WaterPath { get; private set; }
    public static SectNameAtomAsset BattlePath { get; private set; }
    public static SectNameAtomAsset SlaughterPath { get; private set; }
    public static SectNameAtomAsset FortunePath { get; private set; }
    public static SectNameAtomAsset SwordPath { get; private set; }

    public static SectNameAtomAsset SecludedResidence { get; private set; }
    public static SectNameAtomAsset CityResidence { get; private set; }
    public static SectNameAtomAsset ResourceResidence { get; private set; }
    public static SectNameAtomAsset TerritorialResidence { get; private set; }

    public static SectNameAtomAsset SelectiveAdmissionPolicy { get; private set; }
    public static SectNameAtomAsset MasterIntroducedPolicy { get; private set; }
    public static SectNameAtomAsset CityAttachedRecruitmentPolicy { get; private set; }
    public static SectNameAtomAsset StrictLineagePolicy { get; private set; }
    public static SectNameAtomAsset LooseTransmissionPolicy { get; private set; }
    public static SectNameAtomAsset RealmSupremacyPolicy { get; private set; }
    public static SectNameAtomAsset MeritFirstPolicy { get; private set; }
    public static SectNameAtomAsset SeniorityPolicy { get; private set; }
    public static SectNameAtomAsset ExceptionalPromotionPolicy { get; private set; }
    public static SectNameAtomAsset DeaconGovernancePolicy { get; private set; }
    public static SectNameAtomAsset ElderAuthorityPolicy { get; private set; }
    public static SectNameAtomAsset DiscipleSelfGovernancePolicy { get; private set; }
    public static SectNameAtomAsset StrictHierarchyPolicy { get; private set; }
    public static SectNameAtomAsset OrthodoxPolicy { get; private set; }
    public static SectNameAtomAsset HundredSchoolsPolicy { get; private set; }
    public static SectNameAtomAsset SkillResearchPolicy { get; private set; }
    public static SectNameAtomAsset ElixirPolicy { get; private set; }
    public static SectNameAtomAsset StrictScripturePolicy { get; private set; }
    public static SectNameAtomAsset SingleLineagePolicy { get; private set; }
    public static SectNameAtomAsset CollectiveInstructionPolicy { get; private set; }
    public static SectNameAtomAsset LecturePolicy { get; private set; }
    public static SectNameAtomAsset QuietPolicy { get; private set; }
    public static SectNameAtomAsset OpenGatePolicy { get; private set; }
    public static SectNameAtomAsset ConstructionPolicy { get; private set; }
    public static SectNameAtomAsset OrderlyChoresPolicy { get; private set; }
    public static SectNameAtomAsset RewardAndPunishmentPolicy { get; private set; }
    public static SectNameAtomAsset TransmissionRewardPolicy { get; private set; }
    public static SectNameAtomAsset Generic { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SectNameAtom";

    protected override void OnInit()
    {
        SetElement(Iron, ElementIndex.Iron, ["庚金", "金阙", "玄锋"]);
        SetElement(Wood, ElementIndex.Wood, ["青木", "长青", "苍灵"]);
        SetElement(Water, ElementIndex.Water, ["玄水", "沧浪", "寒泉"]);
        SetElement(Fire, ElementIndex.Fire, ["离火", "赤炎", "焚阳"]);
        SetElement(Earth, ElementIndex.Earth, ["厚土", "坤岳", "镇山"]);
        SetElement(Yin, ElementIndex.Neg, ["玄阴", "幽冥", "太阴"]);
        SetElement(Yang, ElementIndex.Pos, ["纯阳", "曜灵", "明光"]);
        SetElement(Entropy, ElementIndex.Entropy, ["混沌", "归墟", "浊玄"]);

        Set(StandardPath, SectNameAtomCategory.Cultivation, ["归元", "周天", "太玄"],
            ["宗", "门", "派"], CultivationPatterns, 10,
            context => context.CultivateMethodId == CultivateMethods.Standard.id ? 20f : 0f);
        Set(NaturalPath, SectNameAtomCategory.Cultivation, ["天象", "地脉", "万化"],
            ["宗", "门", "宫", "谷"], CultivationPatterns, 30,
            context => IsEnvironmentalMethod(context.CultivateMethodId) ? 100f : 0f);
        Set(WaterPath, SectNameAtomCategory.Cultivation, ["沧溟", "玄潮", "寒渊"],
            ["宫", "门", "谷", "宗"], CultivationPatterns, 30,
            context => context.CultivateMethodId == CultivateMethods.WaterMeditation.id ? 120f : 0f);
        Set(BattlePath, SectNameAtomCategory.Cultivation, ["斗战", "破军", "百炼"],
            ["宗", "门", "派", "山庄"], CultivationPatterns, 30,
            context => context.CultivateMethodId == CultivateMethods.BattleCultivate.id ? 120f : 0f);
        Set(SlaughterPath, SectNameAtomCategory.Cultivation, ["血煞", "幽冥", "修罗"],
            ["门", "宗", "宫"], CultivationPatterns, 35,
            context => context.CultivateMethodId == CultivateMethods.KillAbsorb.id ? 120f : 0f);
        Set(FortunePath, SectNameAtomCategory.Cultivation, ["皇极", "山河", "紫气"],
            ["府", "宫", "宗"], CultivationPatterns, 30,
            context => context.CultivateMethodId == CultivateMethods.KingdomFortune.id ? 120f : 0f);
        Set(SwordPath, SectNameAtomCategory.Cultivation, ["剑心", "玄锋", "天剑"],
            ["剑宗", "剑门", "剑派", "山庄"], CultivationPatterns, 40,
            context => context.HasDoctrineSemantic(CultivationSemantics.Path.Sword) ? 130f : 0f);

        Set(SecludedResidence, SectNameAtomCategory.Residence, ["隐真", "栖霞", "云隐"],
            ["宗", "门", "宫", "观", "谷", "洞"], ResidencePatterns, 30,
            context => context.HasTrait(SectTraits.SecludedMountainGate) ? 100f : 0f);
        Set(CityResidence, SectNameAtomCategory.Residence, ["同尘", "玄都", "济世"],
            ["府", "堂", "会", "院", "书院"], ResidencePatterns, 30,
            context => context.HasTrait(SectTraits.CityAttachedBranch) ? 100f : 0f);
        Set(ResourceResidence, SectNameAtomCategory.Residence, ["聚灵", "蕴真", "灵泉"],
            ["宗", "门", "宫", "谷"], ResidencePatterns, 30,
            context => context.HasTrait(SectTraits.ResourceSeekingGate) ? 100f : 0f);
        Set(TerritorialResidence, SectNameAtomCategory.Residence, ["开山", "镇岳", "山河"],
            ["宗", "门", "派", "宫", "山庄"], ResidencePatterns, 30,
            context => context.HasTrait(SectTraits.TerritorialGate) ? 100f : 0f);

        Set(SelectiveAdmissionPolicy, SectNameAtomCategory.Policy, ["择贤", "英华", "凌云"],
            ["宗", "院", "门"], PolicyPatterns, 15,
            context => context.HasTrait(SectTraits.SelectiveAdmission) ? 50f : 0f);
        Set(MasterIntroducedPolicy, SectNameAtomCategory.Policy, ["传灯", "引真", "师承"],
            ["门", "宗", "院"], PolicyPatterns, 15,
            context => context.HasTrait(SectTraits.MasterIntroducedAdmission) ? 50f : 0f);
        Set(CityAttachedRecruitmentPolicy, SectNameAtomCategory.Policy, ["会仙", "同尘", "济世"],
            ["会", "府", "院", "堂"], PolicyPatterns, 15,
            context => context.HasTrait(SectTraits.CityAttachedRecruitment) ? 50f : 0f);
        Set(OrthodoxPolicy, SectNameAtomCategory.Policy, ["正一", "太一", "祖庭"],
            ["宗", "门", "派", "宫"], PolicyPatterns, 40,
            context => context.HasTrait(SectTraits.DoctrineOrthodoxy) ? 120f : 0f);
        Set(HundredSchoolsPolicy, SectNameAtomCategory.Policy, ["百家", "博玄", "万法"],
            ["书院", "学宫", "会", "院"], PolicyPatterns, 40,
            context => context.HasTrait(SectTraits.HundredSchools) ? 120f : 0f);
        Set(SkillResearchPolicy, SectNameAtomCategory.Policy, ["衍法", "万象", "神霄"],
            ["宫", "门", "派", "堂"], PolicyPatterns, 40,
            context => context.HasTrait(SectTraits.SkillResearch) ? 120f : 0f);
        Set(ElixirPolicy, SectNameAtomCategory.Policy, ["丹鼎", "药王", "金炉"],
            ["宗", "谷", "院"], PolicyPatterns, 40,
            context => context.HasTrait(SectTraits.ElixirInheritance) ? 120f : 0f);
        Set(StrictScripturePolicy, SectNameAtomCategory.Policy, ["藏真", "秘法", "传经"],
            ["阁", "宗", "门", "宫"], PolicyPatterns, 40,
            context => context.HasTrait(SectTraits.StrictScripture) ? 120f : 0f);
        Set(StrictLineagePolicy, SectNameAtomCategory.Policy, ["正脉", "承真", "嫡传"],
            ["宗", "门", "派"], PolicyPatterns, 30,
            context => context.HasTrait(SectTraits.StrictLineage) ? 80f : 0f);
        Set(LooseTransmissionPolicy, SectNameAtomCategory.Policy, ["广传", "弘道", "普济"],
            ["门", "院", "会", "书院"], PolicyPatterns, 30,
            context => context.HasTrait(SectTraits.LooseTransmission) ? 80f : 0f);
        Set(SingleLineagePolicy, SectNameAtomCategory.Policy, ["一脉", "太一", "独秀"],
            ["宗", "门", "派"], PolicyPatterns, 25,
            context => context.HasTrait(SectTraits.SingleLineage) ? 80f : 0f);
        Set(CollectiveInstructionPolicy, SectNameAtomCategory.Policy, ["广闻", "讲玄", "传道"],
            ["书院", "学宫", "院", "会"], PolicyPatterns, 25,
            context => context.HasTrait(SectTraits.CollectiveInstruction) ? 80f : 0f);
        Set(RealmSupremacyPolicy, SectNameAtomCategory.Policy, ["凌霄", "登真", "天阶"],
            ["宗", "宫", "门"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.RealmSupremacy) ? 65f : 0f);
        Set(MeritFirstPolicy, SectNameAtomCategory.Policy, ["崇功", "勋贤", "策勋"],
            ["宗", "府", "堂"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.MeritFirst) ? 65f : 0f);
        Set(SeniorityPolicy, SectNameAtomCategory.Policy, ["承序", "尊古", "长序"],
            ["宗", "门", "院"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.SeniorityOrder) ? 65f : 0f);
        Set(ExceptionalPromotionPolicy, SectNameAtomCategory.Policy, ["拔萃", "擢英", "青云"],
            ["门", "院", "会"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.ExceptionalPromotion) ? 65f : 0f);
        Set(DeaconGovernancePolicy, SectNameAtomCategory.Policy, ["司务", "理事", "执律"],
            ["堂", "府", "院"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.DeaconGovernance) ? 70f : 0f);
        Set(ElderAuthorityPolicy, SectNameAtomCategory.Policy, ["耆宿", "尊老", "长议"],
            ["宗", "宫", "院"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.ElderAuthority) ? 70f : 0f);
        Set(DiscipleSelfGovernancePolicy, SectNameAtomCategory.Policy, ["共议", "同门", "自治"],
            ["会", "院", "门"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.DiscipleSelfGovernance) ? 70f : 0f);
        Set(StrictHierarchyPolicy, SectNameAtomCategory.Policy, ["九阶", "森严", "天序"],
            ["宫", "宗", "门"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.StrictHierarchy) ? 70f : 0f);
        Set(LecturePolicy, SectNameAtomCategory.Policy, ["讲玄", "传道", "弘法"],
            ["书院", "学宫", "堂", "院"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.LectureCulture) ? 65f : 0f);
        Set(QuietPolicy, SectNameAtomCategory.Policy, ["清微", "守静", "坐忘"],
            ["观", "宗", "门", "宫"], PolicyPatterns, 20,
            context => context.HasTrait(SectTraits.QuietCultivation) ? 65f : 0f);
        Set(OpenGatePolicy, SectNameAtomCategory.Policy, ["广缘", "开明", "四海"],
            ["门", "会", "院", "宗"], PolicyPatterns, 10,
            context => context.HasTrait(SectTraits.OpenGate) ? 45f : 0f);
        Set(ConstructionPolicy, SectNameAtomCategory.Policy, ["天工", "营造", "开山"],
            ["堂", "院", "山庄", "门"], PolicyPatterns, 10,
            context => context.HasTrait(SectTraits.ConstructionZeal) ? 45f : 0f);
        Set(OrderlyChoresPolicy, SectNameAtomCategory.Policy, ["百工", "勤务", "井然"],
            ["堂", "院", "府"], PolicyPatterns, 15,
            context => context.HasTrait(SectTraits.OrderlyChores) ? 60f : 0f);
        Set(RewardAndPunishmentPolicy, SectNameAtomCategory.Policy, ["明赏", "赏善", "刑名"],
            ["堂", "府", "宗"], PolicyPatterns, 15,
            context => context.HasTrait(SectTraits.RewardAndPunishment) ? 60f : 0f);
        Set(TransmissionRewardPolicy, SectNameAtomCategory.Policy, ["传薪", "薪火", "弘经"],
            ["宗", "院", "书院"], PolicyPatterns, 15,
            context => context.HasTrait(SectTraits.TransmissionReward) ? 60f : 0f);

        Set(Generic, SectNameAtomCategory.Generic, ["灵霄", "紫府", "玉虚", "归真"],
            ["宗", "门", "派"], GenericPatterns, 0, _ => 1f);
    }

    private static void SetElement(SectNameAtomAsset atom, int element, string[] stems)
    {
        Set(atom, SectNameAtomCategory.Element, stems, ["宗", "门", "派"], ElementPatterns, 20,
            context => context.PrimaryElement == element ? 100f : 0f);
    }

    /// <summary>判断宗门主修方式是否使用统一自然环境修炼规则。</summary>
    private static bool IsEnvironmentalMethod(string methodId)
    {
        return Libraries.Manager.CultivateMethodLibrary.get(methodId)?.EnvironmentRule != null;
    }

    private static void Set(
        SectNameAtomAsset atom,
        SectNameAtomCategory category,
        string[] stems,
        string[] suffixes,
        string[] patterns,
        int priority,
        Func<SectNamingContext, float> score)
    {
        atom.category = category;
        atom.name_stems = stems;
        atom.suffixes = suffixes;
        atom.patterns = patterns;
        atom.priority = priority;
        atom.ScoreContext = score;
    }
}
