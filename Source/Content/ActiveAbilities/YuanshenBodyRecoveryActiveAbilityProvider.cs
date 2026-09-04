using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using strings;

namespace Cultiway.Content.ActiveAbilities;

/// <summary>把本命法器寄托、自愿承载、化神夺舍和本相塑体接入玩家能力栏。</summary>
internal sealed class YuanshenBodyRecoveryActiveAbilityProvider : IActiveAbilityProvider
{
    /// <summary>稳定来源编号。</summary>
    public const string ProviderId = "content.yuanshen_body_recovery";

    /// <summary>固定能力条目。</summary>
    private const string BindAnchor = "bind_anchor";
    private const string UnbindAnchor = "unbind_anchor";
    private const string RestAtAnchor = "rest_anchor";
    private const string OfferBody = "offer_body";
    private const string Possess = "possess";
    private const string AcceptBody = "accept_body";
    private const string CancelPossession = "cancel_possession";
    private const string Reconstruct = "reconstruct";
    private const string CancelReconstruction = "cancel_reconstruction";

    /// <summary>返回稳定来源编号。</summary>
    public string Id => ProviderId;

    /// <summary>按人物当前身体和元神状态提供身体恢复命令。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (caster == null || caster.Base == null || caster.Base.isRekt()) return;
        bool bodyless = YuanshenLifecycleService.IsBodiless(caster);
        if (caster.TryGetComponent(out Yuanshen yuanshen))
        {
            if (yuanshen.stage >= 7)
            {
                output.Add(new ActiveAbilityHandle(Id, caster.E, BindAnchor));
                if (caster.HasComponent<YuanshenArtifactAnchorState>())
                    output.Add(new ActiveAbilityHandle(Id, caster.E, UnbindAnchor));
            }
            if (bodyless)
            {
                if (caster.HasComponent<YuanshenPossessionState>())
                    output.Add(new ActiveAbilityHandle(Id, caster.E, CancelPossession));
                else
                {
                    output.Add(new ActiveAbilityHandle(Id, caster.E, Possess));
                    output.Add(new ActiveAbilityHandle(Id, caster.E, AcceptBody));
                }
                if (caster.HasComponent<YuanshenArtifactAnchorState>())
                    output.Add(new ActiveAbilityHandle(Id, caster.E, RestAtAnchor));
                if (yuanshen.stage >= 9)
                {
                    output.Add(new ActiveAbilityHandle(
                        Id,
                        caster.E,
                        caster.HasComponent<YuanshenReconstructionState>()
                            ? CancelReconstruction
                            : Reconstruct));
                }
            }
        }
        if (!bodyless && YuanshenBodyRecoveryService.IsEligibleHost(caster.Base))
            output.Add(new ActiveAbilityHandle(Id, caster.E, OfferBody));
    }

    /// <summary>全部命令使用世界能力通道。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) =>
        IsValid(caster, handle) ? ActiveAbilityChannel.World : ActiveAbilityChannel.None;

    /// <summary>生成固定本地化名称、图标和目标模式。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        ResolvePresentation(handle.EntryId, out string key, out string icon);
        bool objectTarget = handle.EntryId is OfferBody or Possess or AcceptBody;
        SkillUseTargetRelation relation = handle.EntryId is OfferBody or AcceptBody
            ? SkillUseTargetRelation.Friendly
            : handle.EntryId == Possess ? SkillUseTargetRelation.Hostile : SkillUseTargetRelation.Self;
        return new ActiveAbilityDescriptor(
            key.Localize(),
            SpriteTextureLoader.getSprite(icon),
            ActiveAbilityChannel.World,
            objectTarget ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Self,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            relation);
    }

    /// <summary>合法条目显示就绪，进行中状态由对应取消条目替代。</summary>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsValid(caster, handle)
            ? ActiveAbilityControlState.Ready
            : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
    }

    /// <summary>准备阶段只校验人物和句柄。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        IsValid(caster, handle);

    /// <summary>对象命令严格校验明确人物目标，其余命令由服务校验完整门槛。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!IsValid(caster, handle)) return false;
        if (handle.EntryId is not (OfferBody or Possess or AcceptBody)) return true;
        return target.Object != null && !target.Object.isRekt() && target.Object.isActor() &&
               target.Object.a != caster.Base;
    }

    /// <summary>普通战斗规划器不自动安排身体转移。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 0;

    /// <summary>身体恢复没有普通战斗画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target) => default;

    /// <summary>身体转移必须近距，其他命令为自用。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        handle.EntryId is OfferBody or Possess or AcceptBody
            ? YuanshenBodyRecoveryService.PossessionRange
            : 0f;

    /// <summary>身体恢复能力不使用面积目标。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;

    /// <summary>提交本命寄托、承载、夺舍或塑体命令。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        return handle.EntryId switch
        {
            BindAnchor => YuanshenArtifactAnchorService.TryBindNext(caster),
            UnbindAnchor => YuanshenArtifactAnchorService.Unbind(caster),
            RestAtAnchor => YuanshenArtifactAnchorService.TryRestAtAnchor(caster),
            OfferBody => YuanshenBodyRecoveryService.TryOfferBody(caster.Base, target.Object.a),
            Possess or AcceptBody => YuanshenBodyRecoveryService.TryStartPossession(caster, target.Object.a),
            CancelPossession => YuanshenBodyRecoveryService.CancelPossession(caster),
            Reconstruct => YuanshenBodyRecoveryService.TryStartReconstruction(caster),
            CancelReconstruction => YuanshenBodyRecoveryService.CancelReconstruction(caster, false),
            _ => false
        };
    }

    /// <summary>按人物状态校验固定条目。</summary>
    private static bool IsValid(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (caster == null || handle.ProviderId != ProviderId || handle.Source != caster.E) return false;
        bool bodyless = YuanshenLifecycleService.IsBodiless(caster);
        bool hasYuanshen = caster.TryGetComponent(out Yuanshen yuanshen);
        return handle.EntryId switch
        {
            BindAnchor => hasYuanshen && yuanshen.stage >= 7,
            UnbindAnchor => hasYuanshen && yuanshen.stage >= 7 && caster.HasComponent<YuanshenArtifactAnchorState>(),
            RestAtAnchor => bodyless && caster.HasComponent<YuanshenArtifactAnchorState>(),
            OfferBody => !bodyless && YuanshenBodyRecoveryService.IsEligibleHost(caster.Base),
            Possess or AcceptBody => bodyless && hasYuanshen &&
                                      !caster.HasComponent<YuanshenPossessionState>(),
            CancelPossession => bodyless && caster.HasComponent<YuanshenPossessionState>(),
            Reconstruct => bodyless && hasYuanshen && yuanshen.stage >= 9 &&
                           !caster.HasComponent<YuanshenReconstructionState>(),
            CancelReconstruction => bodyless && caster.HasComponent<YuanshenReconstructionState>(),
            _ => false
        };
    }

    /// <summary>按条目取得本地化名称与现有图标。</summary>
    private static void ResolvePresentation(string entryId, out string key, out string icon)
    {
        key = "Cultiway.Yuanshen.Ability." + entryId;
        icon = entryId switch
        {
            BindAnchor or UnbindAnchor or RestAtAnchor => "cultiway/icons/artifact_atoms/soul_binding_script",
            OfferBody or Possess or AcceptBody or CancelPossession =>
                "cultiway/icons/artifact_atoms/soul_mirror",
            Reconstruct or CancelReconstruction => "cultiway/icons/artifact_atoms/spirit_gathering_pattern",
            _ => "cultiway/icons/artifact_atoms/soul_core"
        };
    }
}
