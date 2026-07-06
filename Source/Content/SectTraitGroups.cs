using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门特质分组集合。
/// </summary>
public class SectTraitGroups : ExtendLibrary<SectTraitGroupAsset, SectTraitGroups>
{
    /// <summary>
    /// 驻地策略分组。
    /// </summary>
    public static SectTraitGroupAsset ResidenceStrategy { get; private set; }

    /// <summary>
    /// 入门制度分组。
    /// </summary>
    public static SectTraitGroupAsset EntranceSystem { get; private set; }

    /// <summary>
    /// 师承制度分组。
    /// </summary>
    public static SectTraitGroupAsset MasterSystem { get; private set; }

    /// <summary>
    /// 晋升考核分组。
    /// </summary>
    public static SectTraitGroupAsset PromotionEvaluation { get; private set; }

    /// <summary>
    /// 职司任命分组。
    /// </summary>
    public static SectTraitGroupAsset OfficeAppointment { get; private set; }

    /// <summary>
    /// 传承方向分组。
    /// </summary>
    public static SectTraitGroupAsset TransmissionDirection { get; private set; }

    /// <summary>
    /// 宗门事务分组。
    /// </summary>
    public static SectTraitGroupAsset SectAffairPolicy { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.TraitGroup";

    protected override void OnInit()
    {
        ResidenceStrategy.name = "sect_trait_group_residence_strategy";
        ResidenceStrategy.color = "#8fd17f";
        EntranceSystem.name = "sect_trait_group_entrance_system";
        EntranceSystem.color = "#d7c274";
        MasterSystem.name = "sect_trait_group_master_system";
        MasterSystem.color = "#86c7d9";
        PromotionEvaluation.name = "sect_trait_group_promotion_evaluation";
        PromotionEvaluation.color = "#e3a262";
        OfficeAppointment.name = "sect_trait_group_office_appointment";
        OfficeAppointment.color = "#c59bdb";
        TransmissionDirection.name = "sect_trait_group_transmission_direction";
        TransmissionDirection.color = "#75d7c1";
        SectAffairPolicy.name = "sect_trait_group_sect_affair_policy";
        SectAffairPolicy.color = "#d68b8b";
    }
}
