using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 师徒关系类型资产集合。
/// </summary>
public class MasterApprenticeTypes : ExtendLibrary<MasterApprenticeTypeAsset, MasterApprenticeTypes>
{
    private const string SectRolePrefix = "Cultiway.Sect.Role";

    /// <summary>
    /// 记名弟子，最低层级师徒关系。
    /// </summary>
    public static MasterApprenticeTypeAsset Nominal { get; private set; }

    /// <summary>
    /// 入室弟子，正式师徒关系。
    /// </summary>
    public static MasterApprenticeTypeAsset Formal { get; private set; }

    /// <summary>
    /// 亲传弟子，高亲密度师徒关系。
    /// </summary>
    public static MasterApprenticeTypeAsset Direct { get; private set; }

    /// <summary>
    /// 衣钵传人，带继任标记的最高层级师徒关系。
    /// </summary>
    public static MasterApprenticeTypeAsset Successor { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.MasterApprentice.Type";

    protected override void OnInit()
    {
        Setup(Nominal, 0, 0f, 0.5f, false, RoleId("OuterDisciple"), null);
        Setup(Formal, 1, 30f, 0.8f, false, RoleId("InnerDisciple"), null);
        Setup(Direct, 2, 60f, 1.0f, false, RoleId("DirectDisciple"), null);
        Setup(Successor, 3, 90f, 1.2f, true, RoleId("DirectDisciple"), RoleId("Successor"));
    }

    private static void Setup(
        MasterApprenticeTypeAsset asset,
        int rank,
        float minIntimacy,
        float teachEfficiencyMultiplier,
        bool requiresSuccessorFlag,
        string sectGradeRoleId,
        string sectTitleRoleId)
    {
        asset.rank = rank;
        asset.minIntimacy = minIntimacy;
        asset.teachEfficiencyMultiplier = teachEfficiencyMultiplier;
        asset.requiresSuccessorFlag = requiresSuccessorFlag;
        asset.nameKey = asset.id;
        asset.descriptionKey = $"{asset.id}.Info";
        asset.sectGradeRoleId = sectGradeRoleId;
        asset.sectTitleRoleId = sectTitleRoleId;
    }

    private static string RoleId(string roleName)
    {
        return $"{SectRolePrefix}.{roleName}";
    }
}
