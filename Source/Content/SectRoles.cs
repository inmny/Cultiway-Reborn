using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门角色资产集合，定义门阶、职司和头衔的默认配置。
/// </summary>
[Dependency(typeof(SectPermissions))]
public class SectRoles : ExtendLibrary<SectRoleAsset, SectRoles>
{
    /// <summary>
    /// 无门阶，门阶槽位的默认角色。
    /// </summary>
    public static SectRoleAsset NoGrade { get; private set; }

    /// <summary>
    /// 外门弟子，普通门人入宗时的基础门阶。
    /// </summary>
    public static SectRoleAsset OuterDisciple { get; private set; }

    /// <summary>
    /// 内门弟子，高于外门弟子的门阶。
    /// </summary>
    public static SectRoleAsset InnerDisciple { get; private set; }

    /// <summary>
    /// 亲传弟子，普通门阶晋升的最高角色。
    /// </summary>
    public static SectRoleAsset DirectDisciple { get; private set; }

    /// <summary>
    /// 无职司，职司槽位的默认角色。
    /// </summary>
    public static SectRoleAsset NoOffice { get; private set; }

    /// <summary>
    /// 执事，预留的基础管理职司。
    /// </summary>
    public static SectRoleAsset Deacon { get; private set; }

    /// <summary>
    /// 长老，负责招揽门人和评定人事的高层职司。
    /// </summary>
    public static SectRoleAsset Elder { get; private set; }

    /// <summary>
    /// 掌门，宗门最高职司。
    /// </summary>
    public static SectRoleAsset Leader { get; private set; }

    /// <summary>
    /// 无头衔，头衔槽位的默认角色。
    /// </summary>
    public static SectRoleAsset NoTitle { get; private set; }

    /// <summary>
    /// 衣钵传人，掌门继任的优先头衔。
    /// </summary>
    public static SectRoleAsset Successor { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.Role";

    protected override void OnInit()
    {
        SetupGrade(NoGrade, 0, 0, 0, true, false);
        SetupGrade(
            OuterDisciple,
            10,
            10,
            0,
            false,
            true,
            SectPermissions.ReadBasicScripture,
            SectPermissions.WriteScripture);
        SetupGrade(
            InnerDisciple,
            20,
            20,
            200,
            false,
            true,
            SectPermissions.ReadBasicScripture,
            SectPermissions.ReadCoreScripture,
            SectPermissions.WriteScripture);
        SetupGrade(
            DirectDisciple,
            30,
            30,
            300,
            false,
            true,
            SectPermissions.ReadBasicScripture,
            SectPermissions.ReadCoreScripture,
            SectPermissions.ReadHighScripture,
            SectPermissions.WriteScripture);

        SetupOffice(NoOffice, 0, 0, true, false, false);
        SetupOffice(Deacon, 50, 35, false, false, false);
        SetupOffice(
            Elder,
            70,
            70,
            false,
            true,
            true,
            true,
            300,
            3,
            1,
            10,
            SectPermissions.ReadBasicScripture,
            SectPermissions.ReadCoreScripture,
            SectPermissions.ReadHighScripture,
            SectPermissions.WriteScripture,
            SectPermissions.RecruitMember,
            SectPermissions.EvaluatePersonnel,
            SectPermissions.ManageScripture);
        SetupOffice(
            Leader,
            100,
            100,
            false,
            false,
            false,
            true,
            0,
            -1,
            1,
            0,
            SectPermissions.ReadBasicScripture,
            SectPermissions.ReadCoreScripture,
            SectPermissions.ReadHighScripture,
            SectPermissions.WriteScripture,
            SectPermissions.RecruitMember,
            SectPermissions.EvaluatePersonnel,
            SectPermissions.PromoteMember,
            SectPermissions.ManageScripture,
            SectPermissions.ManageSect);

        SetupTitle(NoTitle, 0, 0, true, false);
        SetupTitle(
            Successor,
            40,
            60,
            false,
            true,
            SectPermissions.ReadBasicScripture,
            SectPermissions.ReadCoreScripture,
            SectPermissions.ReadHighScripture,
            SectPermissions.WriteScripture);
    }

    private static void SetupGrade(
        SectRoleAsset asset,
        int order,
        int authority,
        int minPersonnelScore,
        bool defaultForSlot,
        bool showInPersonnel,
        params SectPermissionAsset[] permissions)
    {
        Setup(asset, SectRoleSlot.Grade, order, authority, defaultForSlot, showInPersonnel, true, true, false, minPersonnelScore, -1, -1, 0, permissions);
    }

    private static void SetupOffice(
        SectRoleAsset asset,
        int order,
        int authority,
        bool defaultForSlot,
        bool allowAutoAssign,
        bool allowInitialAssign,
        bool clearsGrade = false,
        int minPersonnelScore = 0,
        int minCultivationLevel = -1,
        int baseSlots = -1,
        int membersPerExtraSlot = 0,
        params SectPermissionAsset[] permissions)
    {
        Setup(asset, SectRoleSlot.Office, order, authority, defaultForSlot, !defaultForSlot, allowAutoAssign, allowInitialAssign, clearsGrade, minPersonnelScore, minCultivationLevel, baseSlots, membersPerExtraSlot, permissions);
    }

    private static void SetupTitle(
        SectRoleAsset asset,
        int order,
        int authority,
        bool defaultForSlot,
        bool showInPersonnel,
        params SectPermissionAsset[] permissions)
    {
        Setup(asset, SectRoleSlot.Title, order, authority, defaultForSlot, showInPersonnel, false, false, false, 0, -1, -1, 0, permissions);
    }

    private static void Setup(
        SectRoleAsset asset,
        SectRoleSlot slot,
        int order,
        int authority,
        bool defaultForSlot,
        bool showInPersonnel,
        bool allowAutoAssign,
        bool allowInitialAssign,
        bool clearsGrade,
        int minPersonnelScore,
        int minCultivationLevel,
        int baseSlots,
        int membersPerExtraSlot,
        params SectPermissionAsset[] permissions)
    {
        asset.slot = slot;
        asset.order = order;
        asset.authority = authority;
        asset.nameKey = asset.id;
        asset.descriptionKey = $"{asset.id}.Info";
        asset.defaultForSlot = defaultForSlot;
        asset.showInPersonnel = showInPersonnel;
        asset.allowAutoAssign = allowAutoAssign;
        asset.allowInitialAssign = allowInitialAssign;
        asset.clearsGrade = clearsGrade;
        asset.minPersonnelScore = minPersonnelScore;
        asset.minCultivationLevel = minCultivationLevel;
        asset.baseSlots = baseSlots;
        asset.membersPerExtraSlot = membersPerExtraSlot;
        asset.permissionIds = new List<string>();
        for (int i = 0; i < permissions.Length; i++)
        {
            SectPermissionAsset permission = permissions[i];
            if (permission != null)
            {
                asset.permissionIds.Add(permission.id);
            }
        }
    }
}
