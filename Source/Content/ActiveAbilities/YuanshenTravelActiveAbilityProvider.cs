using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.ActiveAbilities;

/// <summary>把三种定点神游和命魂归一接入统一主动能力栏。</summary>
internal sealed class YuanshenTravelActiveAbilityProvider : IActiveAbilityProvider
{
    /// <summary>主动能力系统使用的稳定来源编号。</summary>
    public const string ProviderId = "content.yuanshen_travel";

    /// <summary>守一神游条目编号。</summary>
    private const string GuardedTravel = "travel_guarded";

    /// <summary>均衡神游条目编号。</summary>
    private const string BalancedTravel = "travel_balanced";

    /// <summary>尽出神游条目编号。</summary>
    private const string FullTravel = "travel_full";

    /// <summary>归一条目编号。</summary>
    private const string Return = "return";

    /// <summary>返回稳定来源编号。</summary>
    public string Id => ProviderId;

    /// <summary>为有效化神人物枚举三种神游姿态，并在命魂离体时加入归一。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="output">接收能力句柄的集合。</param>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!YuanshenTravelService.CanTravel(caster)) return;
        output.Add(new ActiveAbilityHandle(Id, caster.E, GuardedTravel));
        output.Add(new ActiveAbilityHandle(Id, caster.E, BalancedTravel));
        output.Add(new ActiveAbilityHandle(Id, caster.E, FullTravel));
        if (YuanshenTravelService.TryGetSoulCarrier(caster, out _))
            output.Add(new ActiveAbilityHandle(Id, caster.E, Return));
    }

    /// <summary>元神神游只参与世界命令通道，不进入普通战斗技能选择。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <returns>句柄有效时返回世界通道。</returns>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsValid(caster, handle) ? ActiveAbilityChannel.World : ActiveAbilityChannel.None;
    }

    /// <summary>生成能力栏名称、图标、目标方式和移动约束。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <returns>玩家界面使用的稳定描述。</returns>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        ResolvePresentation(handle.EntryId, out string nameKey, out string iconPath);
        bool returning = handle.EntryId == Return;
        return new ActiveAbilityDescriptor(
            nameKey.Localize(),
            SpriteTextureLoader.getSprite(iconPath),
            ActiveAbilityChannel.World,
            returning ? ActiveAbilityTargetMode.Self : ActiveAbilityTargetMode.Point,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            returning ? SkillUseTargetRelation.Self : SkillUseTargetRelation.WorldTile);
    }

    /// <summary>返回能力栏当前是否允许点击。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <returns>当前可用状态。</returns>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!IsValid(caster, handle))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        if (handle.EntryId == Return)
            return YuanshenTravelService.TryGetSoulCarrier(caster, out _)
                ? ActiveAbilityControlState.Ready
                : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        return ActiveAbilityControlState.Ready;
    }

    /// <summary>检查能力是否能够进入选点或立即释放。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">准备阶段已有对象目标；神游不读取它。</param>
    /// <returns>人物和条目有效时返回真。</returns>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!IsValid(caster, handle)) return false;
        return handle.EntryId != Return || YuanshenTravelService.TryGetSoulCarrier(caster, out _);
    }

    /// <summary>检查明确地面坐标或归一命令是否有效。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">本次能力目标。</param>
    /// <returns>目标位于牵引范围或当前能够归一时返回真。</returns>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!IsValid(caster, handle)) return false;
        if (handle.EntryId == Return) return YuanshenTravelService.TryGetSoulCarrier(caster, out _);
        return YuanshenTravelService.IsWithinTether(caster, target.Position);
    }

    /// <summary>战斗规划器不自动选择世界神游命令。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">战斗目标。</param>
    /// <returns>固定返回零。</returns>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return 0;
    }

    /// <summary>世界神游没有普通战斗用途画像。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">战斗目标。</param>
    /// <returns>空战术画像。</returns>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        return default;
    }

    /// <summary>返回神游牵引上限；归一不需要选点距离。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">已有对象目标。</param>
    /// <returns>神游最大牵引距离或零。</returns>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return handle.EntryId == Return ? 0f : YuanshenTravelService.MaximumTetherDistance;
    }

    /// <summary>神游只选择单一落点，不提供范围预览。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <returns>固定返回零。</returns>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return 0f;
    }

    /// <summary>把玩家或脚本命令提交给唯一元神旅行服务。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">地面目标或自身目标。</param>
    /// <param name="origin">本次命令来源。</param>
    /// <returns>服务完整提交命令时返回真。</returns>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        if (handle.EntryId == Return) return YuanshenTravelService.RequestReturn(caster);
        return YuanshenTravelService.TryTravelTo(caster, target.Position, ResolveStance(handle.EntryId));
    }

    /// <summary>校验能力来源人物和条目编号。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">能力句柄。</param>
    /// <returns>句柄确实属于当前人物时返回真。</returns>
    private static bool IsValid(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return YuanshenTravelService.CanTravel(caster) &&
               handle.ProviderId == ProviderId && handle.Source == caster.E &&
               handle.EntryId is GuardedTravel or BalancedTravel or FullTravel or Return;
    }

    /// <summary>把神游条目编号映射到心神姿态。</summary>
    /// <param name="entryId">神游条目编号。</param>
    /// <returns>对应心神姿态。</returns>
    private static YuanshenTravelStance ResolveStance(string entryId)
    {
        return entryId switch
        {
            GuardedTravel => YuanshenTravelStance.Guarded,
            BalancedTravel => YuanshenTravelStance.Balanced,
            FullTravel => YuanshenTravelStance.FullRelease,
            _ => throw new System.ArgumentOutOfRangeException(nameof(entryId), entryId, "未知神游条目。")
        };
    }

    /// <summary>按条目编号取得本地化名称和现有图标路径。</summary>
    /// <param name="entryId">能力条目编号。</param>
    /// <param name="nameKey">返回名称本地化键。</param>
    /// <param name="iconPath">返回图标路径。</param>
    private static void ResolvePresentation(string entryId, out string nameKey, out string iconPath)
    {
        switch (entryId)
        {
            case GuardedTravel:
                nameKey = "Cultiway.Yuanshen.Ability.TravelGuarded";
                iconPath = "cultiway/icons/artifact_atoms/spirit_gathering_pattern";
                return;
            case BalancedTravel:
                nameKey = "Cultiway.Yuanshen.Ability.TravelBalanced";
                iconPath = "cultiway/icons/artifact_atoms/soul_core";
                return;
            case FullTravel:
                nameKey = "Cultiway.Yuanshen.Ability.TravelFull";
                iconPath = "cultiway/icons/achievements/chaos_nascent_soul";
                return;
            case Return:
                nameKey = "Cultiway.Yuanshen.Ability.Return";
                iconPath = "cultiway/icons/artifact_atoms/spirit_awakening_script";
                return;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(entryId), entryId, "未知神游条目。");
        }
    }
}
