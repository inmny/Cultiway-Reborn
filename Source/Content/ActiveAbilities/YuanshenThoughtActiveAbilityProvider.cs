using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using strings;
using UnityEngine;

namespace Cultiway.Content.ActiveAbilities;

/// <summary>把普通分念的创建、聚焦、移动和批量归一接入玩家能力栏。</summary>
internal sealed class YuanshenThoughtActiveAbilityProvider : IActiveAbilityProvider
{
    /// <summary>稳定来源编号。</summary>
    public const string ProviderId = "content.yuanshen_thought";

    /// <summary>创建分念条目。</summary>
    private const string Create = "create";

    /// <summary>点击聚焦条目。</summary>
    private const string Focus = "focus";

    /// <summary>移动当前聚焦节点条目。</summary>
    private const string MoveFocused = "move_focused";

    /// <summary>令当前聚焦节点守护明确地点。</summary>
    private const string GuardFocused = "guard_focused";

    /// <summary>令当前聚焦节点跟随明确友方人物。</summary>
    private const string FollowFocused = "follow_focused";

    /// <summary>令当前聚焦节点追踪短时锁定。</summary>
    private const string TrackFocused = "track_focused";

    /// <summary>归返当前聚焦节点条目。</summary>
    private const string ReturnFocused = "return_focused";

    /// <summary>归返全部普通分念条目。</summary>
    private const string ReturnAll = "return_all";

    /// <summary>令聚焦节点接管下一件法器。</summary>
    private const string ControlArtifact = "control_artifact";

    /// <summary>解除聚焦节点的远程控宝。</summary>
    private const string ReleaseArtifact = "release_artifact";

    /// <summary>节点点击命中半径。</summary>
    private const float NodePickRadius = 3f;

    /// <summary>能力栏中固定的条目显示顺序。</summary>
    private static readonly string[] EntryOrder =
    [
        Create,
        Focus,
        MoveFocused,
        TrackFocused,
        GuardFocused,
        FollowFocused,
        ReturnFocused,
        ControlArtifact,
        ReleaseArtifact,
        ReturnAll
    ];

    /// <summary>返回稳定来源编号。</summary>
    public string Id => ProviderId;

