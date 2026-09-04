using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进稳定化身在授权锚点旁的准备进度，并结算完成或取消。</summary>
public sealed class YuanshenAvatarPreparationSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>本轮失去锚点或心神的稳定化身准备。</summary>
    private readonly List<ActorExtend> cancelledAvatars = new();

    /// <summary>本轮到期的稳定化身准备。</summary>
    private readonly List<AvatarRequest> completedAvatars = new();

    /// <summary>先收集准备进度，再在查询外提交取消与完成。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        cancelledAvatars.Clear();
        completedAvatars.Clear();
        double now = World.world?.getCurWorldTime() ?? 0d;

        ModClass.I.W.Query<ActorBinder, YuanshenAvatarPreparationState>().ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenAvatarPreparationState preparation,
            Entity actorEntity) =>
        {
            Actor ownerBase = binder.Actor;
            if (ownerBase == null || ownerBase.isRekt() || !ownerBase.isAlive()) return;
            ActorExtend owner = ownerBase.GetExtend();
            if (!YuanshenAnchorNetworkService.TryGetUsableAuthorized(owner, preparation.anchor, out _, out _) ||
                !YuanshenAnchorNetworkService.IsPresenceAtAnchor(owner, preparation.anchor))
            {
                cancelledAvatars.Add(owner);
                return;
            }
            if (ownerBase.isJustAttacked() || ownerBase.has_attack_target)
            {
                preparation.last_updated_at = now;
                if (now - preparation.last_interrupted_at < Cultiway.Const.TimeScales.SecPerMonth) return;
                preparation.progress *= 0.99d;
                preparation.last_interrupted_at = now;
                return;
            }
            double elapsed = Mathf.Clamp(
                (float)(now - preparation.last_updated_at),
                0f,
                Cultiway.Const.TimeScales.SecPerMonth);
            if (elapsed <= 0d || !owner.HasCultisys<Xian>()) return;
            float required = preparation.required_wakan *
                             (float)(elapsed / YuanshenAdvancedNodeService.AvatarPreparationDuration);
            if (!WakanResourceService.TrySpend(owner, required))
            {
                preparation.last_updated_at = now;
                return;
            }
            preparation.paid_wakan += required;
            preparation.progress += elapsed;
            preparation.last_updated_at = now;
            if (preparation.progress >= YuanshenAdvancedNodeService.AvatarPreparationDuration &&
                preparation.paid_wakan + 0.01f >= preparation.required_wakan)
                completedAvatars.Add(new AvatarRequest(owner, preparation));
        });

        for (var i = 0; i < cancelledAvatars.Count; i++)
            YuanshenAdvancedNodeService.CancelAvatarPreparation(cancelledAvatars[i]);
        for (var i = 0; i < completedAvatars.Count; i++)
        {
            AvatarRequest request = completedAvatars[i];
            if (!YuanshenAdvancedNodeService.CompleteAvatarPreparation(request.Actor, request.Preparation))
                YuanshenAdvancedNodeService.CancelAvatarPreparation(request.Actor);
        }
    }

    /// <summary>世界切换时丢弃尚未提交的帧内请求。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        cancelledAvatars.Clear();
        completedAvatars.Clear();
    }

    /// <summary>稳定化身准备完成请求。</summary>
    private readonly struct AvatarRequest
    {
        /// <summary>原人物。</summary>
        public readonly ActorExtend Actor;

        /// <summary>冻结准备状态。</summary>
        public readonly YuanshenAvatarPreparationState Preparation;

        /// <summary>创建化身完成请求。</summary>
        public AvatarRequest(ActorExtend actor, YuanshenAvatarPreparationState preparation)
        {
            Actor = actor;
            Preparation = preparation;
        }
    }
}
