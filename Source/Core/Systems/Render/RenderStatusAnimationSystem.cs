using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

/// <summary>
/// 让 StatusEffectAsset 持有的 SkillEntityAnimation 跟随状态持有者播放，
/// 并在状态建立和移除时处理出现、运行及消散阶段。
/// </summary>
public sealed class RenderStatusAnimationSystem :
    QuerySystem<StatusComponent, StatusAnimationState>,
    IWorldStateClearable
{
    private readonly List<DesiredVisual> desired = new();
    private readonly Dictionary<VisualKey, RuntimeVisual> visuals = new();
    private readonly HashSet<VisualKey> seen = new();
    private readonly List<VisualKey> stale = new();

    /// <summary>忽略预制体、失活和待回收状态。</summary>
    public RenderStatusAnimationSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    void IWorldStateClearable.ClearWorldState()
    {
        Clear();
    }

    /// <summary>缓冲查询结果后再创建或回收动画实体，避免在 ECS 查询中发生结构变更。</summary>
    protected override void OnUpdate()
    {
        if (!MapBox.isRenderGameplay())
        {
            Clear();
            return;
        }

        desired.Clear();
        Query.ForEachEntity((ref StatusComponent status, ref StatusAnimationState state, Entity statusEntity) =>
        {
            StatusAnimationSettings settings = status.Type.AnimationSettings;
            if (settings.Animation == null) return;
            foreach (Entity ownerEntity in statusEntity.GetIncomingLinks<StatusRelation>().Entities)
            {
                if (!ownerEntity.HasComponent<ActorBinder>()) continue;
                Actor actor = ownerEntity.GetComponent<ActorBinder>().Actor;
                if (actor == null || actor.isRekt() || !actor.is_visible) continue;
                desired.Add(new DesiredVisual(
                    new VisualKey(statusEntity.Id, actor.data.id),
                    actor,
                    settings,
                    state.ScaleMultiplier > 0f ? state.ScaleMultiplier : 1f));
            }
        });

        seen.Clear();
        for (var i = 0; i < desired.Count; i++)
        {
            DesiredVisual item = desired[i];
            seen.Add(item.Key);
            Ensure(item);
        }

        stale.Clear();
        foreach (VisualKey key in visuals.Keys)
        {
            if (!seen.Contains(key)) stale.Add(key);
        }
        for (var i = 0; i < stale.Count; i++) End(stale[i]);
    }

    /// <summary>创建新状态的出现阶段，或刷新现有运行阶段的位置。</summary>
    private void Ensure(DesiredVisual desiredVisual)
    {
        Vector3 position = SkillVisualCoordinates.FromActor(desiredVisual.Actor);
        if (!visuals.TryGetValue(desiredVisual.Key, out RuntimeVisual runtime))
        {
            runtime = new RuntimeVisual
            {
                Actor = desiredVisual.Actor,
                Settings = desiredVisual.Settings,
                ScaleMultiplier = desiredVisual.ScaleMultiplier,
                LastPosition = position,
            };
            SkillEntityAnimation animation = desiredVisual.Settings.Animation;
            if (animation.HasAppearance)
            {
                runtime.Appearance = SpawnClip(
                    animation.Appearance,
                    animation.Scale,
                    desiredVisual.Settings,
                    position,
                    false,
                    desiredVisual.ScaleMultiplier);
                runtime.RuntimeStartsAt = Time.time + ClipDuration(
                    animation.Appearance,
                    desiredVisual.Settings.FrameInterval);
            }
            else
            {
                runtime.RuntimeStartsAt = Time.time;
            }
            visuals.Add(desiredVisual.Key, runtime);
        }

        runtime.Actor = desiredVisual.Actor;
        runtime.ScaleMultiplier = desiredVisual.ScaleMultiplier;
        runtime.LastPosition = position;
        if (!IsAlive(runtime.Runtime) && Time.time >= runtime.RuntimeStartsAt)
        {
            SkillEntityAnimation animation = runtime.Settings.Animation;
            runtime.Runtime = SpawnClip(
                animation.Runtime,
                animation.Scale,
                runtime.Settings,
                position,
                true,
                runtime.ScaleMultiplier);
        }
        KeepAt(runtime.Appearance, position, runtime.Settings.Animation.Scale * runtime.ScaleMultiplier);
        KeepAt(runtime.Runtime, position, runtime.Settings.Animation.Scale * runtime.ScaleMultiplier);
    }

    /// <summary>结束状态运行阶段，并按需播放消散片段。</summary>
    private void End(VisualKey key)
    {
        if (!visuals.TryGetValue(key, out RuntimeVisual runtime)) return;
        Recycle(runtime.Appearance);
        Recycle(runtime.Runtime);
        SkillEntityAnimation animation = runtime.Settings.Animation;
        if (animation.HasDissipation)
        {
            SpawnClip(
                animation.Dissipation,
                animation.Scale,
                runtime.Settings,
                runtime.LastPosition,
                false,
                runtime.ScaleMultiplier);
        }
        visuals.Remove(key);
    }

    /// <summary>使用标准 Skill 原始动画实体播放一个状态动画片段。</summary>
    private static Entity SpawnClip(
        SkillEntityAnimationClip clip,
        float scale,
        StatusAnimationSettings settings,
        Vector3 position,
        bool runtime,
        float scaleMultiplier)
    {
        float frameInterval = clip.Settings.ResolveFrameInterval(settings.FrameInterval);
        bool loop = runtime && clip.Settings.ResolveLoop(true);
        float lifeTime = loop ? 2f : ClipDuration(clip, settings.FrameInterval);
        return ModClass.I.SkillV3.SpawnAnim(
            clip.Frames,
            position,
            Vector3.right,
            scale * Mathf.Max(0.01f, scaleMultiplier),
            null,
            frameInterval,
            loop,
            Mathf.Max(0.05f, lifeTime),
            settings.FixedUpright ? VisualRotation.FixedUpright() : VisualRotation.FollowRotation());
    }

    /// <summary>计算非循环片段完整播放一次的持续时间。</summary>
    private static float ClipDuration(SkillEntityAnimationClip clip, float baseFrameInterval)
    {
        return Mathf.Max(0.05f,
            clip.Frames.Length * clip.Settings.ResolveFrameInterval(baseFrameInterval));
    }

    /// <summary>让循环动画持续跟随持有者，并延长原始动画实体寿命。</summary>
    private static void KeepAt(Entity entity, Vector3 position, float scale)
    {
        if (!IsAlive(entity)) return;
        entity.GetComponent<Position>().value = position;
        entity.GetComponent<Scale>().value = Vector3.one * scale;
        if (entity.GetComponent<AnimController>().meta.loop)
        {
            entity.GetComponent<AliveTimer>().value = 0f;
            entity.GetComponent<AliveTimeLimit>().value = 2f;
        }
    }

    /// <summary>请求回收一个仍存活的动画实体。</summary>
    private static void Recycle(Entity entity)
    {
        if (IsAlive(entity)) ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
    }

    /// <summary>判断动画实体仍可安全访问。</summary>
    private static bool IsAlive(Entity entity)
    {
        return !entity.IsNull && !entity.Tags.Has<TagRecycle>();
    }

    /// <summary>在离开游戏渲染态时清理全部状态动画。</summary>
    private void Clear()
    {
        foreach (RuntimeVisual runtime in visuals.Values)
        {
            Recycle(runtime.Appearance);
            Recycle(runtime.Runtime);
        }
        visuals.Clear();
        desired.Clear();
        seen.Clear();
        stale.Clear();
    }

    /// <summary>由状态实体和持有者共同确定的唯一表现键。</summary>
    private readonly struct VisualKey : System.IEquatable<VisualKey>
    {
        private readonly int statusId;
        private readonly long actorId;

        public VisualKey(int statusId, long actorId)
        {
            this.statusId = statusId;
            this.actorId = actorId;
        }

        public bool Equals(VisualKey other)
        {
            return statusId == other.statusId && actorId == other.actorId;
        }

        public override bool Equals(object obj)
        {
            return obj is VisualKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return statusId * 397 ^ actorId.GetHashCode();
            }
        }
    }

    /// <summary>从查询阶段传递到结构变更阶段的不可变表现需求。</summary>
    private readonly struct DesiredVisual
    {
        public readonly VisualKey Key;
        public readonly Actor Actor;
        public readonly StatusAnimationSettings Settings;
        public readonly float ScaleMultiplier;

        public DesiredVisual(
            VisualKey key,
            Actor actor,
            StatusAnimationSettings settings,
            float scaleMultiplier)
        {
            Key = key;
            Actor = actor;
            Settings = settings;
            ScaleMultiplier = scaleMultiplier;
        }
    }

    /// <summary>一个状态动画生命周期的运行时实体引用。</summary>
    private sealed class RuntimeVisual
    {
        public Actor Actor;
        public StatusAnimationSettings Settings;
        public float ScaleMultiplier;
        public Entity Appearance;
        public Entity Runtime;
        public Vector3 LastPosition;
        public float RuntimeStartsAt;
    }
}