    /// <summary>蕴养三层后提供创建与聚焦；存在聚焦或分念时提供对应管理命令。</summary>
    /// <param name="caster">能力所有者。</param>
    /// <param name="output">接收能力句柄的集合。</param>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!TryGetAvailability(caster, out int stage, out bool hasFocus, out bool hasThoughts)) return;
        for (var i = 0; i < EntryOrder.Length; i++)
        {
            string entryId = EntryOrder[i];
            if (IsEntryAvailable(entryId, stage, hasFocus, hasThoughts))
                output.Add(new ActiveAbilityHandle(Id, caster.E, entryId));
        }
    }

    /// <summary>分念管理只参与世界控制。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) =>
        IsValid(caster, handle) ? ActiveAbilityChannel.World : ActiveAbilityChannel.None;

    /// <summary>生成分念能力栏描述。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        ResolvePresentation(handle.EntryId, out string key, out string icon);
        bool self = handle.EntryId is ReturnFocused or ReturnAll or ControlArtifact or ReleaseArtifact;
        ActiveAbilityTargetMode targetMode = self
            ? ActiveAbilityTargetMode.Self
            : handle.EntryId == FollowFocused
                ? ActiveAbilityTargetMode.Object
                : ActiveAbilityTargetMode.Point;
        SkillUseTargetRelation relation = self
            ? SkillUseTargetRelation.Self
            : handle.EntryId == FollowFocused
                ? SkillUseTargetRelation.Friendly
                : SkillUseTargetRelation.WorldTile;
        return new ActiveAbilityDescriptor(
            key.Localize(),
            SpriteTextureLoader.getSprite(icon),
            ActiveAbilityChannel.World,
            targetMode,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            relation);
    }

    /// <summary>返回条目当前控制状态。</summary>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsValid(caster, handle)
            ? ActiveAbilityControlState.Ready
            : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
    }

    /// <summary>检查是否能开始使用分念命令。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        IsValid(caster, handle);

    /// <summary>检查明确点选目标或当前聚焦状态。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!IsValid(caster, handle)) return false;
        return handle.EntryId switch
        {
            Create => YuanshenTravelService.IsWithinTether(caster, target.Position),
            Focus => YuanshenThoughtService.TryGetOwnedAtPoint(caster, target.Position, NodePickRadius, out _),
            MoveFocused or GuardFocused => YuanshenTravelService.IsWithinTether(caster, target.Position),
            FollowFocused => target.Object != null && !target.Object.isRekt() && target.Object.isActor() &&
                             !caster.Base.canAttackTarget(target.Object.a) &&
                             YuanshenTravelService.IsWithinTether(caster, target.Object.a.current_position),
            TrackFocused => YuanshenNodeLockService.TryGetLockedNear(
                caster.Base, target.Position, NodePickRadius, out _),
            ReturnFocused or ReturnAll or ControlArtifact or ReleaseArtifact => true,
            _ => false
        };
    }

    /// <summary>普通战斗规划器不自动选择玩家分念管理命令。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 0;

    /// <summary>分念管理没有普通战斗画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target) => default;

    /// <summary>返回点选命令的牵引上限。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        handle.EntryId is ReturnFocused or ReturnAll or ControlArtifact or ReleaseArtifact ? 0f : 10000f;

    /// <summary>聚焦提供固定点击半径。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) =>
        handle.EntryId is Focus or TrackFocused ? NodePickRadius : 0f;

    /// <summary>提交创建、聚焦、移动或归返命令。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        switch (handle.EntryId)
        {
            case Create:
                return YuanshenThoughtService.TryCreateThought(caster, target.Position);
            case Focus:
                return YuanshenThoughtService.TryGetOwnedAtPoint(
                           caster,
                           target.Position,
                           NodePickRadius,
                           out var selected) &&
                       YuanshenThoughtService.TryFocus(caster, selected);
            case MoveFocused:
                return YuanshenThoughtService.TryGetFocused(caster, out var moving, out _) &&
                       YuanshenThoughtService.TryAssignMove(caster, moving, target.Position);
            case GuardFocused:
                return YuanshenThoughtService.TryGetFocused(caster, out var guarding, out _) &&
                       YuanshenThoughtService.TryAssignGuard(caster, guarding, target.Position);
            case FollowFocused:
                return YuanshenThoughtService.TryGetFocused(caster, out var following, out _) &&
                       YuanshenThoughtService.TryAssignFollow(caster, following, target.Object.a);
            case TrackFocused:
                return YuanshenThoughtService.TryGetFocused(caster, out var tracking, out _) &&
                       YuanshenNodeLockService.TryGetLockedNear(
                           caster.Base, target.Position, NodePickRadius, out var tracked) &&
                       YuanshenThoughtService.TryAssignLockedNodeTracking(caster, tracking, tracked);
            case ReturnFocused:
                return YuanshenThoughtService.TryGetFocused(caster, out var returning, out _) &&
                       YuanshenThoughtService.RequestReturn(caster, returning);
            case ReturnAll:
                return YuanshenThoughtService.RequestAllThoughtsReturn(caster);
            case ControlArtifact:
                return ArtifactYuanshenControlService.TryBindNextEquippedArtifact(caster);
            case ReleaseArtifact:
                return ArtifactYuanshenControlService.ReleaseFocusedArtifact(caster);
            default:
                return false;
        }
    }

    /// <summary>校验句柄来源和条目当前是否仍应出现在能力栏。</summary>
    private static bool IsValid(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (caster == null || handle.ProviderId != ProviderId || handle.Source != caster.E ||
            !TryGetAvailability(caster, out int stage, out bool hasFocus, out bool hasThoughts)) return false;
        return IsEntryAvailable(handle.EntryId, stage, hasFocus, hasThoughts);
    }

    /// <summary>一次读取人物的分念能力栏公共状态。</summary>
    /// <param name="caster">能力所有者。</param>
    /// <param name="stage">返回当前元神层数。</param>
    /// <param name="hasFocus">返回是否存在有效聚焦节点。</param>
    /// <param name="hasThoughts">返回是否存在普通分念。</param>
    /// <returns>人物具备分念能力栏的基础条件时返回真。</returns>
    private static bool TryGetAvailability(
        ActorExtend caster,
        out int stage,
        out bool hasFocus,
        out bool hasThoughts)
    {
        stage = 0;
        hasFocus = false;
        hasThoughts = false;
        if (caster == null || caster.HasComponent<Components.YuanshenBodilessTransitState>() ||
            !YuanshenNodeCombatService.CanUseSoulAbilities(caster) ||
            !caster.TryGetComponent(out Components.Yuanshen yuanshen) || yuanshen.stage < 3) return false;
        stage = yuanshen.stage;
        hasFocus = YuanshenThoughtService.TryGetFocused(caster, out _, out _);
        hasThoughts = YuanshenThoughtService.CountThoughts(caster) > 0;
        return true;
    }

    /// <summary>按层数和当前节点状态判断固定条目是否可用。</summary>
    /// <param name="entryId">固定条目编号。</param>
    /// <param name="stage">当前元神层数。</param>
    /// <param name="hasFocus">是否存在有效聚焦节点。</param>
    /// <param name="hasThoughts">是否存在普通分念。</param>
    /// <returns>条目当前应显示且旧句柄仍有效时返回真。</returns>
    private static bool IsEntryAvailable(string entryId, int stage, bool hasFocus, bool hasThoughts)
    {
        return entryId switch
        {
            Create or Focus => true,
            MoveFocused or TrackFocused or ReturnFocused => hasFocus,
            GuardFocused or FollowFocused => stage >= 5 && hasFocus,
            ControlArtifact or ReleaseArtifact => stage >= 4 && hasFocus,
            ReturnAll => hasThoughts,
            _ => false
        };
    }

    /// <summary>按条目取得本地化名称与现有图标。</summary>
    private static void ResolvePresentation(string entryId, out string key, out string icon)
    {
        switch (entryId)
        {
            case Create:
                key = "Cultiway.Yuanshen.Ability.CreateThought";
                icon = "cultiway/icons/artifact_atoms/spirit_awakening_script";
                return;
            case Focus:
                key = "Cultiway.Yuanshen.Ability.FocusThought";
                icon = "cultiway/icons/artifact_atoms/soul_mirror";
                return;
            case MoveFocused:
                key = "Cultiway.Yuanshen.Ability.MoveThought";
                icon = "cultiway/icons/artifact_atoms/spirit_gathering_pattern";
                return;
            case GuardFocused:
                key = "Cultiway.Yuanshen.Ability.GuardThought";
                icon = "cultiway/icons/artifact_atoms/soul_binding_script";
                return;
            case FollowFocused:
                key = "Cultiway.Yuanshen.Ability.FollowThought";
                icon = "cultiway/icons/artifact_atoms/spirit_gathering_pattern";
                return;
            case TrackFocused:
                key = "Cultiway.Yuanshen.Ability.TrackThought";
                icon = "cultiway/icons/artifact_atoms/soul_mirror";
                return;
            case ReturnFocused:
                key = "Cultiway.Yuanshen.Ability.ReturnThought";
                icon = "cultiway/icons/artifact_atoms/spirit_awakening_script";
                return;
            case ReturnAll:
                key = "Cultiway.Yuanshen.Ability.ReturnAllThoughts";
                icon = "cultiway/icons/artifact_atoms/soul_core";
                return;
            case ControlArtifact:
                key = "Cultiway.Yuanshen.Ability.ControlArtifact";
                icon = "cultiway/icons/artifact_atoms/spirit_ding";
                return;
            case ReleaseArtifact:
                key = "Cultiway.Yuanshen.Ability.ReleaseArtifact";
                icon = "cultiway/icons/artifact_atoms/soul_binding_script";
                return;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(entryId), entryId, "未知分念管理条目。");
        }
    }
}
