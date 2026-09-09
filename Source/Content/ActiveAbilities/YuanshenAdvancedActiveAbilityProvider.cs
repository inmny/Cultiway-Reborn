using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using strings;

namespace Cultiway.Content.ActiveAbilities;

/// <summary>把法相、化身、显圣和锚点网络接入玩家世界能力栏。</summary>
internal sealed class YuanshenAdvancedActiveAbilityProvider : IActiveAbilityProvider
{
    /// <summary>稳定来源编号。</summary>
    public const string ProviderId = "content.yuanshen_advanced";

    private const string Dharma = "dharma";
    private const string ReturnDharma = "return_dharma";
    private const string PrepareAvatar = "prepare_avatar";
    private const string CancelAvatar = "cancel_avatar";
    private const string ReturnAvatar = "return_avatar";
    private const string Manifest = "manifest";
    private const string ReturnManifest = "return_manifest";
    private const string ConsecrateSect = "consecrate_sect";
    private const string ConsecrateAltar = "consecrate_altar";
    private const string SelectAnchor = "select_anchor";
    private const string ConnectAnchor = "connect_anchor";
    private const string OfferIncense = "offer_incense";
    private const string DismantleAnchor = "dismantle_anchor";
    private const string Transit = "transit";
    private const string Engage = "engage";

    /// <summary>能力栏中固定的条目显示顺序。</summary>
    private static readonly string[] EntryOrder =
    [
        Manifest,
        ConsecrateSect,
        ConsecrateAltar,
        SelectAnchor,
        ConnectAnchor,
        OfferIncense,
        DismantleAnchor,
        ReturnManifest,
        Dharma,
        ReturnDharma,
        CancelAvatar,
        PrepareAvatar,
        ReturnAvatar,
        Transit,
        Engage
    ];

    /// <summary>返回稳定来源编号。</summary>
    public string Id => ProviderId;

