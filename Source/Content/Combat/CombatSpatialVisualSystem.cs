using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Visuals;
using Cultiway.Content.Utils;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Visuals;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>读取地刺、真实武器与组合法宝的现有状态，提供两个视角共用的空间快照。</summary>
internal sealed class CombatSpatialVisualSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>地刺装饰碎屑的同屏上限；主体与玩法结算不受此限制。</summary>
    private const int MaxGroundShards = 64;
    /// <summary>当前显示帧的模型快照；没有自己的玩法时钟。</summary>
    internal static readonly List<SpatialVisualMeshInstance> Meshes = new();
    /// <summary>当前世界的实际近战执行体。</summary>
    private readonly ArchetypeQuery<Position, Rotation, EquippedWeaponMotionState, AnimData, Scale> weapons;
    /// <summary>当前世界带动画阶段的技能。</summary>
    private readonly ArchetypeQuery<Position, SkillEntity, AnimData> skills;
    /// <summary>实际魔法护持身体。</summary>
    private readonly ArchetypeQuery<ActorBinder, Magic> mages;
    /// <summary>实际单帧物品的空间显示。</summary>
    private readonly ArchetypeQuery<Position, Rotation, AnimData, Scale> spriteBodies;
    /// <summary>已经显化的组合外观法宝。</summary>
    private readonly ArchetypeQuery<Position, Rotation, ArtifactAppearance, ArtifactManifestation> artifacts;
    /// <summary>原版几何视图的世界根节点。</summary>
    private readonly Transform root;
    /// <summary>复用到历史最大可见数量的二维视图。</summary>
    private SpatialNativeMeshView view;
    /// <summary>本帧经过原版镜头裁剪的同材质批次。</summary>
    private readonly List<SpatialVisualMeshInstance> nativeBatch = new();
    /// <summary>地刺主体，所有实例共享几何。</summary>
    private readonly SpatialVisualMesh spike = SpatialMaterialGeometry.Rocks[0];
    /// <summary>接触后的少量碎屑模型。</summary>
    private readonly SpatialVisualMesh shard = SpatialMaterialGeometry.Rocks[2];

    /// <summary>只登记读取查询，不增加施法、支付与命中入口。</summary>
    /// <param name="world">现有世界实体库。</param>
    internal CombatSpatialVisualSystem(EntityStore world)
    {
        QueryFilter filter = new();
        filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        weapons = world.Query<Position, Rotation, EquippedWeaponMotionState, AnimData, Scale>(filter);
        skills = world.Query<Position, SkillEntity, AnimData>(filter);
        artifacts = world.Query<Position, Rotation, ArtifactAppearance, ArtifactManifestation>(filter);
        mages = world.Query<ActorBinder, Magic>(filter);
        QueryFilter spriteFilter = new();
        spriteFilter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        spriteFilter.AllTags(Tags.Get<TagSpatialSpriteBody>());
        spriteBodies = world.Query<Position, Rotation, AnimData, Scale>(spriteFilter);
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
        bool drawNative = MapBox.isRenderGameplay();
        if (drawNative || Compatibility.Worldfall.WorldfallVisualBridge.IsInstalled)
        {
            int detailBudget = 384;
            BattleSpellVisualRuntime.Append(Meshes, ref detailBudget);
            SampleGroundSpikes(ref detailBudget);
            GoldenBellSpatialVisualCue.AppendMeshes(Meshes, World.world.getCurWorldTime(), ref detailBudget);
            ArtifactSpatialVisualRuntime.Append(Meshes, ref detailBudget);
            MagicShieldVfx.Append(mages, Meshes);
            TalismanVfxManager.Append(Meshes);
            Meshes.AddRange(Core.Systems.Render.RenderStatusAnimationSystem.Meshes);
            SampleSpriteBodies();
            SpatialVisualEffects.Append(Meshes, ref detailBudget);
            if (Compatibility.Worldfall.WorldfallVisualBridge.IsInstalled)
            {
                SampleWeapons();
                SampleArtifacts();
                WeaponControl.WeaponControlRuntimeService.AppendSpatialProjectiles(Meshes);
            }
        }
        nativeBatch.Clear();
        foreach (SpatialVisualMeshInstance instance in Meshes)
            if (drawNative && instance.DrawNative && IsNativeVisible(instance)) nativeBatch.Add(instance);
        if (nativeBatch.Count > 0 && view == null) view = SpatialNativeMeshView.Create(root);
        if (view != null) view.DrawBatch(nativeBatch);
    }

    /// <summary>使用真实动画阶段决定生长与收尾；半径取实际作用范围。</summary>
    /// <param name="detailBudget">本帧剩余装饰数量。</param>
    private void SampleGroundSpikes(ref int detailBudget)
    {
        int remainingShards = Mathf.Min(MaxGroundShards, detailBudget);
        int remainingDetails = detailBudget;
        skills.ForEachEntity((ref Position position, ref SkillEntity skill, ref AnimData animation, Entity entity) =>
        {
            SkillAnimationLifecycleState lifecycle = entity.TryGetComponent(out SkillAnimationLifecycleState state)
                ? state : new SkillAnimationLifecycleState { Phase = SkillAnimationPhase.Runtime };
            SkillSpatialVisual.Append(entity, skill, position.value, lifecycle, animation, Meshes, ref remainingDetails);
            if (skill.Asset == SkillEntities.LightningStrike && entity.Tags.Has<TagSpatialVisualReplacement>())
                LightningStrikeSpatialVisual.Append(entity, position.value, lifecycle, animation, Meshes);
            if (skill.Asset != SkillEntities.GroundSpike || !entity.Tags.Has<TagSpatialVisualReplacement>() ||
                lifecycle.Phase == SkillAnimationPhase.RecycleReady) return;
            SkillPositionImpactState impact = entity.GetComponent<SkillPositionImpactState>();
            if (!impact.Requested && !impact.Resolved) return;
            float progress = Mathf.Clamp01((animation.frame_idx + animation.frame_timer /
                SkillAnimationLifecycle.ResolveCurrentFrameInterval(entity)) / animation.frames.Length);
            float radius = SkillEffectRadius.Resolve(entity, entity.GetComponent<ColliderSphere>().Radius);
            float height = lifecycle.Phase switch
            {
                SkillAnimationPhase.Appearance => Mathf.Lerp(0.08f, 1f, 1f - Mathf.Pow(1f - progress, 3f)),
                SkillAnimationPhase.Dissipation => 1f - progress * progress,
                _ => 1f,
            };
            float alpha = lifecycle.Phase == SkillAnimationPhase.Dissipation ? 1f - progress : 1f;
            if(lifecycle.Phase==SkillAnimationPhase.Dissipation)
                MaterialSpellVisuals.DrawRock(Meshes,0,position.value,Quaternion.identity,Vector3.one*radius,.2f+progress*1.3f);
            else Meshes.Add(new SpatialVisualMeshInstance(spike, position.value, Quaternion.identity,
                new Vector3(radius, radius, radius * height), Color.white, true,false));
            if (lifecycle.Phase != SkillAnimationPhase.Dissipation) return;
            // 碎屑由当前阶段和固定序号计算，切换视角时不会重发或使用战斗随机数。
            float t = progress * 0.45f;
            for (int i = 0; i < 5 && remainingShards > 0 && remainingDetails > 0; i++, remainingShards--, remainingDetails--)
            {
                float angle = (i * 1.256637f + entity.Id * 0.37f);
                Vector3 offset = new(Mathf.Cos(angle) * (0.35f + t * 1.5f),
                    Mathf.Sin(angle) * (0.35f + t * 1.5f), Mathf.Max(0.02f, 0.3f + t * 2f - t * t * 7f));
                Meshes.Add(new SpatialVisualMeshInstance(shard, position.value + offset * radius,
                    Quaternion.Euler(progress * 160f, i * 43f, i * 72f), Vector3.one * radius * 0.12f,
                    new Color(1f, 1f, 1f, alpha), true));
            }
        });
        detailBudget = remainingDetails;
    }

    /// <summary>单帧真实武器与纸符不会被当作整张法术动画处理。</summary>
    private void SampleSpriteBodies()
    {
        spriteBodies.ForEachEntity((ref Position position, ref Rotation rotation, ref AnimData animation, ref Scale scale, Entity entity) =>
        {
            if (animation.CurrentFrame == null || entity.Tags.Has<TagRecycle>()) return;
            Color tint = entity.TryGetComponent(out AnimTint color) ? color.Value : Color.white;
            if (entity.TryGetComponent(out ArtifactSpatialProjection projection) &&
                ArtifactAppearanceRenderer.TryGetSpatialPose(projection.Artifact, out SpatialVisualMeshInstance body))
            {
                Quaternion facing = Quaternion.Euler(0f, 0f, rotation.z - projection.Artifact.GetComponent<Rotation>().z) * body.Rotation;
                // 原版剑影走已有器身精灵批次；三维视角保留组合模型，避免原版重复投影 96 份精细器身。
                Meshes.Add(new SpatialVisualMeshInstance(body.Model, position.value, facing, body.Scale * projection.SizeRatio, tint));
                return;
            }
            Meshes.Add(new SpatialVisualMeshInstance(SpatialSpriteMesh.Get(animation.CurrentFrame), position.value,
                Quaternion.Euler(0f, 0f, AnimVisualUtils.GetVisualAngle(entity, ref rotation)), scale.value, tint, true));
        });
    }

    /// <summary>直接读取当前真实武器精灵和运动姿态，三维显示使用像素轮廓挤出的薄实体。</summary>
    private void SampleWeapons()
    {
        weapons.ForEachEntity((ref Position position, ref Rotation rotation, ref EquippedWeaponMotionState _,
            ref AnimData animation, ref Scale scale, Entity entity) =>
        {
            if (entity.Tags.Has<TagRecycle>() || animation.CurrentFrame == null) return;
            float angle = AnimVisualUtils.GetVisualAngle(entity, ref rotation);
            Meshes.Add(new SpatialVisualMeshInstance(SpatialSpriteMesh.Get(animation.CurrentFrame), position.value,
                Quaternion.Euler(0f, 0f, angle), scale.value, Color.white));
        });
    }

    /// <summary>复用组合模型和材质；原有显化系统仍负责位置、大小与控制状态。</summary>
    private void SampleArtifacts()
    {
        artifacts.ForEachEntity((ref Position position, ref Rotation rotation, ref ArtifactAppearance _,
            ref ArtifactManifestation manifestation, Entity entity) =>
        {
            if ((!manifestation.visible && !HasSpatialController(entity)) || entity.Tags.Has<TagRecycle>() ||
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
