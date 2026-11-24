using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Systems.Render;
using Cultiway.Content.Visuals;
using Cultiway.Content.Utils;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Visuals;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>读取地刺、真实武器与组合法宝的现有状态，提供两个视角共用的空间快照。</summary>
internal sealed class CombatSpatialVisualSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>当前显示帧的模型快照；没有自己的玩法时钟。</summary>
    internal static readonly List<SpatialVisualMeshInstance> Meshes = new();
    /// <summary>实际魔法护持身体。</summary>
    private readonly ArchetypeQuery<ActorBinder, Magic> mages;
    /// <summary>实际单帧物品的空间显示。</summary>
    private readonly ArchetypeQuery<Position, Rotation, AnimData, Scale> spriteBodies;
    private readonly ArchetypeQuery<ArtifactSpatialProjection> projections;
    /// <summary>已经显化的组合外观法宝。</summary>
    private readonly ArchetypeQuery<Position, Rotation, ArtifactAppearance, ArtifactManifestation> artifacts;
    /// <summary>原版几何视图的世界根节点。</summary>
    private readonly Transform root;
    /// <summary>复用到历史最大可见数量的二维视图。</summary>
    private SpatialNativeMeshView view;
    /// <summary>本帧经过原版镜头裁剪的同材质批次。</summary>
    private readonly List<SpatialVisualMeshInstance> nativeBatch = new();
    /// <summary>只登记读取查询，不增加施法、支付与命中入口。</summary>
    /// <param name="world">现有世界实体库。</param>
    internal CombatSpatialVisualSystem(EntityStore world)
    {
        QueryFilter filter = new();
        filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        artifacts = world.Query<Position, Rotation, ArtifactAppearance, ArtifactManifestation>(filter);
        mages = world.Query<ActorBinder, Magic>(filter);
        QueryFilter spriteFilter = new();
        spriteFilter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        spriteFilter.AllTags(Tags.Get<TagSpatialSpriteBody>());
        spriteBodies = world.Query<Position, Rotation, AnimData, Scale>(spriteFilter);
        projections = world.Query<ArtifactSpatialProjection>(filter);
        root = new GameObject("combat_spatial_visuals").transform;
        root.SetParent(World.world.transform, false);
    }

    /// <summary>先采样原有状态，再绘制需要替换旧整张动画的主体。</summary>
    protected override void OnUpdateGroup()
    {
        Compatibility.Worldfall.WorldfallVisualBridge.InstallIfNeeded();
        CombatVisualAudio.UpdateVolume();
        NativeFireGlow.Ensure();
        Meshes.Clear();
        NativeProjectileFlights.Prune();
        bool drawNative = MapBox.isRenderGameplay();
        if (drawNative || Compatibility.Worldfall.WorldfallVisualBridge.IsInstalled)
        {
            int detailBudget = 384;
            BattleSpellVisualRuntime.Append(Meshes, ref detailBudget);
            GoldenBellSpatialVisualCue.AppendMeshes(Meshes, World.world.getCurWorldTime(), ref detailBudget);
            ArtifactSpatialVisualRuntime.Append(Meshes, ref detailBudget);
            Meshes.AddRange(ArtifactSectManifestationSystem.Veins);
            MagicShieldVfx.Append(mages, Meshes);
            TalismanVfxManager.Append(Meshes);
            Meshes.AddRange(Core.Systems.Render.RenderStatusAnimationSystem.Meshes);
            SampleSpriteBodies();
            SampleProjections();
            WeaponControl.WeaponControlFlight.AppendSpatial(Meshes);
            SpatialVisualEffects.Append(Meshes, ref detailBudget);
            GroundWeaponService.Append(Meshes);
            bool worldfall = Compatibility.Worldfall.WorldfallVisualBridge.IsInstalled;
            SampleArtifacts(worldfall);
            if (worldfall) NativeProjectileFlights.AppendSpatial(Meshes);
        }
        nativeBatch.Clear();
        foreach (SpatialVisualMeshInstance instance in Meshes)
            if (drawNative && instance.DrawNative && IsNativeVisible(instance)) nativeBatch.Add(instance);
        if (nativeBatch.Count > 0 && view == null) view = SpatialNativeMeshView.Create(root);
        if (view != null) view.DrawBatch(nativeBatch);
    }

    /// <summary>单帧真实武器与纸符不会被当作整张法术动画处理。</summary>
    private void SampleSpriteBodies()
    {
        spriteBodies.ForEachEntity((ref Position position, ref Rotation rotation, ref AnimData animation, ref Scale scale, Entity entity) =>
        {
            if (animation.CurrentFrame == null || entity.Tags.Has<TagRecycle>()) return;
            Color tint = entity.TryGetComponent(out AnimTint color) ? color.Value : Color.white;
            Meshes.Add(new SpatialVisualMeshInstance(SpatialSpriteMesh.Get(animation.CurrentFrame), position.value,
                Quaternion.Euler(0f, 0f, AnimVisualUtils.GetVisualAngle(entity, ref rotation)), scale.value, tint, true));
        });
    }

    private void SampleProjections()
    {
        projections.ForEachEntity((ref ArtifactSpatialProjection projection, Entity entity) =>
        {
            if (projection.Pose.Model != null && projection.Pose.Tint.a > 0f &&
                ArtifactSwordArrayExecution.IsActiveBlade(entity))
                Meshes.Add(projection.Pose);
        });
    }

    /// <summary>复用组合模型和材质；原有显化系统仍负责位置、大小与控制状态。</summary>
    private void SampleArtifacts(bool worldfall)
    {
        artifacts.ForEachEntity((ref Position position, ref Rotation rotation, ref ArtifactAppearance _,
            ref ArtifactManifestation manifestation, Entity entity) =>
        {
            if ((!worldfall && !ArtifactAppearanceRenderer.UsesSpatialPresentation(entity)) ||
                (!manifestation.visible && !HasSpatialController(entity)) || entity.Tags.Has<TagRecycle>() ||
                !ArtifactAppearanceRenderer.TryGetSpatialPose(entity, out SpatialVisualMeshInstance pose)) return;
            Meshes.Add(pose);
        });
    }

    /// <summary>原版的可见标记包含镜头裁剪；三维仍需显示实际装备或独立运动的法宝。</summary>
    /// <param name="artifact">当前显化的法宝。</param>
    private static bool HasSpatialController(Entity artifact)
    {
        if (artifact.HasComponent<ArtifactIndependentMotion>() || artifact.HasComponent<SkillExecutionBodyLease>()) return true;
        foreach (Entity owner in artifact.GetIncomingLinks<EquippedArtifactRelation>().Entities)
        {
            Actor actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor != null && actor.isAlive()) return true;
        }
        foreach (Entity inventory in artifact.GetIncomingLinks<ArtifactSectInstallationRelation>().Entities)
            if (inventory.TryGetComponent(out SectInventoryBinder binder) && binder.Sect != null &&
                !binder.Sect.isRekt() && ArtifactSectService.ResolveInstallationBuilding(binder.Sect) != null) return true;
        return false;
    }

    /// <summary>原版按完整投影边界裁剪，避免大型主体的中心出镜后提前消失。</summary>
    /// <param name="instance">包含几何、姿态和大小的主体快照。</param>
    private static bool IsNativeVisible(SpatialVisualMeshInstance instance)
    {
        if (MapBox.isRenderMiniMap()) return false;
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        Bounds bounds = instance.Model.Bounds;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = instance.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
            Vector3 viewport = World.world.camera.WorldToViewportPoint(new Vector3(point.x, point.y + point.z));
            min = Vector2.Min(min, viewport);
            max = Vector2.Max(max, viewport);
        }
        return max.x >= 0f && min.x <= 1f && max.y >= 0f && min.y <= 1f;
    }

    /// <summary>退出世界时清掉快照和绘制引用，不触碰玩法实体。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        Meshes.Clear();
        SpatialSpriteMesh.Clear();
        SpatialCombatFeedback.Clear();
        SkillParticleEmitter.ClearWorldState();
        SpatialVisualEffects.ClearWorldState();
        GoldenBellSpatialVisualCue.ClearWorldState();
        ArtifactSpatialVisualRuntime.ClearWorldState();
        TalismanVfxManager.ClearWorldState();
        CombatVisualAudio.ClearWorldState();
        Compatibility.Worldfall.WorldfallVisualBridge.ClearWorldState();
        if (view != null) Object.Destroy(view.gameObject);
        view = null;
        nativeBatch.Clear();
    }
}