    /// <summary>按元神层数、活动节点和准备状态提供固定命令。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!TryGetStage(caster, out int stage)) return;
        bool hasFocus = YuanshenThoughtService.TryGetFocused(caster, out _, out _);
        for (var i = 0; i < EntryOrder.Length; i++)
        {
            string entryId = EntryOrder[i];
            if (IsEntryAvailable(caster, entryId, stage, hasFocus))
                output.Add(new ActiveAbilityHandle(Id, caster.E, entryId));
        }
    }

    /// <summary>高阶元神命令只参与世界控制。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) =>
        IsValid(caster, handle) ? ActiveAbilityChannel.World : ActiveAbilityChannel.None;

    /// <summary>生成命令名称、图标、目标模式和关系。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        ResolvePresentation(handle.EntryId, out string key, out string icon);
        bool self = handle.EntryId is ReturnDharma or CancelAvatar or ReturnAvatar or ReturnManifest;
        bool engage = handle.EntryId == Engage;
        return new ActiveAbilityDescriptor(
            key.Localize(),
            SpriteTextureLoader.getSprite(icon),
            ActiveAbilityChannel.World,
            self ? ActiveAbilityTargetMode.Self : engage ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Point,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            self ? SkillUseTargetRelation.Self : engage
                ? SkillUseTargetRelation.Hostile
                : SkillUseTargetRelation.WorldTile);
    }

    /// <summary>句柄和层数仍有效时显示就绪。</summary>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsValid(caster, handle)
            ? ActiveAbilityControlState.Ready
            : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
    }

    /// <summary>准备阶段只校验人物与固定句柄。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        IsValid(caster, handle);

    /// <summary>敌对指派需要明确人物，其他点选命令由服务严格校验落点。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!IsValid(caster, handle)) return false;
        return handle.EntryId != Engage || target.Object != null && !target.Object.isRekt() &&
               target.Object.isActor() && target.Object.a != caster.Base;
    }

    /// <summary>普通战斗规划器不直接选择高阶元神管理命令。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 0;

    /// <summary>高阶元神管理没有普通战斗画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target) => default;

    /// <summary>返回点选命令的控制距离；远距锚点命令仍由已建网络严格校验。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (handle.EntryId is ReturnDharma or CancelAvatar or ReturnAvatar or ReturnManifest) return 0f;
        return 10000f;
    }

    /// <summary>高阶命令没有范围预览。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;

    /// <summary>提交高阶节点、设施、连接或迁移命令。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        return handle.EntryId switch
        {
            Dharma => YuanshenAdvancedNodeService.TryCreateDharmaForm(caster, target.Position),
            ReturnDharma => YuanshenAdvancedNodeService.RequestRoleReturn(caster, YuanshenNodeRole.DharmaForm),
            PrepareAvatar => YuanshenAdvancedNodeService.TryStartAvatarPreparation(caster, target.Position),
            CancelAvatar => YuanshenAdvancedNodeService.CancelAvatarPreparation(caster),
            ReturnAvatar => YuanshenAdvancedNodeService.RequestRoleReturn(caster, YuanshenNodeRole.Avatar),
            Manifest => YuanshenAdvancedNodeService.TryCreateManifestation(caster, target.Position),
            ReturnManifest => YuanshenAdvancedNodeService.RequestRoleReturn(caster,
                YuanshenNodeRole.Manifestation),
            ConsecrateSect => YuanshenAnchorNetworkService.TryConsecrate(caster, target.Position,
                YuanshenAnchorKind.SectPlatform),
            ConsecrateAltar => YuanshenAnchorNetworkService.TryConsecrate(caster, target.Position,
                YuanshenAnchorKind.CityAltar),
            SelectAnchor => YuanshenAnchorNetworkService.TrySelectForConnection(caster, target.Position),
            ConnectAnchor => YuanshenAnchorNetworkService.TryConnectSelected(caster, target.Position),
            OfferIncense => YuanshenAnchorNetworkService.TryOfferIncense(caster, target.Position),
            DismantleAnchor => YuanshenAnchorNetworkService.TryDismantle(caster, target.Position),
            Transit => YuanshenAdvancedNodeService.TryTransitFocused(caster, target.Position),
            Engage => YuanshenAdvancedNodeService.TryAssignFocusedEngage(caster, target.Object.a),
            _ => false
        };
    }

    /// <summary>校验句柄来源和条目当前是否仍应出现在能力栏。</summary>
    private static bool IsValid(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (caster == null || handle.ProviderId != ProviderId || handle.Source != caster.E ||
            !TryGetStage(caster, out int stage)) return false;
        bool hasFocus = YuanshenThoughtService.TryGetFocused(caster, out _, out _);
        return IsEntryAvailable(caster, handle.EntryId, stage, hasFocus);
    }

    /// <summary>读取人物是否具备高阶元神能力栏及其当前层数。</summary>
    /// <param name="caster">能力所有者。</param>
    /// <param name="stage">返回当前元神层数。</param>
    /// <returns>人物具备七层元神且未在锚点迁移时返回真。</returns>
    private static bool TryGetStage(ActorExtend caster, out int stage)
    {
        stage = 0;
        if (caster == null || caster.HasComponent<YuanshenBodilessTransitState>() ||
            !YuanshenNodeCombatService.CanUseSoulAbilities(caster) ||
            !caster.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 7) return false;
        stage = yuanshen.stage;
        return true;
    }

    /// <summary>按层数和当前节点状态判断固定条目是否可用。</summary>
    /// <param name="caster">能力所有者。</param>
    /// <param name="entryId">固定条目编号。</param>
    /// <param name="stage">当前元神层数。</param>
    /// <param name="hasFocus">是否存在有效聚焦节点。</param>
    /// <returns>条目当前应显示且旧句柄仍有效时返回真。</returns>
    private static bool IsEntryAvailable(ActorExtend caster, string entryId, int stage, bool hasFocus)
    {
        return entryId switch
        {
            Manifest or ConsecrateSect or ConsecrateAltar or SelectAnchor or ConnectAnchor or OfferIncense or
                DismantleAnchor => true,
            ReturnManifest => YuanshenAdvancedNodeService.CountRole(caster, YuanshenNodeRole.Manifestation) > 0,
            Dharma => stage >= 8,
            ReturnDharma => stage >= 8 &&
                            YuanshenAdvancedNodeService.CountRole(caster, YuanshenNodeRole.DharmaForm) > 0,
            CancelAvatar => stage >= 9 && caster.HasComponent<YuanshenAvatarPreparationState>(),
            PrepareAvatar => stage >= 9 && !caster.HasComponent<YuanshenAvatarPreparationState>() &&
                             YuanshenAdvancedNodeService.CountRole(caster, YuanshenNodeRole.Avatar) == 0,
            ReturnAvatar => stage >= 9 &&
                            YuanshenAdvancedNodeService.CountRole(caster, YuanshenNodeRole.Avatar) > 0,
            Transit => hasFocus || YuanshenLifecycleService.IsBodiless(caster),
            Engage => hasFocus,
            _ => false
        };
    }

    /// <summary>按条目取得本地化键和现有资源图标。</summary>
    private static void ResolvePresentation(string entryId, out string key, out string icon)
    {
        key = "Cultiway.Yuanshen.Advanced." + entryId;
        icon = entryId switch
        {
            Dharma or ReturnDharma => "cultiway/icons/artifact_atoms/ancestral_guardian_vow",
            PrepareAvatar or CancelAvatar or ReturnAvatar => "cultiway/icons/artifact_atoms/transformation_pattern",
            Manifest or ReturnManifest => "cultiway/icons/artifact_atoms/guardian_halo",
            ConsecrateSect or SelectAnchor or ConnectAnchor => "cultiway/icons/artifact_atoms/spirit_ding",
            ConsecrateAltar or OfferIncense => "cultiway/icons/artifact_atoms/soul_banner",
            DismantleAnchor => "cultiway/icons/artifact_atoms/soul_binding_script",
            Transit => "cultiway/icons/artifact_atoms/spatial_fold",
            Engage => "cultiway/icons/artifact_atoms/soul_mirror",
            _ => "cultiway/icons/artifact_atoms/soul_core"
        };
    }
}
