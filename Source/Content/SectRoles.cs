using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门角色资产集合，定义门阶、职司和头衔的默认配置。
/// </summary>
[Dependency(typeof(SectPermissions), typeof(MasterApprenticeTypes))]
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

    /// <summary>
    /// 启用库的自动资产注册，让静态角色字段按前缀自动生成对应的 <see cref="SectRoleAsset"/>。
    /// </summary>
    /// <returns>始终返回 true，表示该库由 <see cref="ExtendLibrary{TAsset, TLibrary}"/> 自动注册资产。</returns>
    protected override bool AutoRegisterAssets() => true;

    /// <summary>
    /// 获取宗门角色资产的统一 ID 前缀。
    /// </summary>
    /// <returns>宗门角色资产 ID 的命名空间前缀。</returns>
    protected override string Prefix() => "Cultiway.Sect.Role";

    /// <summary>
    /// 初始化宗门角色资产的门阶、职司、头衔配置。
    /// </summary>
    protected override void OnInit()
    {
        // 默认门阶：用于填充门阶槽位，避免成员没有任何门阶时出现空角色。
        SetupGrade(
            NoGrade, // asset：要初始化的角色资产。
            0, // order：默认门阶排序最低。
            0, // authority：默认门阶没有权威。
            0, // minPersonnelScore：默认门阶没有人事评分要求。
            true, // defaultForSlot：作为门阶槽位默认值。
            false); // showInPersonnel：不在人事界面单独显示。

        // 外门弟子：入宗后的基础门阶，要求至少存在记名师徒关系；可由有带徒入宗权限的人自动补师父。
        SetupGradeWithMasterRequirement(
            OuterDisciple, // asset：外门弟子角色资产。
            10, // order：基础弟子门阶排序。
            10, // authority：外门弟子的基础权威。
            0, // minPersonnelScore：外门弟子没有人事评分要求。
            false, // defaultForSlot：不是门阶槽位默认值。
            true, // showInPersonnel：在人事界面显示外门分组。
            MasterApprenticeTypes.Nominal, // requiredMasterRelationType：至少需要记名师徒关系。
            true, // canAutoAssignMasterForRequirement：允许评定时自动匹配师父。
            null, // requiredMasterOfficeRole：不按师父职司限制。
            SectPermissions.BringApprenticeToSect, // requiredMasterPermission：师父需要有带徒入宗权限。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.WriteScripture); // permissions：可撰写并贡献藏书。

        // 内门弟子：进阶门阶，要求人事评分达到 200 且具备正式师徒关系；可自动匹配有带徒入宗权限的师父。
        SetupGradeWithMasterRequirement(
            InnerDisciple, // asset：内门弟子角色资产。
            20, // order：高于外门弟子的门阶排序。
            20, // authority：内门弟子的权威。
            200, // minPersonnelScore：内门弟子最低人事评分。
            false, // defaultForSlot：不是门阶槽位默认值。
            true, // showInPersonnel：在人事界面显示内门分组。
            MasterApprenticeTypes.Formal, // requiredMasterRelationType：至少需要正式师徒关系。
            true, // canAutoAssignMasterForRequirement：允许评定时自动匹配师父。
            null, // requiredMasterOfficeRole：不按师父职司限制。
            SectPermissions.BringApprenticeToSect, // requiredMasterPermission：师父需要有带徒入宗权限。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.ReadCoreScripture, // permissions：可阅读核心藏书。
            SectPermissions.WriteScripture); // permissions：可撰写并贡献藏书。

        // 亲传弟子：高阶弟子身份，要求人事评分达到 300 且已经具备亲传师徒关系，不自动补师父。
        SetupGradeWithMasterRequirement(
            DirectDisciple, // asset：亲传弟子角色资产。
            30, // order：普通门阶中的最高排序。
            30, // authority：亲传弟子的权威。
            300, // minPersonnelScore：亲传弟子最低人事评分。
            false, // defaultForSlot：不是门阶槽位默认值。
            true, // showInPersonnel：在人事界面显示亲传分组。
            MasterApprenticeTypes.Direct, // requiredMasterRelationType：必须具备亲传师徒关系。
            false, // canAutoAssignMasterForRequirement：不自动匹配亲传师父。
            null, // requiredMasterOfficeRole：不按师父职司限制。
            null, // requiredMasterPermission：不按师父权限限制。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.ReadCoreScripture, // permissions：可阅读核心藏书。
            SectPermissions.ReadHighScripture, // permissions：可阅读高阶藏书。
            SectPermissions.WriteScripture); // permissions：可撰写并贡献藏书。

        // 默认职司：用于填充职司槽位，不显示在人事列表中，也不参与自动任命。
        SetupOffice(
            NoOffice, // asset：无职司角色资产。
            0, // order：默认职司排序最低。
            0, // authority：默认职司没有权威。
            true, // defaultForSlot：作为职司槽位默认值。
            false, // allowAutoAssign：不允许自动任命。
            false); // allowInitialAssign：不允许初始任命。

        // 执事：基础管理职司，要求内门弟子、人事评分 200、境界等级 2；可初始任命并按人数扩充名额。
        SetupOfficeWithRequirements(
            Deacon, // asset：执事角色资产。
            50, // order：基础管理职司排序。
            35, // authority：高于弟子门阶的管理权威。
            false, // defaultForSlot：不是职司槽位默认值。
            true, // allowAutoAssign：允许人事评定自动任命。
            true, // allowInitialAssign：允许入宗或建宗时初始任命。
            false, // clearsGrade：获得执事后保留门阶。
            200, // minPersonnelScore：执事最低人事评分。
            2, // minCultivationLevel：执事最低境界等级。
            2, // baseSlots：执事基础名额。
            8, // membersPerExtraSlot：每 8 名成员增加 1 个执事名额。
            InnerDisciple, // requiredGradeRole：必须至少是内门弟子。
            null, // requiredPreviousOfficeRole：不要求前置职司。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.ReadCoreScripture, // permissions：可阅读核心藏书。
            SectPermissions.WriteScripture, // permissions：可撰写并贡献藏书。
            SectPermissions.RecruitMember, // permissions：可招揽成员。
            SectPermissions.BringApprenticeToSect); // permissions：可带徒入宗。

        // 长老：高层管理职司，要求从执事晋升、人事评分 300、境界等级 3；负责人事评定和藏经阁管理。
        SetupOfficeWithRequirements(
            Elder, // asset：长老角色资产。
            70, // order：高层管理职司排序。
            70, // authority：长老权威。
            false, // defaultForSlot：不是职司槽位默认值。
            true, // allowAutoAssign：允许人事评定自动任命。
            false, // allowInitialAssign：不允许入宗时直接成为长老。
            true, // clearsGrade：获得长老后清除弟子门阶。
            300, // minPersonnelScore：长老最低人事评分。
            3, // minCultivationLevel：长老最低境界等级。
            1, // baseSlots：长老基础名额。
            10, // membersPerExtraSlot：每 10 名成员增加 1 个长老名额。
            null, // requiredGradeRole：不要求特定门阶。
            Deacon, // requiredPreviousOfficeRole：必须先担任执事。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.ReadCoreScripture, // permissions：可阅读核心藏书。
            SectPermissions.ReadHighScripture, // permissions：可阅读高阶藏书。
            SectPermissions.WriteScripture, // permissions：可撰写并贡献藏书。
            SectPermissions.RecruitMember, // permissions：可招揽成员。
            SectPermissions.BringApprenticeToSect, // permissions：可带徒入宗。
            SectPermissions.EvaluatePersonnel, // permissions：可评定宗门人事。
            SectPermissions.ManageScripture); // permissions：可管理藏经阁。

        // 掌门：宗门最高职司，唯一名额，清除门阶并授予宗门管理、提拔、人事评定等完整权限。
        SetupOffice(
            Leader, // asset：掌门角色资产。
            100, // order：职司最高排序。
            100, // authority：宗门最高权威。
            false, // defaultForSlot：不是职司槽位默认值。
            false, // allowAutoAssign：不通过普通人事评定自动产生。
            false, // allowInitialAssign：不通过普通入宗初始任命。
            true, // clearsGrade：掌门不再保留弟子门阶。
            0, // minPersonnelScore：掌门本身不设置人事评分门槛。
            -1, // minCultivationLevel：掌门本身不设置境界等级门槛。
            1, // baseSlots：掌门唯一名额。
            0, // membersPerExtraSlot：不随成员数扩充名额。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.ReadCoreScripture, // permissions：可阅读核心藏书。
            SectPermissions.ReadHighScripture, // permissions：可阅读高阶藏书。
            SectPermissions.WriteScripture, // permissions：可撰写并贡献藏书。
            SectPermissions.RecruitMember, // permissions：可招揽成员。
            SectPermissions.BringApprenticeToSect, // permissions：可带徒入宗。
            SectPermissions.EvaluatePersonnel, // permissions：可评定宗门人事。
            SectPermissions.PromoteMember, // permissions：可提拔成员。
            SectPermissions.ManageScripture, // permissions：可管理藏经阁。
            SectPermissions.ManageSect); // permissions：可管理宗门。

        // 默认头衔：用于填充头衔槽位，避免没有特殊头衔时出现空角色。
        SetupTitle(
            NoTitle, // asset：无头衔角色资产。
            0, // order：默认头衔排序最低。
            0, // authority：默认头衔没有权威。
            true, // defaultForSlot：作为头衔槽位默认值。
            false); // showInPersonnel：不在人事界面单独显示。

        // 衣钵传人：掌门继任相关头衔，要求已经具备衣钵传人师徒关系，不自动补师父。
        SetupTitleWithMasterRequirement(
            Successor, // asset：衣钵传人角色资产。
            40, // order：特殊头衔排序。
            60, // authority：衣钵传人的头衔权威。
            false, // defaultForSlot：不是头衔槽位默认值。
            true, // showInPersonnel：在人事界面显示衣钵传人分组。
            MasterApprenticeTypes.Successor, // requiredMasterRelationType：必须具备衣钵传人师徒关系。
            false, // canAutoAssignMasterForRequirement：不自动匹配衣钵师父。
            null, // requiredMasterOfficeRole：不按师父职司限制。
            null, // requiredMasterPermission：不按师父权限限制。
            SectPermissions.ReadBasicScripture, // permissions：可阅读基础藏书。
            SectPermissions.ReadCoreScripture, // permissions：可阅读核心藏书。
            SectPermissions.ReadHighScripture, // permissions：可阅读高阶藏书。
            SectPermissions.WriteScripture); // permissions：可撰写并贡献藏书。
    }

    /// <summary>
    /// 初始化门阶角色的通用配置。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="minPersonnelScore">自动任命或晋升时要求的最低人事评分。</param>
    /// <param name="defaultForSlot">是否作为门阶槽位的默认角色。</param>
    /// <param name="showInPersonnel">是否在人事界面中显示该角色分组。</param>
    /// <param name="permissions">该门阶直接授予的权限列表。</param>
    private static void SetupGrade(
        SectRoleAsset asset,
        int order,
        int authority,
        int minPersonnelScore,
        bool defaultForSlot,
        bool showInPersonnel,
        params SectPermissionAsset[] permissions)
    {
        Setup(
            asset,
            SectRoleSlot.Grade,
            order,
            authority,
            defaultForSlot,
            showInPersonnel,
            true,
            true,
            false,
            minPersonnelScore,
            -1,
            -1,
            0,
            null,
            false,
            null,
            null,
            null,
            null,
            permissions);
    }

    /// <summary>
    /// 初始化带师徒关系要求的门阶角色。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="minPersonnelScore">自动任命或晋升时要求的最低人事评分。</param>
    /// <param name="defaultForSlot">是否作为门阶槽位的默认角色。</param>
    /// <param name="showInPersonnel">是否在人事界面中显示该角色分组。</param>
    /// <param name="requiredMasterRelationType">晋升该门阶前必须满足的师徒关系类型。</param>
    /// <param name="canAutoAssignMasterForRequirement">是否允许人事评定时自动为该门阶匹配师父。</param>
    /// <param name="requiredMasterOfficeRole">自动匹配师父时要求师父具备的职司；为 null 表示不按职司限制。</param>
    /// <param name="requiredMasterPermission">自动匹配师父时要求师父具备的权限；为 null 表示不按权限限制。</param>
    /// <param name="permissions">该门阶直接授予的权限列表。</param>
    private static void SetupGradeWithMasterRequirement(
        SectRoleAsset asset,
        int order,
        int authority,
        int minPersonnelScore,
        bool defaultForSlot,
        bool showInPersonnel,
        MasterApprenticeTypeAsset requiredMasterRelationType,
        bool canAutoAssignMasterForRequirement,
        SectRoleAsset requiredMasterOfficeRole,
        SectPermissionAsset requiredMasterPermission,
        params SectPermissionAsset[] permissions)
    {
        Setup(
            asset,
            SectRoleSlot.Grade,
            order,
            authority,
            defaultForSlot,
            showInPersonnel,
            true,
            true,
            false,
            minPersonnelScore,
            -1,
            -1,
            0,
            requiredMasterRelationType,
            canAutoAssignMasterForRequirement,
            requiredMasterOfficeRole,
            requiredMasterPermission,
            null,
            null,
            permissions);
    }

    /// <summary>
    /// 初始化不带额外前置角色要求的职司角色。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="defaultForSlot">是否作为职司槽位的默认角色。</param>
    /// <param name="allowAutoAssign">是否允许人事评定自动任命该职司。</param>
    /// <param name="allowInitialAssign">入宗或建宗时是否允许直接获得该职司。</param>
    /// <param name="clearsGrade">获得该职司后是否清除门阶。</param>
    /// <param name="minPersonnelScore">自动任命或晋升时要求的最低人事评分。</param>
    /// <param name="minCultivationLevel">自动任命或晋升时要求的最低境界等级；-1 表示不限制。</param>
    /// <param name="baseSlots">基础名额；-1 表示不限制。</param>
    /// <param name="membersPerExtraSlot">每增加多少名宗门成员额外增加一个名额；0 表示不随成员数扩充。</param>
    /// <param name="permissions">该职司直接授予的权限列表。</param>
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
        SetupOfficeWithRequirements(
            asset,
            order,
            authority,
            defaultForSlot,
            allowAutoAssign,
            allowInitialAssign,
            clearsGrade,
            minPersonnelScore,
            minCultivationLevel,
            baseSlots,
            membersPerExtraSlot,
            null,
            null,
            permissions);
    }

    /// <summary>
    /// 初始化带门阶或前置职司要求的职司角色。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="defaultForSlot">是否作为职司槽位的默认角色。</param>
    /// <param name="allowAutoAssign">是否允许人事评定自动任命该职司。</param>
    /// <param name="allowInitialAssign">入宗或建宗时是否允许直接获得该职司。</param>
    /// <param name="clearsGrade">获得该职司后是否清除门阶。</param>
    /// <param name="minPersonnelScore">自动任命或晋升时要求的最低人事评分。</param>
    /// <param name="minCultivationLevel">自动任命或晋升时要求的最低境界等级；-1 表示不限制。</param>
    /// <param name="baseSlots">基础名额；-1 表示不限制。</param>
    /// <param name="membersPerExtraSlot">每增加多少名宗门成员额外增加一个名额；0 表示不随成员数扩充。</param>
    /// <param name="requiredGradeRole">晋升该职司前必须具备的门阶；为 null 表示不限制。</param>
    /// <param name="requiredPreviousOfficeRole">晋升该职司前必须具备的前置职司；为 null 表示不限制。</param>
    /// <param name="permissions">该职司直接授予的权限列表。</param>
    private static void SetupOfficeWithRequirements(
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
        SectRoleAsset requiredGradeRole = null,
        SectRoleAsset requiredPreviousOfficeRole = null,
        params SectPermissionAsset[] permissions)
    {
        Setup(
            asset,
            SectRoleSlot.Office,
            order,
            authority,
            defaultForSlot,
            !defaultForSlot,
            allowAutoAssign,
            allowInitialAssign,
            clearsGrade,
            minPersonnelScore,
            minCultivationLevel,
            baseSlots,
            membersPerExtraSlot,
            null,
            false,
            null,
            null,
            requiredGradeRole,
            requiredPreviousOfficeRole,
            permissions);
    }

    /// <summary>
    /// 初始化不带师徒关系要求的头衔角色。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="defaultForSlot">是否作为头衔槽位的默认角色。</param>
    /// <param name="showInPersonnel">是否在人事界面中显示该角色分组。</param>
    /// <param name="permissions">该头衔直接授予的权限列表。</param>
    private static void SetupTitle(
        SectRoleAsset asset,
        int order,
        int authority,
        bool defaultForSlot,
        bool showInPersonnel,
        params SectPermissionAsset[] permissions)
    {
        Setup(asset, SectRoleSlot.Title, order, authority, defaultForSlot, showInPersonnel, false, false, false, 0, -1, -1, 0, null, false, null, null, null, null, permissions);
    }

    /// <summary>
    /// 初始化带师徒关系要求的头衔角色。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="defaultForSlot">是否作为头衔槽位的默认角色。</param>
    /// <param name="showInPersonnel">是否在人事界面中显示该角色分组。</param>
    /// <param name="requiredMasterRelationType">获得该头衔前必须满足的师徒关系类型。</param>
    /// <param name="canAutoAssignMasterForRequirement">是否允许人事评定时自动为该头衔匹配师父。</param>
    /// <param name="requiredMasterOfficeRole">自动匹配师父时要求师父具备的职司；为 null 表示不按职司限制。</param>
    /// <param name="requiredMasterPermission">自动匹配师父时要求师父具备的权限；为 null 表示不按权限限制。</param>
    /// <param name="permissions">该头衔直接授予的权限列表。</param>
    private static void SetupTitleWithMasterRequirement(
        SectRoleAsset asset,
        int order,
        int authority,
        bool defaultForSlot,
        bool showInPersonnel,
        MasterApprenticeTypeAsset requiredMasterRelationType,
        bool canAutoAssignMasterForRequirement,
        SectRoleAsset requiredMasterOfficeRole,
        SectPermissionAsset requiredMasterPermission,
        params SectPermissionAsset[] permissions)
    {
        Setup(asset, SectRoleSlot.Title, order, authority, defaultForSlot, showInPersonnel, false, false, false, 0, -1, -1, 0, requiredMasterRelationType, canAutoAssignMasterForRequirement, requiredMasterOfficeRole, requiredMasterPermission, null, null, permissions);
    }

    /// <summary>
    /// 写入宗门角色资产的完整配置，是所有角色初始化包装函数的最终落点。
    /// </summary>
    /// <param name="asset">要初始化的宗门角色资产。</param>
    /// <param name="slot">角色所属槽位，决定它是门阶、职司还是头衔。</param>
    /// <param name="order">同槽位内的排序值，数值越高越靠后也通常表示层级越高。</param>
    /// <param name="authority">角色权威值，用于比较角色地位和权限边界。</param>
    /// <param name="defaultForSlot">是否作为所在槽位的默认角色。</param>
    /// <param name="showInPersonnel">是否在人事界面中显示该角色分组。</param>
    /// <param name="allowAutoAssign">是否允许人事评定自动任命该角色。</param>
    /// <param name="allowInitialAssign">入宗或建宗时是否允许直接获得该角色。</param>
    /// <param name="clearsGrade">获得该角色后是否清除门阶。</param>
    /// <param name="minPersonnelScore">自动任命或晋升时要求的最低人事评分。</param>
    /// <param name="minCultivationLevel">自动任命或晋升时要求的最低境界等级；-1 表示不限制。</param>
    /// <param name="baseSlots">基础名额；-1 表示不限制。</param>
    /// <param name="membersPerExtraSlot">每增加多少名宗门成员额外增加一个名额；0 表示不随成员数扩充。</param>
    /// <param name="requiredMasterRelationType">获得该角色前必须满足的师徒关系类型；为 null 表示不限制。</param>
    /// <param name="canAutoAssignMasterForRequirement">是否允许人事评定时自动为该角色匹配师父。</param>
    /// <param name="requiredMasterOfficeRole">自动匹配师父时要求师父具备的职司；为 null 表示不按职司限制。</param>
    /// <param name="requiredMasterPermission">自动匹配师父时要求师父具备的权限；为 null 表示不按权限限制。</param>
    /// <param name="requiredGradeRole">晋升该角色前必须具备的门阶；为 null 表示不限制。</param>
    /// <param name="requiredPreviousOfficeRole">晋升该角色前必须具备的前置职司；为 null 表示不限制。</param>
    /// <param name="permissions">该角色直接授予的权限列表；null 项会被忽略。</param>
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
        MasterApprenticeTypeAsset requiredMasterRelationType,
        bool canAutoAssignMasterForRequirement,
        SectRoleAsset requiredMasterOfficeRole,
        SectPermissionAsset requiredMasterPermission,
        SectRoleAsset requiredGradeRole,
        SectRoleAsset requiredPreviousOfficeRole,
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
        asset.requiredMasterRelationTypeId = requiredMasterRelationType?.id;
        asset.canAutoAssignMasterForRequirement = canAutoAssignMasterForRequirement;
        asset.requiredMasterOfficeRoleId = requiredMasterOfficeRole?.id;
        asset.requiredMasterPermissionId = requiredMasterPermission?.id;
        asset.requiredGradeRoleId = requiredGradeRole?.id;
        asset.requiredPreviousOfficeRoleId = requiredPreviousOfficeRole?.id;
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
