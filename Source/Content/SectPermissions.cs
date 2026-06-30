using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门权限资产集合。
/// </summary>
public class SectPermissions : ExtendLibrary<SectPermissionAsset, SectPermissions>
{
    /// <summary>
    /// 允许研读基础藏书。
    /// </summary>
    public static SectPermissionAsset ReadBasicScripture { get; private set; }

    /// <summary>
    /// 允许研读核心藏书。
    /// </summary>
    public static SectPermissionAsset ReadCoreScripture { get; private set; }

    /// <summary>
    /// 允许研读高阶藏书。
    /// </summary>
    public static SectPermissionAsset ReadHighScripture { get; private set; }

    /// <summary>
    /// 允许向宗门贡献典籍。
    /// </summary>
    public static SectPermissionAsset WriteScripture { get; private set; }

    /// <summary>
    /// 允许招揽散修加入宗门。
    /// </summary>
    public static SectPermissionAsset RecruitMember { get; private set; }

    /// <summary>
    /// 允许将自己的徒弟带入宗门。
    /// </summary>
    public static SectPermissionAsset BringApprenticeToSect { get; private set; }

    /// <summary>
    /// 允许执行宗门杂务。
    /// </summary>
    public static SectPermissionAsset DoSectChore { get; private set; }

    /// <summary>
    /// 允许执行宗门人事评定。
    /// </summary>
    public static SectPermissionAsset EvaluatePersonnel { get; private set; }

    /// <summary>
    /// 允许任免宗门成员职司。
    /// </summary>
    public static SectPermissionAsset PromoteMember { get; private set; }

    /// <summary>
    /// 允许管理藏经阁。
    /// </summary>
    public static SectPermissionAsset ManageScripture { get; private set; }

    /// <summary>
    /// 宗门最高管理权限。
    /// </summary>
    public static SectPermissionAsset ManageSect { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.Permission";

    protected override void OnInit()
    {
        Setup(ReadBasicScripture);
        Setup(ReadCoreScripture);
        Setup(ReadHighScripture);
        Setup(WriteScripture);
        Setup(RecruitMember);
        Setup(BringApprenticeToSect);
        Setup(DoSectChore);
        Setup(EvaluatePersonnel);
        Setup(PromoteMember);
        Setup(ManageScripture);
        Setup(ManageSect);
    }

    private static void Setup(SectPermissionAsset asset)
    {
        asset.nameKey = asset.id;
        asset.descriptionKey = $"{asset.id}.Info";
    }
}
