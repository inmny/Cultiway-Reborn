using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 提供角色法器装备关系的操作和查询入口。
/// 调用者应传入有效的角色扩展和法器实体；库存转移导致的关系清理由 ArtifactEquipmentManager 负责。
/// </summary>
public static class ArtifactEquipmentTools
{
    /// <summary>
    /// 为角色装备其当前持有的可用法器，并立即重新调度整套法器。
    /// </summary>
    /// <param name="actor">法器的驾驭者。</param>
    /// <param name="artifact">需要装备的法器实体。</param>
    /// <param name="locked">是否锁定装备，防止自动规划器将其移除。</param>
    /// <param name="mode">法器装备后的调度模式。</param>
    /// <param name="priority">参与自动选择和运行调度的人工优先级。</param>
    /// <returns>满足法器类型、可用状态和持有关系时返回 true，否则返回 false。</returns>
    public static bool EquipArtifact(
        this ActorExtend actor,
        Entity artifact,
        bool locked = false,
        ArtifactEquipMode mode = ArtifactEquipMode.Automatic,
        int priority = 0)
    {
        // 装备入口只接受角色实际持有的已完成法器。
        if (!artifact.HasComponent<Artifact>() || !artifact.IsAvailable() ||
            !Carries(actor.E, artifact)) return false;

        long ownerId = actor.Base.data.id;
        // 原地完成炼制的法器不会再次触发库存添加事件，因此在此补充调度标记。
        if (!actor.E.HasComponent<ArtifactLoadoutState>()) actor.E.AddComponent(new ArtifactLoadoutState());

        // 装备会绑定当前祭炼者；手动重新装备同时恢复该法器的自动装备资格。
        ArtifactControlRules.BindAttunement(artifact, ownerId);
        ArtifactControlRules.SetAutoEquipDisabled(artifact, false);

        actor.E.AddRelation(new EquippedArtifactRelation
        {
            artifact = artifact,
            mode = mode,
            priority = priority,
            locked = locked,
        });
        // 只重新分配现有装备的运行状态，不在手动操作中触发自动增删装备。
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
        return true;
    }

    /// <summary>
    /// 移除角色与指定法器的装备关系，并立即重新调度剩余法器。
    /// </summary>
    /// <param name="actor">法器当前的驾驭者。</param>
    /// <param name="artifact">需要卸下的法器实体。</param>
    /// <param name="suppressAutoEquip">是否禁止自动规划器之后重新装备该法器。</param>
    /// <returns>法器原本已装备时返回 true，否则返回 false。</returns>
    public static bool UnequipArtifact(this ActorExtend actor, Entity artifact, bool suppressAutoEquip = false)
    {
        if (!actor.IsArtifactEquipped(artifact)) return false;

        // UI 主动卸下时记录抑制标记；库存转移等被动卸下不应永久禁止自动装备。
        if (suppressAutoEquip)
        {
            ArtifactControlRules.SetAutoEquipDisabled(artifact, true);
        }
        actor.E.RemoveRelation<EquippedArtifactRelation>(artifact);
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
        return true;
    }

    /// <summary>
    /// 修改已装备法器的调度模式，并立即刷新运行状态。
    /// </summary>
    /// <remarks>调用前必须保证指定法器已经装备。</remarks>
    public static void SetArtifactEquipMode(
        this ActorExtend actor,
        Entity artifact,
        ArtifactEquipMode mode)
    {
        ref EquippedArtifactRelation relation = ref actor.E
            .GetRelation<EquippedArtifactRelation, Entity>(artifact);
        relation.mode = mode;
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
    }

    /// <summary>
    /// 修改已装备法器的人工优先级，并立即刷新运行状态。
    /// </summary>
    /// <remarks>调用前必须保证指定法器已经装备。</remarks>
    public static void SetArtifactPriority(this ActorExtend actor, Entity artifact, int priority)
    {
        ref EquippedArtifactRelation relation = ref actor.E
            .GetRelation<EquippedArtifactRelation, Entity>(artifact);
        relation.priority = priority;
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
    }

    /// <summary>
    /// 设置已装备法器是否锁定，并立即刷新运行状态。
    /// 锁定法器不会被自动装备规划移除。
    /// </summary>
    /// <remarks>调用前必须保证指定法器已经装备。</remarks>
    public static void SetArtifactLocked(this ActorExtend actor, Entity artifact, bool locked)
    {
        ref EquippedArtifactRelation relation = ref actor.E
            .GetRelation<EquippedArtifactRelation, Entity>(artifact);
        relation.locked = locked;
        ArtifactLoadoutPlanner.Refresh(actor, false, 0f);
    }

    /// <summary>
    /// 尝试读取角色指向指定法器的装备关系。
    /// </summary>
    /// <param name="actor">需要查询的角色。</param>
    /// <param name="artifact">作为关系键的法器实体。</param>
    /// <param name="relation">查询成功时返回装备配置和最近调度状态。</param>
    /// <returns>存在对应装备关系时返回 true。</returns>
    public static bool TryGetArtifactEquipRelation(
        this ActorExtend actor,
        Entity artifact,
        out EquippedArtifactRelation relation)
    {
        relation = default;
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].artifact != artifact) continue;
            relation = relations[i];
            return true;
        }
        return false;
    }

    /// <summary>
    /// 判断角色是否已经装备指定法器。
    /// </summary>
    public static bool IsArtifactEquipped(this ActorExtend actor, Entity artifact)
    {
        return actor.TryGetArtifactEquipRelation(artifact, out _);
    }

    /// <summary>
    /// 判断角色是否存在任意法器装备关系。
    /// </summary>
    public static bool HasEquippedArtifacts(this ActorExtend actor)
    {
        return actor.E.GetRelations<EquippedArtifactRelation>().Length > 0;
    }

    /// <summary>
    /// 枚举角色当前装备的全部法器，不区分运行状态。
    /// </summary>
    public static IEnumerable<Entity> GetEquippedArtifacts(this ActorExtend actor)
    {
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            yield return relations[i].artifact;
        }
    }

    /// <summary>
    /// 枚举角色当前正在正常或超载运转的可用法器。
    /// 该查询用于后续法器效果系统判断哪些法器实际生效。
    /// </summary>
    public static IEnumerable<Entity> GetOperatingArtifacts(this ActorExtend actor)
    {
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            if (relation.state is not (ArtifactControlState.Operating or ArtifactControlState.Overloaded)) continue;
            if (relation.artifact.IsAvailable()) yield return relation.artifact;
        }
    }

    /// <summary>
    /// 获取角色最近一次法器调度生成的负荷和分念汇总状态。
    /// </summary>
    /// <remarks>调用前必须保证角色具有 ArtifactLoadoutState 组件。</remarks>
    public static ArtifactLoadoutState GetArtifactLoadoutState(this ActorExtend actor)
    {
        return actor.E.GetComponent<ArtifactLoadoutState>();
    }

    /// <summary>
    /// 判断物品是否通过库存关系由指定实体持有。
    /// </summary>
    private static bool Carries(Entity owner, Entity artifact)
    {
        var relations = owner.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == artifact) return true;
        }
        return false;
    }
}
