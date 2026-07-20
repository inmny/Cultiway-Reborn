using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>消费核心形成表现信号，并维护来自权威运行时状态的唯一循环实例。</summary>
public sealed class CoreFormationEffectVisualSystem : QuerySystem<ActorBinder, CoreFormationEffectRuntime>
{
    private readonly List<CoreFormationEffectVisualSignal> signals = new();
    private readonly List<TransientVisual> transients = new();
    private readonly List<CoreFormationResolvedEffect> effects =
        new(CoreFormationEffectRuntime.MaxEntries);
    private readonly List<DesiredLoop> desiredLoops = new();
    private readonly Dictionary<LoopKey, LoopVisual> loops = new();
    private readonly HashSet<LoopKey> seenLoops = new();
    private readonly List<LoopKey> staleLoops = new();
    private readonly Dictionary<string, Sprite[]> frameCache = new(StringComparer.Ordinal);

    /// <summary>过滤预制体、失活与待回收角色实体。</summary>
    public CoreFormationEffectVisualSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    /// <summary>消费一次性信号、推进运动，并同步所有可见角色的循环表现。</summary>
    protected override void OnUpdate()
    {
        CoreFormationEffectVisualSignals.Drain(signals);
        if (!MapBox.isRenderGameplay())
        {
            ClearAll();
            return;
        }

        for (var i = 0; i < signals.Count; i++) SpawnSignal(signals[i]);
        UpdateTransients();
        SynchronizeLoops();
    }

    /// <summary>为一条表现信号创建原始帧动画和共享粒子。</summary>
    private void SpawnSignal(CoreFormationEffectVisualSignal signal)
    {
        if (signal.Owner == null || signal.Owner.isRekt() || !signal.Owner.is_visible || signal.Cue == null) return;
        Vector3 start = signal.Owner.cur_transform_position;
        Vector3 end = signal.Target != null && !signal.Target.isRekt()
            ? signal.Target.cur_transform_position
            : signal.Position;
        Vector3 position = ResolvePosition(signal.Cue.motion, start, end);
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;
        Entity entity = Spawn(signal.Cue, position, direction, signal.Potency, false);
        if (!IsAlive(entity)) return;

        float lifeTime = Mathf.Max(0.05f, signal.Cue.life_time);
        transients.Add(new TransientVisual
        {
            Entity = entity,
            Owner = signal.Owner,
            Target = signal.Target,
            Cue = signal.Cue,
            Start = start,
            End = end,
            StartedAt = Time.time,
            LifeTime = lifeTime,
            BaseScale = signal.Cue.scale * Mathf.Lerp(0.9f, 1.2f, Mathf.InverseLerp(0.75f, 2.5f,
                signal.Potency)),
        });
        EmitParticles(signal, position, direction);
    }

    /// <summary>推进跟随、直线和缩放运动，并回收已经结束的临时记录。</summary>
    private void UpdateTransients()
    {
        for (var i = transients.Count - 1; i >= 0; i--)
        {
            TransientVisual visual = transients[i];
            if (!IsAlive(visual.Entity) || Time.time - visual.StartedAt >= visual.LifeTime)
            {
                transients.RemoveAt(i);
                continue;
            }

            float progress = Mathf.Clamp01((Time.time - visual.StartedAt) / visual.LifeTime);
            Vector3 ownerPosition = visual.Owner != null && !visual.Owner.isRekt()
                ? visual.Owner.cur_transform_position
                : visual.Start;
            Vector3 targetPosition = visual.Target != null && !visual.Target.isRekt()
                ? visual.Target.cur_transform_position
                : visual.End;
            ref Position position = ref visual.Entity.GetComponent<Position>();
            position.value = visual.Cue.motion switch
            {
                CoreFormationVisualMotion.FollowOwner => ownerPosition,
                CoreFormationVisualMotion.FollowTarget => targetPosition,
                CoreFormationVisualMotion.Linear => Vector3.Lerp(ownerPosition, targetPosition, progress),
                _ => position.value,
            };
            float scaleFactor = visual.Cue.motion switch
            {
                CoreFormationVisualMotion.Expand => Mathf.Lerp(0.55f, 1.35f, progress),
                CoreFormationVisualMotion.Contract => Mathf.Lerp(1.35f, 0.55f, progress),
                _ => 1f,
            };
            visual.Entity.GetComponent<Scale>().value = Vector3.one * visual.BaseScale * scaleFactor;
            if (!visual.Cue.fixed_upright)
            {
                Vector3 direction = targetPosition - ownerPosition;
                if (direction.sqrMagnitude > 0.0001f)
                    visual.Entity.GetComponent<Rotation>().value = direction.normalized;
            }
        }
    }

