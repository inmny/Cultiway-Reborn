using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>把已祭炼法器绑定到普通分念，并提供统一远程世界起点。</summary>
public static class ArtifactYuanshenControlService
{
    /// <summary>把当前聚焦节点绑定到下一件未被其他节点控制的已装备法器。</summary>
    /// <param name="actor">法器祭炼者和资源所有者。</param>
    /// <returns>找到合法节点与法器并完成绑定时返回真。</returns>
    public static bool TryBindNextEquippedArtifact(ActorExtend actor)
    {
        if (actor == null || !actor.TryGetComponent(out Yuanshen yuanshen) || yuanshen.stage < 4 ||
            !YuanshenThoughtService.TryGetFocused(actor, out YuanshenNodeHandle handle, out _))
            return false;

        Entity best = default;
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (var i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            Entity artifact = relation.artifact;
            if (artifact.IsNull || relation.state is ArtifactControlState.Cold or ArtifactControlState.Ready ||
                !artifact.TryGetComponent(out ArtifactAttunement attunement) ||
                attunement.owner_actor_id != actor.Base.data.id ||
                artifact.TryGetComponent(out ArtifactYuanshenControl remote) && remote.owner_actor_id != 0L)
                continue;
            if (best.IsNull || artifact.Id < best.Id) best = artifact;
        }
        if (best.IsNull) return false;
        ReleaseNodeArtifact(actor, handle);
        var control = new ArtifactYuanshenControl
        {
            owner_actor_id = actor.Base.data.id,
            node = handle
        };
        if (best.HasComponent<ArtifactYuanshenControl>())
            best.GetComponent<ArtifactYuanshenControl>() = control;
        else
            best.AddComponent(control);
        if (YuanshenNodeLockService.TryResolve(handle, out Entity node))
        {
            var nextTask = new YuanshenNodeTask
            {
                kind = YuanshenNodeTaskKind.ControlArtifact,
                artifact_entity_id = best.Id,
                started_at = World.world?.getCurWorldTime() ?? 0d
            };
            if (node.HasComponent<YuanshenNodeTask>()) node.GetComponent<YuanshenNodeTask>() = nextTask;
            else node.AddComponent(nextTask);
        }
        return true;
    }

    /// <summary>解除当前聚焦节点正在执行的远程控宝任务。</summary>
    /// <param name="actor">法器祭炼者。</param>
    /// <returns>至少解除一件法器时返回真。</returns>
    public static bool ReleaseFocusedArtifact(ActorExtend actor)
    {
        return actor != null && YuanshenThoughtService.TryGetFocused(actor, out YuanshenNodeHandle handle, out _) &&
               ReleaseNodeArtifact(actor, handle);
    }

    /// <summary>解析法器当前是否由一枚有效的所属人物节点远程控制。</summary>
    /// <param name="controller">法器原人物控制者。</param>
    /// <param name="artifact">法器实体。</param>
    /// <param name="origin">返回节点世界起点。</param>
    /// <returns>祭炼归属、节点身份和位置均有效时返回真。</returns>
    public static bool TryResolveOrigin(Actor controller, Entity artifact, out Vector3 origin)
    {
        origin = default;
        if (controller == null || artifact.IsNull ||
            !artifact.TryGetComponent(out ArtifactYuanshenControl remote) ||
            remote.owner_actor_id != controller.data.id ||
            !artifact.TryGetComponent(out ArtifactAttunement attunement) ||
            attunement.owner_actor_id != controller.data.id ||
            !YuanshenNodeLockService.TryResolve(remote.node, out Entity node) ||
            !node.TryGetComponent(out Position position))
            return false;
        origin = position.value;
        return true;
    }

    /// <summary>返回法器远程节点位置，未远程控制时返回人物位置。</summary>
    /// <param name="controller">法器控制人物。</param>
    /// <param name="artifact">法器实体。</param>
    /// <returns>能力和表现使用的世界起点。</returns>
    public static Vector3 ResolveOrigin(Actor controller, Entity artifact)
    {
        return TryResolveOrigin(controller, artifact, out Vector3 origin)
            ? origin
            : controller.GetSimPos();
    }

    /// <summary>节点失效时清除某件法器上残留的远程控制状态。</summary>
    /// <param name="artifact">需要校验的法器。</param>
    public static void CleanupInvalid(Entity artifact)
    {
        if (artifact.IsNull || !artifact.TryGetComponent(out ArtifactYuanshenControl remote)) return;
        Actor owner = World.world?.units?.get(remote.owner_actor_id);
        if (owner != null && !owner.isRekt() && TryResolveOrigin(owner, artifact, out _)) return;
        artifact.RemoveComponent<ArtifactYuanshenControl>();
    }

    /// <summary>解除一枚节点对已装备法器的控制关系。</summary>
    /// <param name="actor">法器祭炼者。</param>
    /// <param name="handle">节点稳定句柄。</param>
    /// <returns>至少清理一件法器时返回真。</returns>
    public static bool ReleaseNodeArtifact(ActorExtend actor, YuanshenNodeHandle handle)
    {
        bool changed = false;
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (var i = 0; i < relations.Length; i++)
        {
            Entity artifact = relations[i].artifact;
            if (!artifact.TryGetComponent(out ArtifactYuanshenControl remote) || remote.node != handle) continue;
            artifact.RemoveComponent<ArtifactYuanshenControl>();
            changed = true;
        }
        if (YuanshenNodeLockService.TryResolve(handle, out Entity node) &&
            node.TryGetComponent(out YuanshenNodeTask current) &&
            current.kind == YuanshenNodeTaskKind.ControlArtifact)
        {
            ref YuanshenNodeTask task = ref node.GetComponent<YuanshenNodeTask>();
            task = new YuanshenNodeTask
            {
                kind = YuanshenNodeTaskKind.Idle,
                point = node.TryGetComponent(out Position position) ? position.v2 : default,
                started_at = World.world?.getCurWorldTime() ?? 0d
            };
        }
        return changed;
    }
}
