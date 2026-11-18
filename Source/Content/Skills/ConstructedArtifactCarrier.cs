using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>标记构造能力对真实法宝的本次借用，旧主动能力不能同时接管。</summary>
internal struct ConstructedCarrierSession : IComponent
{
    /// <summary>通过立体双门后的真实模型朝向；没有转移时沿原显示摆放。</summary>
    internal Quaternion? MappedRotation;
}

/// <summary>借用原法宝及原显示，只向共同数值执行提供实际载体姿态。</summary>
internal sealed class ConstructedArtifactCarrier : IDisposable
{
    /// <summary>实际控制者及其当时身份，人物池复用后不延续旧借用。</summary>
    private readonly ActorExtend owner;
    /// <summary>控制者的稳定人物编号。</summary>
    private readonly long ownerId;
    /// <summary>原法宝与本次唯一执行身份，不保存临时整数编号。</summary>
    internal readonly Entity Artifact;
    /// <summary>沿已有世界本体关系取得的独占执行。</summary>
    private readonly Entity execution;

    /// <summary>记录已取得的真实借用。</summary>
    /// <param name="owner">实际人物。</param><param name="artifact">原法宝。</param><param name="execution">实际执行。</param>
    private ConstructedArtifactCarrier(ActorExtend owner, Entity artifact, Entity execution)
    { this.owner = owner; ownerId = owner.Base.id; Artifact = artifact; this.execution = execution; }

    /// <summary>从本人实际装备逐个公开真实承载者，能力知识本身不绑定某次实体编号。</summary>
    /// <param name="owner">已学会能力的人物。</param>
    internal static IEnumerable<Entity> Equipped(ActorExtend owner)
    {
        var relations = owner.E.GetRelations<EquippedArtifactRelation>();
        var result = new List<Entity>();
        for (int i = 0; i < relations.Length; i++)
            if (relations[i].artifact.IsAvailable() && relations[i].state != ArtifactControlState.Cold)
                result.Add(relations[i].artifact);
        return result;
    }

    /// <summary>只接受仍由本人实际控制、可显化且没有其他主体控制的原法宝。</summary>
    /// <param name="owner">当前持有者。</param><param name="artifact">实际选择。</param>
    internal static bool CanBorrow(ActorExtend owner, Entity artifact) => Controlled(owner, artifact) &&
        !artifact.HasComponent<SkillExecutionBodyLease>() &&
        !ArtifactAbilityLifecycle.HasOtherActiveAbility(artifact, null) &&
        ArtifactAppearanceRenderer.TryGetSpatialPose(artifact, out _);

    /// <summary>查询新构造借用，供原法宝主动入口保持同一独占规则。</summary>
    /// <param name="artifact">原法宝。</param>
    internal static bool IsBorrowed(Entity artifact) => !artifact.IsNull &&
        artifact.TryGetComponent(out SkillExecutionBodyLease lease) && !lease.execution.IsNull &&
        lease.execution.HasComponent<ConstructedCarrierSession>();

    /// <summary>两套视图都从同一法宝读取本次借用的空间朝向。</summary>
    /// <param name="artifact">当前显示的原法宝。</param>
    /// <param name="rotation">未转移时回到原显示朝向。</param>
    internal static Quaternion Heading(Entity artifact, Quaternion rotation) =>
        artifact.TryGetComponent(out SkillExecutionBodyLease lease) && !lease.execution.IsNull &&
        lease.execution.TryGetComponent(out ConstructedCarrierSession session)
            ? session.MappedRotation ?? rotation : rotation;

    /// <summary>在实际支付前取得原本体，转换旧随身坐标时保留当前真实空间位置。</summary>
    /// <param name="owner">实际控制者。</param><param name="artifact">本人真实装备。</param>
    internal static ConstructedArtifactCarrier TryAcquire(ActorExtend owner, Entity artifact)
    {
        if (!CanBorrow(owner, artifact)) return null;
        ArtifactAppearanceRenderer.TryGetSpatialPose(artifact, out SpatialVisualMeshInstance before);
        Entity execution = ModClass.I.W.CreateEntity(new SkillExecution(), new SkillExecutionWithoutBody(),
            new ConstructedCarrierSession());
        if (!SkillExecutionLifecycle.TryBorrowBody(execution, artifact))
        { SkillExecutionLifecycle.RequestEnd(execution); return null; }
        // 随身布局与独立本体的旧存储坐标不同；只转换表示，法宝不在取得控制时跳动。
        ArtifactAppearanceRenderer.TryGetSpatialPose(artifact, out SpatialVisualMeshInstance after);
        Vector3 shift = (Vector3)ConstructedAbilityService.CarrierFrame(before).GetColumn(3) -
            (Vector3)ConstructedAbilityService.CarrierFrame(after).GetColumn(3);
        artifact.GetComponent<Position>().value += new Vector3(shift.x, shift.y + shift.z, shift.z);
        return new ConstructedArtifactCarrier(owner, artifact, execution);
    }

