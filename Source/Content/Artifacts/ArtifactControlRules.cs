using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Content.Utils;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

public static class ArtifactControlRules
{
    /// <summary>
    /// 计算神识对应的分念容量
    /// </summary>
    /// <param name="divineSense">神识</param>
    /// <returns>分念容量</returns>
    public static int GetThreadCapacity(float divineSense)
    {
        if (divineSense <= 0f) return 0;
        return 1 + Mathf.FloorToInt(Mathf.Log(1f + divineSense / 16f, 2f));
    }

    /// <summary>
    /// 计算法器各类情况的负荷（考虑法器祭炼等情况）
    /// </summary>
    /// <param name="artifact">法器</param>
    /// <param name="ownerActorId">祭炼者ID</param>
    /// <param name="preparedLoad">准备负荷</param>
    /// <param name="operatingLoad">运转负荷</param>
    /// <param name="threadCost">分念成本</param>
    public static void ResolveLoads(
        Entity artifact,
        long ownerActorId,
        out float preparedLoad,
        out float operatingLoad,
        out int threadCost)
    {
        ArtifactControlProfile profile = artifact.GetComponent<ArtifactControlProfile>();
        ArtifactAbilityDispatcher.ResolveControlContribution(
            artifact,
            out float abilityComplexity,
            out int abilityThreadCost);
        ItemLevel itemLevel = artifact.GetComponent<ItemLevel>();
        float rankMultiplier = Mathf.Pow(2f, itemLevel.Stage) * (1f + itemLevel.Level / 9f);
        float masteryMultiplier = 1.5f;
        if (artifact.TryGetComponent(out ArtifactAttunement attunement) &&
            attunement.owner_actor_id == ownerActorId)
        {
            masteryMultiplier = Mathf.Lerp(1.5f, 0.7f, attunement.mastery / 100f);
            if (attunement.life_bound) masteryMultiplier *= 0.65f;
        }

        operatingLoad = ArtifactSetting.DefaultOperatingLoad * rankMultiplier *
                        (profile.complexity + abilityComplexity) * masteryMultiplier;
        preparedLoad = operatingLoad * profile.prepared_load_ratio;
        threadCost = profile.autonomous ? 0 : profile.thread_cost + abilityThreadCost;
    }
    /// <summary>
    /// 绑定开始祭炼
    /// </summary>
    /// <param name="artifact">法器</param>
    /// <param name="ownerActorId">祭炼者ID</param>
    public static void BindAttunement(Entity artifact, long ownerActorId)
    {
        if (!artifact.HasComponent<ArtifactAttunement>())
        {
            artifact.AddComponent(new ArtifactAttunement { owner_actor_id = ownerActorId });
            return;
        }

        ref ArtifactAttunement attunement = ref artifact.GetComponent<ArtifactAttunement>();
        if (attunement.owner_actor_id == ownerActorId) return;
        attunement = new ArtifactAttunement { owner_actor_id = ownerActorId };
    }
    
    /// <summary>
    /// 设置法器是否能够自动装备
    /// </summary>
    /// <param name="artifact">法器</param>
    /// <param name="disabled">是否能够自动装备</param>
    public static void SetAutoEquipDisabled(Entity artifact, bool disabled)
    {
        ref ArtifactAttunement attunement = ref artifact.GetComponent<ArtifactAttunement>();
        attunement.auto_equip_disabled = disabled;
    }
    /// <summary>
    /// 判断法器是否能够自动装备
    /// </summary>
    /// <param name="artifact">法器</param>
    /// <param name="ownerActorId">祭炼者ID</param>
    /// <returns>是否自动装备</returns>
    public static bool IsAutoEquipDisabled(Entity artifact, long ownerActorId)
    {
        return artifact.TryGetComponent(out ArtifactAttunement attunement) &&
               attunement.owner_actor_id == ownerActorId &&
               attunement.auto_equip_disabled;
    }
    /// <summary>
    /// 推进祭炼进度
    /// </summary>
    /// <param name="artifact">法器</param>
    /// <param name="state">法器状态</param>
    /// <param name="elapsedSeconds">流逝时间</param>
    public static void AdvanceAttunement(
        Entity artifact,
        ArtifactControlState state,
        float elapsedSeconds)
    {
        ref ArtifactAttunement attunement = ref artifact.GetComponent<ArtifactAttunement>();
        attunement.mastery = Mathf.Min(100f, attunement.mastery + elapsedSeconds * state.GetAttunementRate());
    }
}
