using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>用人物魂魄恢复属性逐步解开神魂创伤锁定份额。</summary>
public sealed class YuanshenMindRecoverySystem
    : QuerySystem<ActorBinder, YuanshenRuntimeState>
{
    /// <summary>建立只处理存活人物的运行状态查询。</summary>
    public YuanshenMindRecoverySystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    /// <summary>按整秒恢复份额，并在命魂在外时同步节点完整度。</summary>
    protected override void OnUpdate()
    {
        float deltaTime = Mathf.Max(0f, Tick.deltaTime);
        Query.ForEachEntity((ref ActorBinder binder, ref YuanshenRuntimeState runtime, Entity _) =>
        {
            Actor owner = binder.Actor;
            if (owner == null || owner.isRekt() || !owner.isAlive() || runtime.injury_locked_share <= 0f) return;
            ActorExtend actor = owner.GetExtend();
            if (!CanRecover(actor, in runtime, out Actor soulCarrier)) return;

            runtime.recovery_elapsed += deltaTime;
            if (runtime.recovery_elapsed < 1f) return;
            float elapsed = Mathf.Floor(runtime.recovery_elapsed);
            runtime.recovery_elapsed -= elapsed;
            float maxSoul = Mathf.Max(1f, owner.stats[WorldboxGame.BaseStats.MaxSoul.id]);
            float soulRegen = Mathf.Max(0f, owner.stats[WorldboxGame.BaseStats.SoulRegen.id]);
            float stageScale = actor.TryGetComponent(out Yuanshen yuanshen)
                ? 1f + Mathf.Clamp(yuanshen.stage, 0, 9) * 0.05f
                : 1f;
            float rate = (0.15f + soulRegen / maxSoul * 1.5f) * stageScale;
            rate *= YuanshenAnchorNetworkService.ResolveRecoveryMultiplier(actor);
            if (runtime.IsOutside) rate *= 0.5f;
            float recovered = Mathf.Min(runtime.injury_locked_share, rate * elapsed);
            if (recovered <= 0f) return;

            runtime.injury_locked_share = Mathf.Max(0f, runtime.injury_locked_share - recovered);
            runtime.main_soul_share += recovered;
            if (soulCarrier != null)
            {
                ref YuanshenSoulCarrierState state = ref soulCarrier.GetExtend()
                    .GetComponent<YuanshenSoulCarrierState>();
                float allocated = Mathf.Max(0f, state.mind_share + state.locked_share);
                state.mind_share += recovered;
                state.locked_share = Mathf.Max(0f, state.locked_share - recovered);
                float integrityGain = allocated > 0f
                    ? state.maximum_integrity * recovered / allocated
                    : 0f;
                state.current_integrity = Mathf.Min(
                    state.maximum_integrity,
                    state.current_integrity + integrityGain);
                YuanshenTravelService.SynchronizeCarrierHealth(soulCarrier, in state);
            }
            YuanshenTravelService.NotifyMindStateChanged(actor);
        });
    }

    /// <summary>只有命魂在体，或外部节点停止任务后，才允许自然稳定创伤。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="runtime">人物当前元神运行状态。</param>
    /// <param name="soulCarrier">返回可同步完整度的临时命魂人物。</param>
    /// <returns>本帧允许恢复时返回真。</returns>
    private static bool CanRecover(
        ActorExtend actor,
        in YuanshenRuntimeState runtime,
        out Actor soulCarrier)
    {
        soulCarrier = null;
        if (!runtime.IsOutside) return true;
        if (!YuanshenTravelService.TryGetSoulCarrier(actor, out Actor carrier) ||
            !carrier.GetExtend().TryGetComponent(out YuanshenSoulCarrierState state) ||
            state.action != YuanshenSoulCarrierAction.Idle)
            return false;
        soulCarrier = carrier;
        return true;
    }
}