    /// <summary>从角色运行时重建护盾、蓄力、储备和主动形态的唯一循环表现。</summary>
    private void SynchronizeLoops()
    {
        desiredLoops.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref CoreFormationEffectRuntime runtime, Entity _) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt() || !actor.is_visible || runtime.entries == null) return;
            effects.Clear();
            CoreFormationEffectResolver.Resolve(actor.GetExtend(), effects);
            for (var i = 0; i < effects.Count; i++)
            {
                CoreFormationResolvedEffect effect = effects[i];
                CoreFormationEffectVisualCue cue = effect.Definition.visual?.loop;
                int runtimeIndex = runtime.FindIndex(effect.Definition.family_id);
                if (cue == null || runtimeIndex < 0 || !ShouldLoop(runtime.entries[runtimeIndex])) continue;
                var key = new LoopKey(actor.data.id, effect.Definition.family_id);
                desiredLoops.Add(new DesiredLoop(key, actor, cue, effect.Potency));
            }
        });

        seenLoops.Clear();
        for (var i = 0; i < desiredLoops.Count; i++)
        {
            DesiredLoop desired = desiredLoops[i];
            seenLoops.Add(desired.Key);
            EnsureLoop(desired.Key, desired.Actor, desired.Cue, desired.Potency);
        }

        staleLoops.Clear();
        foreach (LoopKey key in loops.Keys)
            if (!seenLoops.Contains(key)) staleLoops.Add(key);
        for (var i = 0; i < staleLoops.Count; i++) EndLoop(staleLoops[i]);
    }

    /// <summary>创建或刷新一个角色效果族的唯一循环帧动画。</summary>
    private void EnsureLoop(LoopKey key, Actor actor, CoreFormationEffectVisualCue cue, float potency)
    {
        if (!loops.TryGetValue(key, out LoopVisual loop) || !IsAlive(loop.Entity))
        {
            Entity entity = Spawn(cue, actor.cur_transform_position, Vector3.right, potency, true);
            loop = new LoopVisual { Entity = entity, Actor = actor, Cue = cue };
            loops[key] = loop;
        }
        if (!IsAlive(loop.Entity)) return;
        loop.Entity.GetComponent<Position>().value = actor.cur_transform_position;
        loop.Entity.GetComponent<AliveTimer>().value = 0f;
        loop.Entity.GetComponent<AliveTimeLimit>().value = Mathf.Max(0.5f, cue.life_time);
    }

    /// <summary>使用缓存帧序列创建一个原始动画实体。</summary>
    private Entity Spawn(
        CoreFormationEffectVisualCue cue,
        Vector3 position,
        Vector3 direction,
        float potency,
        bool forceLoop)
    {
        if (!frameCache.TryGetValue(cue.path, out Sprite[] frames))
        {
            frames = SkillEntityAsset.LoadOrderedFrames(cue.path);
            frameCache[cue.path] = frames;
        }
        if (frames.Length == 0) return default;
        float scale = cue.scale * Mathf.Lerp(0.9f, 1.2f,
            Mathf.InverseLerp(0.75f, 2.5f, potency));
        Color? tint = cue.use_tint ? cue.tint : null;
        VisualRotation rotation = cue.fixed_upright
            ? VisualRotation.FixedUpright()
            : VisualRotation.FollowRotation();
        return ModClass.I.SkillV3.SpawnAnim(
            frames,
            position,
            direction.normalized,
            scale,
            tint,
            cue.frame_interval,
            forceLoop || cue.loop,
            Mathf.Max(0.05f, cue.life_time),
            rotation);
    }

    /// <summary>按效果族颜色为一次成功触发发射少量共享粒子。</summary>
    private static void EmitParticles(
        CoreFormationEffectVisualSignal signal,
        Vector3 position,
        Vector3 direction)
    {
        SkillFlyOverParticleStyle style = SkillFlyOverParticleStyle.Default;
        style.ParticlesPerEmission = Mathf.Clamp(Mathf.RoundToInt(3f * signal.Potency), 3, 8);
        style.MinSize *= 0.8f;
        style.MaxSize *= 1.1f;
        SkillFlyOverParticleEmitter.EmitBurst(
            position,
            ResolveColor(signal.FamilyId),
            style,
            0.24f,
            direction,
            signal.Cue.fixed_upright ? 0f : 0.8f,
            style.ParticlesPerEmission);
    }

    /// <summary>返回效果族用于共享粒子的代表色。</summary>
    private static Color ResolveColor(string familyId)
    {
        return familyId switch
        {
            CoreFormationEffectFamilies.Iron or CoreFormationEffectFamilies.Sword =>
                new Color(1f, 0.82f, 0.28f),
            CoreFormationEffectFamilies.Wood or CoreFormationEffectFamilies.Vital =>
                new Color(0.35f, 0.9f, 0.42f),
            CoreFormationEffectFamilies.Water => new Color(0.3f, 0.7f, 1f),
            CoreFormationEffectFamilies.Fire or CoreFormationEffectFamilies.Yang =>
                new Color(1f, 0.35f, 0.12f),
            CoreFormationEffectFamilies.Earth or CoreFormationEffectFamilies.Body =>
                new Color(0.82f, 0.62f, 0.25f),
            CoreFormationEffectFamilies.Yin or CoreFormationEffectFamilies.Illusion =>
                new Color(0.5f, 0.3f, 0.82f),
            CoreFormationEffectFamilies.Dragon => new Color(0.3f, 0.92f, 0.7f),
            _ => new Color(0.85f, 0.85f, 0.92f),
        };
    }

    /// <summary>判断某个运行时状态是否需要保持循环表现。</summary>
    private static bool ShouldLoop(CoreFormationEffectRuntimeEntry runtime)
    {
        return runtime.active_remaining > 0f || runtime.value > 0.001f || runtime.charges > 0;
    }

    /// <summary>根据运动类型选择动画初始位置。</summary>
    private static Vector3 ResolvePosition(CoreFormationVisualMotion motion, Vector3 start, Vector3 end)
    {
        return motion switch
        {
            CoreFormationVisualMotion.FollowOwner or CoreFormationVisualMotion.Linear => start,
            CoreFormationVisualMotion.FollowTarget => end,
            _ => end,
        };
    }

    /// <summary>判断原始动画实体是否仍可更新。</summary>
    private static bool IsAlive(Entity entity)
    {
        return !entity.IsNull && entity.HasComponent<Position>() && !entity.Tags.Has<TagRecycle>();
    }

    /// <summary>结束并移除一个不再由权威状态支持的循环实例。</summary>
    private void EndLoop(LoopKey key)
    {
        if (!loops.TryGetValue(key, out LoopVisual loop)) return;
        if (IsAlive(loop.Entity)) ModClass.I.CommandBuffer.AddTag<TagRecycle>(loop.Entity.Id);
        loops.Remove(key);
    }

    /// <summary>离开游戏渲染模式时回收全部形成动画记录。</summary>
    private void ClearAll()
    {
        for (var i = 0; i < transients.Count; i++)
            if (IsAlive(transients[i].Entity)) ModClass.I.CommandBuffer.AddTag<TagRecycle>(transients[i].Entity.Id);
        transients.Clear();
        staleLoops.Clear();
        staleLoops.AddRange(loops.Keys);
        for (var i = 0; i < staleLoops.Count; i++) EndLoop(staleLoops[i]);
    }

    /// <summary>一次性帧动画的运动记录。</summary>
    private struct TransientVisual
    {
        /// <summary>原始动画实体。</summary>
        public Entity Entity;

        /// <summary>效果持有者。</summary>
        public Actor Owner;

        /// <summary>可选受影响目标。</summary>
        public Actor Target;

        /// <summary>播放与运动配置。</summary>
        public CoreFormationEffectVisualCue Cue;

        /// <summary>出生时的持有者位置。</summary>
        public Vector3 Start;

        /// <summary>出生时的目标或落点位置。</summary>
        public Vector3 End;

        /// <summary>Unity 表现时间的开始时刻。</summary>
        public float StartedAt;

        /// <summary>动画寿命。</summary>
        public float LifeTime;

        /// <summary>已包含效果倍率的基础缩放。</summary>
        public float BaseScale;
    }

    /// <summary>一个权威循环实例。</summary>
    private struct LoopVisual
    {
        /// <summary>原始动画实体。</summary>
        public Entity Entity;

        /// <summary>循环跟随角色。</summary>
        public Actor Actor;

        /// <summary>循环播放配置。</summary>
        public CoreFormationEffectVisualCue Cue;
    }

    /// <summary>查询阶段收集、离开查询后才创建或刷新的循环表现请求。</summary>
    private readonly struct DesiredLoop
    {
        /// <summary>角色和效果族组成的唯一循环键。</summary>
        public readonly LoopKey Key;

        /// <summary>循环表现跟随的角色。</summary>
        public readonly Actor Actor;

        /// <summary>循环帧动画配置。</summary>
        public readonly CoreFormationEffectVisualCue Cue;

        /// <summary>决定循环表现缩放的效果倍率。</summary>
        public readonly float Potency;

        /// <summary>创建一条延迟到查询结束后处理的循环表现请求。</summary>
        public DesiredLoop(LoopKey key, Actor actor, CoreFormationEffectVisualCue cue, float potency)
        {
            Key = key;
            Actor = actor;
            Cue = cue;
            Potency = potency;
        }
    }

    /// <summary>按角色和效果族唯一标识一个循环实例。</summary>
    private readonly struct LoopKey : IEquatable<LoopKey>
    {
        private readonly long actorId;
        private readonly string familyId;

        /// <summary>创建循环实例键。</summary>
        public LoopKey(long actorId, string familyId)
        {
            this.actorId = actorId;
            this.familyId = familyId;
        }

        /// <summary>判断角色和效果族是否同时相同。</summary>
        public bool Equals(LoopKey other)
        {
            return actorId == other.actorId && string.Equals(familyId, other.familyId, StringComparison.Ordinal);
        }

        /// <summary>判断对象是否为相同循环实例键。</summary>
        public override bool Equals(object obj)
        {
            return obj is LoopKey other && Equals(other);
        }

        /// <summary>生成角色和效果族组合哈希。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return actorId.GetHashCode() * 397 ^ (familyId?.GetHashCode() ?? 0);
            }
        }
    }
}