    /// <summary>每次共同采集确认原身份、实际控制关系和本次独占凭据。</summary>
    internal bool IsValid => owner.Base.data?.id == ownerId && owner.Base.isAlive() &&
        Controlled(owner, Artifact) && !execution.IsNull &&
        execution.TryGetComponent(out SkillExecution state) && !state.end_requested &&
        Artifact.TryGetComponent(out SkillExecutionBodyLease lease) && lease.execution == execution &&
        ArtifactAppearanceRenderer.TryGetSpatialPose(Artifact, out _);

    /// <summary>只读取真实模型的当前空间位置和朝向，原法宝继续由原显示维护。</summary>
    internal Matrix4x4 Pose
    {
        get
        {
            ArtifactAppearanceRenderer.TryGetSpatialPose(Artifact, out SpatialVisualMeshInstance pose);
            return ConstructedAbilityService.CarrierFrame(pose);
        }
    }

    /// <summary>将已确认的出口姿态写回原本体；仍沿原借用和显示维护。</summary>
    /// <param name="point">出口实际世界点。</param><param name="rotation">出口真实模型朝向。</param>
    internal Matrix4x4 PlaceMapped(Vector3 point, Quaternion rotation)
    {
        execution.GetComponent<ConstructedCarrierSession>().MappedRotation = rotation;
        ref Position stored = ref Artifact.GetComponent<Position>();
        stored.value = new Vector3(point.x, point.y, 0f);
        float atGround = Pose.GetColumn(3).z;
        float z = point.z - atGround;
        stored.value = new Vector3(point.x, point.y + z, z);
        return Pose;
    }

    /// <summary>解除本次控制；原物件、外观、装备关系及所有权均保留。</summary>
    public void Dispose()
    {
        if (execution.IsNull) return;
        bool owns = !Artifact.IsNull && Artifact.TryGetComponent(out SkillExecutionBodyLease lease) &&
            lease.execution == execution;
        SpatialVisualMeshInstance before = default;
        bool sampled = owns && ArtifactAppearanceRenderer.TryGetSpatialPose(Artifact, out before);
        SkillExecutionLifecycle.ReleaseBodies(execution);
        if (sampled && ArtifactAppearanceRenderer.UsesFollowerLayout(Artifact) &&
            ArtifactAppearanceRenderer.TryGetSpatialPose(Artifact, out SpatialVisualMeshInstance after))
        {
            Vector3 shift = (Vector3)ConstructedAbilityService.CarrierFrame(before).GetColumn(3) -
                (Vector3)ConstructedAbilityService.CarrierFrame(after).GetColumn(3);
            Artifact.GetComponent<Position>().value += new Vector3(shift.x, shift.z, shift.y);
        }
        SkillExecutionLifecycle.RequestEnd(execution);
    }

    /// <summary>实际装备和调度仍属于当前人物，冷待命或移除后立即失去资格。</summary>
    /// <param name="owner">实际人物。</param><param name="artifact">需要确认的原物件。</param>
    private static bool Controlled(ActorExtend owner, Entity artifact)
    {
        if (owner.E.IsNull || !artifact.IsAvailable() || !artifact.HasComponent<Artifact>() ||
            !artifact.HasComponent<ArtifactManifestation>() ||
            !artifact.TryGetComponent(out ArtifactAbilityRuntime runtime) || runtime.controller != owner.E) return false;
        bool carried = false;
        var inventory = owner.E.GetRelations<InventoryRelation>();
        for (int i = 0; i < inventory.Length; i++)
            if (inventory[i].item == artifact) { carried = true; break; }
        if (!carried) return false;
        var relations = owner.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
            if (relations[i].artifact == artifact) return relations[i].state != ArtifactControlState.Cold;
        return false;
    }

}
