using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>管理人物唯一的本命法器备用锚点和明确归返位置。</summary>
public static class YuanshenArtifactAnchorService
{
    /// <summary>法器锚点允许的扩展牵引距离。</summary>
    public const float AnchorTetherDistance = 240f;

    /// <summary>致命转移后再次建立备用锚点需要等待的时间。</summary>
    public const double RescueRebindCooldown = Cultiway.Const.TimeScales.SecPerYear;

    /// <summary>当前世界进程内下一枚不重复绑定令牌。</summary>
    private static int nextBindingToken = 1;

    /// <summary>把下一件已完全祭炼的装备法器设为人物唯一备用锚点。</summary>
    /// <param name="actor">法器祭炼者。</param>
    /// <returns>元神七层、法器归属和祭炼程度均满足时返回真。</returns>
    public static bool TryBindNext(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 7) return false;
        if (actor.TryGetComponent(out YuanshenArtifactRescueCooldown cooldown))
        {
            if (cooldown.expires_at > Now) return false;
            actor.E.RemoveComponent<YuanshenArtifactRescueCooldown>();
        }
        int currentId = actor.TryGetComponent(out YuanshenArtifactAnchorState current)
            ? current.artifact_entity_id
            : 0;
        Entity best = default;
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (var i = 0; i < relations.Length; i++)
        {
            Entity artifact = relations[i].artifact;
            if (artifact.IsNull || artifact.Id == currentId || !artifact.HasComponent<Artifact>() ||
                !artifact.TryGetComponent(out ArtifactAttunement attunement) ||
                attunement.owner_actor_id != actor.Base.data.id || attunement.mastery < 99.999f)
                continue;
            if (best.IsNull || artifact.Id < best.Id) best = artifact;
        }
        if (best.IsNull && currentId > 0) best = ModClass.I.W.GetEntityById(currentId);
        if (best.IsNull || !ArtifactControlRules.SetLifeBound(best, actor.Base.data.id, true)) return false;
        if (currentId > 0)
            RemoveBinding(ModClass.I.W.GetEntityById(currentId), actor.Base.data.id, current.generation);
        int token = NextBindingToken();
        if (best.HasComponent<YuanshenArtifactAnchorBinding>())
            best.GetComponent<YuanshenArtifactAnchorBinding>() = new YuanshenArtifactAnchorBinding
            {
                owner_actor_id = actor.Base.data.id,
                token = token
            };
        else
            best.AddComponent(new YuanshenArtifactAnchorBinding
            {
                owner_actor_id = actor.Base.data.id,
                token = token
            });
        actor.GetOrAddComponent<YuanshenArtifactAnchorState>() = new YuanshenArtifactAnchorState
        {
            artifact_entity_id = best.Id,
            generation = token,
            bound_at = Now
        };
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
        return true;
    }

    /// <summary>解除人物当前备用锚点，不解除法器已完成的本命祭炼。</summary>
    /// <param name="actor">锚点所属人物。</param>
    /// <returns>人物原本持有锚点时返回真。</returns>
    public static bool Unbind(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenArtifactAnchorState anchor)) return false;
        RemoveBinding(ModClass.I.W.GetEntityById(anchor.artifact_entity_id), actor.Base.data.id, anchor.generation);
        actor.E.RemoveComponent<YuanshenArtifactAnchorState>();
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
        return true;
    }

    /// <summary>严格解析人物当前本命法器锚点及其世界位置。</summary>
    /// <param name="actor">锚点所属人物。</param>
    /// <param name="artifact">返回有效法器实体。</param>
    /// <param name="position">返回法器实际或携带者位置。</param>
    /// <returns>法器、祭炼归属、本命状态和位置均有效时返回真。</returns>
    public static bool TryResolve(
        ActorExtend actor,
        out Entity artifact,
        out Vector3 position)
    {
        artifact = default;
        position = default;
        if (actor == null || !actor.TryGetComponent(out YuanshenArtifactAnchorState anchor) ||
            anchor.artifact_entity_id <= 0) return false;
        Entity candidate = ModClass.I.W.GetEntityById(anchor.artifact_entity_id);
        if (candidate.IsNull || !candidate.HasComponent<Artifact>() ||
            !candidate.TryGetComponent(out YuanshenArtifactAnchorBinding binding) ||
            binding.owner_actor_id != actor.Base.data.id || binding.token != anchor.generation ||
            !candidate.TryGetComponent(out ArtifactAttunement attunement) ||
            attunement.owner_actor_id != actor.Base.data.id || !attunement.life_bound ||
            !TryResolveArtifactPosition(candidate, out position)) return false;
        artifact = candidate;
        return true;
    }

    /// <summary>无身元神主动回到仍有效的本命法器位置。</summary>
    /// <param name="actor">无身元神人物。</param>
    /// <returns>存在有效锚点与地面位置并完成移动时返回真。</returns>
    public static bool TryRestAtAnchor(ActorExtend actor)
    {
        if (!YuanshenLifecycleService.IsBodiless(actor) || !TryResolve(actor, out _, out Vector3 position))
            return false;
        WorldTile tile = World.world.GetTileSimple(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        if (tile == null) return false;
        actor.Base.cancelAllBeh();
        actor.Base.clearAttackTarget();
        actor.Base.clearTileTarget();
        actor.Base.spawnOn(tile, 0f);
        return true;
    }

    /// <summary>消耗当前备用锚点完成一次致命命魂转移，并进入重新绑定冷却。</summary>
    /// <param name="actor">遭遇致命魂伤的人物。</param>
    /// <param name="position">返回法器当前有效位置。</param>
    /// <returns>锚点在提交瞬间仍有效并已被消耗时返回真。</returns>
    public static bool TryConsumeFatalRescue(ActorExtend actor, out Vector3 position)
    {
        position = default;
        if (actor == null || !actor.TryGetComponent(out YuanshenArtifactAnchorState anchor) ||
            !TryResolve(actor, out Entity artifact, out position)) return false;
        RemoveBinding(artifact, actor.Base.data.id, anchor.generation);
        actor.E.RemoveComponent<YuanshenArtifactAnchorState>();
        actor.GetOrAddComponent<YuanshenArtifactRescueCooldown>() = new YuanshenArtifactRescueCooldown
        {
            expires_at = Now + RescueRebindCooldown
        };
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
        return true;
    }

    /// <summary>判断一个点是否位于肉身或备用法器锚点的牵引范围。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="point">待检查位置。</param>
    /// <returns>任一合法锚点覆盖该点时返回真。</returns>
    public static bool IsWithinTether(ActorExtend actor, Vector2 point)
    {
        if (actor?.Base == null) return false;
        if (Vector2.Distance(actor.Base.current_position, point) <= YuanshenTravelService.MaximumTetherDistance)
            return true;
        if (TryResolve(actor, out _, out Vector3 anchorPosition) &&
            Vector2.Distance(anchorPosition, point) <= AnchorTetherDistance) return true;
        return YuanshenAnchorNetworkService.IsWithinAuthorizedNetwork(actor, point);
    }

    /// <summary>牵引受阻时优先提供有效法器备用归返位置。</summary>
    /// <param name="actor">命魂所属人物。</param>
    /// <param name="preferArtifact">是否优先使用法器锚点。</param>
    /// <param name="position">返回归返位置。</param>
    /// <returns>人物或法器至少有一处有效位置时返回真。</returns>
    public static bool TryResolveReturnPosition(
        ActorExtend actor,
        bool preferArtifact,
        out Vector3 position)
    {
        if (preferArtifact && TryResolve(actor, out _, out position)) return true;
        if (actor?.Base != null && !actor.Base.isRekt())
        {
            position = actor.Base.GetSimPos();
            return true;
        }
        return TryResolve(actor, out _, out position);
    }

    /// <summary>锚点失效时清除绑定并让人物承受一次有限神魂创伤。</summary>
    /// <param name="actor">锚点所属人物。</param>
    /// <returns>检测并清理了失效锚点时返回真。</returns>
    public static bool BreakInvalidAnchor(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out YuanshenArtifactAnchorState anchor) ||
            TryResolve(actor, out _, out _)) return false;
        RemoveBinding(ModClass.I.W.GetEntityById(anchor.artifact_entity_id), actor.Base.data.id, anchor.generation);
        actor.E.RemoveComponent<YuanshenArtifactAnchorState>();
        CombatStatusEffects.ApplyStatus(
            actor.Base,
            StatusEffects.SoulTrauma,
            Cultiway.Const.TimeScales.SecPerMonth,
            actor.Base);
        YuanshenTravelService.LockMainMindShare(actor, 10f);
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
        return true;
    }

    /// <summary>读取法器显化位置，未显化时读取明确装备携带者位置。</summary>
    private static bool TryResolveArtifactPosition(Entity artifact, out Vector3 position)
    {
        if (artifact.TryGetComponent(out Position manifested))
        {
            position = manifested.value;
            return true;
        }
        foreach (Entity owner in artifact.GetIncomingLinks<EquippedArtifactRelation>().Entities)
        {
            if (!owner.TryGetComponent(out ActorBinder binder) || binder.Actor == null || binder.Actor.isRekt())
                continue;
            position = binder.Actor.GetSimPos();
            return true;
        }
        position = default;
        return false;
    }

    /// <summary>删除仍与指定人物和令牌一致的法器侧绑定。</summary>
    /// <param name="artifact">待清理法器实体。</param>
    /// <param name="ownerActorId">预期人物编号。</param>
    /// <param name="token">预期绑定令牌。</param>
    private static void RemoveBinding(Entity artifact, long ownerActorId, int token)
    {
        if (artifact.IsNull || !artifact.TryGetComponent(out YuanshenArtifactAnchorBinding binding) ||
            binding.owner_actor_id != ownerActorId || binding.token != token) return;
        artifact.RemoveComponent<YuanshenArtifactAnchorBinding>();
    }

    /// <summary>取得当前世界进程内下一枚正数绑定令牌。</summary>
    /// <returns>不会与上一枚相同的令牌。</returns>
    private static int NextBindingToken()
    {
        int token = nextBindingToken;
        nextBindingToken = nextBindingToken == int.MaxValue ? 1 : nextBindingToken + 1;
        return token;
    }

    /// <summary>当前世界时间。</summary>
    private static double Now => World.world?.getCurWorldTime() ?? 0d;
}
