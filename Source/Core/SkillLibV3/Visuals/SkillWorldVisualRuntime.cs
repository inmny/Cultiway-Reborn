using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 消费结构化结算事件并维护池化法阵、圆弧、扫掠、逐帧动画和精灵碎屑。
/// 该运行时不创建 ECS 粒子，也不为单个碎屑创建 ParticleSystem。
/// </summary>
internal static class SkillWorldVisualRuntime
{
    private const int MaxFields = 48;
    private const int MaxArcs = 128;
    private const int MaxSweeps = 64;
    private const int MaxParticles = 512;
    private const int MaxFlipbooks = 96;
    private const int MaxDelayedEvents = 512;
    private const string PrimitiveRoot = "cultiway/effect/world_primitives";

    private static readonly Dictionary<string, Sprite> SpriteCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Sprite[]> FrameCache = new(StringComparer.Ordinal);
    private static readonly List<FieldInstance> Fields = new(MaxFields);
    private static readonly List<ArcInstance> Arcs = new(MaxArcs);
    private static readonly List<SweepInstance> Sweeps = new(MaxSweeps);
    private static readonly List<SpriteParticle> Particles = new(MaxParticles);
    private static readonly List<FlipbookInstance> Flipbooks = new(MaxFlipbooks);
    private static readonly List<DelayedEvent> DelayedEvents = new(MaxDelayedEvents);
    private static readonly Stack<WorldGlyphFieldView> FieldPool = new();
    private static readonly Stack<WorldArcRenderer> ArcPool = new();
    private static readonly Stack<WorldSweepRenderer> SweepPool = new();
    private static readonly Stack<WorldSpriteView> SpritePool = new();
    private static readonly Stack<SpriteParticle> ParticleStatePool = new();
    private static readonly Stack<FlipbookInstance> FlipbookStatePool = new();
    private static readonly WorldArcBand[] SharedBands = new WorldArcBand[3];
    private static Transform worldRoot;
    private static Transform visualRoot;

    /// <summary>由渲染系统每帧调用，按游戏时间更新全部世界视觉。</summary>
    public static void Update(float deltaTime)
    {
        EnsureWorld();
        if (visualRoot == null) return;
        float elapsed = Mathf.Max(0f, deltaTime);
        ConsumeEvents();
        UpdateDelayedEvents(elapsed);
        UpdateFields(elapsed);
        UpdateArcs(elapsed);
        UpdateSweeps(elapsed);
        UpdateParticles(elapsed);
        UpdateFlipbooks(elapsed);
    }

    /// <summary>清空旧世界全部活动视图和资源缓存。</summary>
    public static void ClearWorldState()
    {
        for (int i = Fields.Count - 1; i >= 0; i--) ReturnField(Fields[i]);
        for (int i = Arcs.Count - 1; i >= 0; i--) ReturnArc(Arcs[i]);
        for (int i = Sweeps.Count - 1; i >= 0; i--) ReturnSweep(Sweeps[i]);
        for (int i = Particles.Count - 1; i >= 0; i--) ReturnParticle(Particles[i]);
        for (int i = Flipbooks.Count - 1; i >= 0; i--) ReturnFlipbook(Flipbooks[i]);
        Fields.Clear();
        Arcs.Clear();
        Sweeps.Clear();
        Particles.Clear();
        Flipbooks.Clear();
        DelayedEvents.Clear();
        FieldPool.Clear();
        ArcPool.Clear();
        SweepPool.Clear();
        SpritePool.Clear();
        ParticleStatePool.Clear();
        FlipbookStatePool.Clear();
        SpriteCache.Clear();
        FrameCache.Clear();
        if (visualRoot != null) UnityEngine.Object.Destroy(visualRoot.gameObject);
        worldRoot = null;
        visualRoot = null;
    }

    /// <summary>解析一个单帧世界视觉资源，并在当前世界内缓存结果。</summary>
    public static Sprite ResolveSprite(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (SpriteCache.TryGetValue(path, out Sprite cached)) return cached;
        Sprite sprite = SpriteTextureLoader.getSprite(path);
        SpriteCache[path] = sprite;
        return sprite;
    }

    /// <summary>判断给定世界范围是否处于相机附近。</summary>
    public static bool IsVisible(Vector3 position, float radius = 0f)
    {
        if (!MapBox.isRenderGameplay() || World.world?.camera == null) return false;
        Vector3 viewport = World.world.camera.WorldToViewportPoint(position);
        float margin = Mathf.Clamp(radius * 0.025f, 0.05f, 0.35f);
        return viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    /// <summary>在世界对象变化后重建视觉根节点和全部池。</summary>
    private static void EnsureWorld()
    {
        Transform currentRoot = World.world?.transform;
        if (currentRoot == null || worldRoot == currentRoot) return;
        ClearWorldState();
        worldRoot = currentRoot;
        GameObject rootObject = new("Cultiway_SkillWorldVisuals");
        rootObject.transform.SetParent(worldRoot, false);
        visualRoot = rootObject.transform;
    }

    /// <summary>消费当前帧已经提交的全部视觉事件。</summary>
    private static void ConsumeEvents()
    {
        while (SkillWorldVisualService.TryDequeue(out SkillWorldVisualEvent visualEvent))
        {
            if (visualEvent.Delay > 0f)
            {
                if (DelayedEvents.Count < MaxDelayedEvents)
                    DelayedEvents.Add(new DelayedEvent(visualEvent));
                continue;
            }
            HandleEvent(in visualEvent);
        }
    }

    /// <summary>把单个视觉事件分发到对应的池化表现。</summary>
    private static void HandleEvent(in SkillWorldVisualEvent visualEvent)
    {
        switch (visualEvent.Kind)
        {
            case SkillWorldVisualEventKind.BeginField:
                BeginField(in visualEvent);
                break;
            case SkillWorldVisualEventKind.AreaResolved:
                PlayAreaImpact(in visualEvent);
                break;
            case SkillWorldVisualEventKind.EffectResolved:
                PlayLocalEffect(in visualEvent);
                break;
        }
    }

    /// <summary>创建或刷新由同一个技能运行时实体维持的法阵。</summary>
    private static void BeginField(in SkillWorldVisualEvent visualEvent)
    {
        for (int i = 0; i < Fields.Count; i++)
        {
            FieldInstance existing = Fields[i];
            if (existing.SkillEntityKey != visualEvent.SkillEntityKey) continue;
            existing.Center = visualEvent.Center;
            existing.Radius = visualEvent.Radius;
            existing.Duration = visualEvent.Duration;
            existing.Elapsed = 0f;
            existing.Detached = false;
            return;
        }
        if (Fields.Count >= MaxFields) ReturnAndRemoveField(0);
        WorldGlyphFieldView view = FieldPool.Count > 0
            ? FieldPool.Pop()
            : WorldGlyphFieldView.Create(visualRoot);
        view.Configure(visualEvent.Profile);
        Fields.Add(new FieldInstance
        {
            SkillEntityKey = visualEvent.SkillEntityKey,
            Profile = visualEvent.Profile,
            Center = visualEvent.Center,
            Radius = visualEvent.Radius,
            Duration = visualEvent.Duration,
            View = view
        });
    }

    /// <summary>播放一次整体范围反馈；净化波由两道固定线宽分段弧组成。</summary>
    private static void PlayAreaImpact(in SkillWorldVisualEvent visualEvent)
    {
        if (visualEvent.Profile.AreaImpact != SkillAreaImpactVisualKind.PurificationWave ||
            !IsVisible(visualEvent.Center, visualEvent.Radius)) return;
        SpawnArc(ArcAnimationKind.PurificationWave, visualEvent.Center, visualEvent.Radius,
            0f, 0.42f, visualEvent.Profile.GlowColor, 1f);
        SpawnArc(ArcAnimationKind.PurificationWave, visualEvent.Center, visualEvent.Radius,
            0.07f, 0.42f, visualEvent.Profile.PrimaryColor, 0.4f);
    }

    /// <summary>点亮所属法阵并播放由实际结果决定的对象或地块局部反馈。</summary>
    private static void PlayLocalEffect(in SkillWorldVisualEvent visualEvent)
    {
        for (int i = 0; i < Fields.Count; i++)
        {
            if (Fields[i].SkillEntityKey == visualEvent.SkillEntityKey)
                Fields[i].View.Pulse(visualEvent.Position);
        }
        if (!IsVisible(visualEvent.Position, 0.75f)) return;

        switch (visualEvent.Profile.LocalEffect)
        {
            case SkillLocalEffectVisualKind.Healing:
                SpawnHealing(visualEvent.Position, visualEvent.Profile, 3, true);
                break;
            case SkillLocalEffectVisualKind.Rejuvenation:
                SpawnHealing(visualEvent.Position, visualEvent.Profile, 2, false);
                break;
            case SkillLocalEffectVisualKind.Purification:
                SpawnPurification(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.BattleBlessing:
                SpawnBattleBlessing(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.GuardBlessing:
                SpawnGuardBlessing(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.HasteBlessing:
                SpawnHasteBlessing(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.RaiseTerrain:
                SpawnTerrain(visualEvent.Position, visualEvent.Profile, true);
                break;
            case SkillLocalEffectVisualKind.LowerTerrain:
                SpawnTerrain(visualEvent.Position, visualEvent.Profile, false);
                break;
            case SkillLocalEffectVisualKind.FillWater:
                SpawnFillWater(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.DrainWater:
                SpawnDrainWater(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.NatureGrowth:
                SpawnNatureGrowth(visualEvent.Position, visualEvent.Profile);
                break;
            case SkillLocalEffectVisualKind.CleanLand:
                SpawnCleanLand(in visualEvent);
                break;
            case SkillLocalEffectVisualKind.Fertilize:
                SpawnFertilize(visualEvent.Position, visualEvent.Profile);
                break;
        }
    }

    /// <summary>更新带传播延迟的目标反馈。</summary>
    private static void UpdateDelayedEvents(float deltaTime)
    {
        for (int i = DelayedEvents.Count - 1; i >= 0; i--)
        {
            DelayedEvent delayed = DelayedEvents[i];
            delayed.Remaining -= deltaTime;
            if (delayed.Remaining > 0f) continue;
            SkillWorldVisualEvent visualEvent = delayed.Event;
            DelayedEvents.RemoveAt(i);
            HandleEvent(in visualEvent);
        }
    }

    /// <summary>更新持续法阵并回收已经消散的实例。</summary>
    private static void UpdateFields(float deltaTime)
    {
        for (int i = Fields.Count - 1; i >= 0; i--)
        {
            FieldInstance field = Fields[i];
            bool entityAlive = ModClass.I.W.TryGetEntityByPid(field.SkillEntityKey, out Entity entity) &&
                               !entity.IsNull;
            if (!field.Detached && (!entityAlive || entity.Tags.Has<TagRecycle>()))
            {
                field.Detached = true;
                field.Duration = Mathf.Min(field.Duration, field.Elapsed + 0.25f);
            }
            field.Elapsed += deltaTime;
            field.View.Show(field.Center, field.Radius, field.Elapsed, field.Duration, deltaTime);
            if (field.Elapsed < field.Duration) continue;
            ReturnAndRemoveField(i);
        }
    }

    /// <summary>更新扩散波、水纹、漩涡与局部环线。</summary>
    private static void UpdateArcs(float deltaTime)
    {
        for (int i = Arcs.Count - 1; i >= 0; i--)
        {
            ArcInstance arc = Arcs[i];
            arc.Elapsed += deltaTime;
            float localTime = arc.Elapsed - arc.Delay;
            if (localTime < 0f)
            {
                arc.View.Hide();
                continue;
            }
            float progress = Mathf.Clamp01(localTime / arc.Duration);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);
            Color color = arc.Color;
            color.a *= arc.Alpha * fade;
            switch (arc.Kind)
            {
                case ArcAnimationKind.PurificationWave:
                {
                    float radius = Mathf.Lerp(arc.Radius * 0.1f, arc.Radius, Mathf.SmoothStep(0f, 1f, progress));
                    arc.View.ShowSegmented(arc.Center, radius, 0.06f, 12, 24f, 6f, 0f, 1f, color);
                    break;
                }
                case ArcAnimationKind.FillRipple:
                {
                    float radius = Mathf.Lerp(0.08f, 0.48f, Mathf.SmoothStep(0f, 1f, progress));
                    SharedBands[0] = new WorldArcBand(radius, 0.04f, localTime * 24f, 300f);
                    arc.View.ShowBands(arc.Center, new ArraySegment<WorldArcBand>(SharedBands, 0, 1), color);
                    break;
                }
                case ArcAnimationKind.DrainWhirlpool:
                {
                    float contraction = Mathf.Lerp(1f, 0.28f, Mathf.SmoothStep(0f, 1f, progress));
                    float rotation = -localTime * 150f;
                    SharedBands[0] = new WorldArcBand(0.45f * contraction, 0.045f, rotation, 210f);
                    SharedBands[1] = new WorldArcBand(0.30f * contraction, 0.04f, rotation + 50f, 160f);
                    SharedBands[2] = new WorldArcBand(0.17f * contraction, 0.035f, rotation + 95f, 100f);
                    arc.View.ShowBands(arc.Center, SharedBands, color);
                    break;
                }
                case ArcAnimationKind.LocalHalo:
                {
                    float radius = Mathf.Lerp(0.12f, 0.46f, Mathf.SmoothStep(0f, 1f, progress));
                    SharedBands[0] = new WorldArcBand(radius, 0.04f, 40f + localTime * 80f, 285f);
                    arc.View.ShowBands(arc.Center, new ArraySegment<WorldArcBand>(SharedBands, 0, 1), color);
                    break;
                }
            }
            if (progress < 1f) continue;
            ReturnAndRemoveArc(i);
        }
    }

    /// <summary>更新净土法阵的中心到目标窄带扫掠。</summary>
    private static void UpdateSweeps(float deltaTime)
    {
        for (int i = Sweeps.Count - 1; i >= 0; i--)
        {
            SweepInstance sweep = Sweeps[i];
            sweep.Elapsed += deltaTime;
            float progress = Mathf.Clamp01(sweep.Elapsed / sweep.Duration);
            Color color = sweep.Color;
            color.a *= Mathf.Sin(progress * Mathf.PI);
            sweep.View.Show(sweep.Start, sweep.End, Mathf.SmoothStep(0f, 1f, progress), 0.10f, color);
            if (progress < 1f) continue;
            ReturnAndRemoveSweep(i);
        }
    }

    /// <summary>更新所有实际精灵粒子的移动、旋转、缩放和淡出。</summary>
    private static void UpdateParticles(float deltaTime)
    {
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            SpriteParticle particle = Particles[i];
            particle.Elapsed += deltaTime;
            if (particle.Elapsed < particle.Delay)
            {
                particle.View.Hide();
                continue;
            }
            float localTime = particle.Elapsed - particle.Delay;
            float progress = Mathf.Clamp01(localTime / particle.Lifetime);
            particle.Velocity += particle.Acceleration * deltaTime;
            particle.Position += particle.Velocity * deltaTime;
            particle.Rotation += particle.AngularVelocity * deltaTime;
            float worldSize = Mathf.Lerp(particle.StartWorldSize, particle.EndWorldSize, progress);
            float spriteSize = Mathf.Max(particle.Sprite.bounds.size.x, particle.Sprite.bounds.size.y);
            Color color = particle.Color;
            color.a *= 1f - Mathf.SmoothStep(0.55f, 1f, progress);
            particle.View.Show(
                particle.Sprite,
                particle.Position,
                particle.Rotation,
                worldSize / Mathf.Max(0.001f, spriteSize),
                color);
            if (progress < 1f) continue;
            ReturnAndRemoveParticle(i);
        }
    }

    /// <summary>更新池化逐帧动画并在最后一帧后回收。</summary>
    private static void UpdateFlipbooks(float deltaTime)
    {
        for (int i = Flipbooks.Count - 1; i >= 0; i--)
        {
            FlipbookInstance flipbook = Flipbooks[i];
            flipbook.Elapsed += deltaTime;
            int frameIndex = Mathf.FloorToInt(flipbook.Elapsed / flipbook.FrameInterval);
            if (frameIndex >= flipbook.Frames.Length)
            {
                ReturnAndRemoveFlipbook(i);
                continue;
            }
            Sprite frame = flipbook.Frames[frameIndex];
            float spriteSize = Mathf.Max(frame.bounds.size.x, frame.bounds.size.y);
            flipbook.View.Show(frame, flipbook.Position, 0f,
                flipbook.WorldSize / Mathf.Max(0.001f, spriteSize), flipbook.Color);
        }
    }

    /// <summary>播放生命恢复的叶片、光籽和局部弧光。</summary>
    private static void SpawnHealing(Vector3 position, SkillWorldVisualProfile profile, int leafCount, bool halo)
    {
        if (halo) SpawnArc(ArcAnimationKind.LocalHalo, position, 0.5f, 0f, 0.38f, profile.GlowColor, 0.9f);
        for (int i = 0; i < leafCount; i++)
        {
            Vector3 velocity = new(Randy.randomFloat(-0.18f, 0.18f), Randy.randomFloat(0.18f, 0.42f));
            string path = i % 2 == 0 ? $"{PrimitiveRoot}/leaf_pointed" : $"{PrimitiveRoot}/leaf_heart";
            SpawnParticle(path, position + RandomOffset(0.12f), velocity, Vector3.zero,
                0.55f, 0.12f, 0.07f, Randy.randomFloat(0f, 360f), Randy.randomFloat(-90f, 90f),
                profile.PrimaryColor);
        }
        SpawnParticle($"{PrimitiveRoot}/seed_light", position, new Vector3(0f, 0.25f), Vector3.zero,
            0.45f, 0.10f, 0.04f, 0f, 35f, profile.GlowColor);
    }

    /// <summary>播放净化目标时向外散开的青白碎光。</summary>
    private static void SpawnPurification(Vector3 position, SkillWorldVisualProfile profile)
    {
        SpawnArc(ArcAnimationKind.LocalHalo, position, 0.5f, 0f, 0.32f, profile.GlowColor, 0.85f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = Direction(i * 90f + Randy.randomFloat(-15f, 15f));
            SpawnParticle($"{PrimitiveRoot}/purify_shard", position, direction * 0.42f, Vector3.zero,
                0.42f, 0.11f, 0.04f, i * 90f, 120f, profile.PrimaryColor);
        }
    }

    /// <summary>播放战意祝福的红橙火星。</summary>
    private static void SpawnBattleBlessing(Vector3 position, SkillWorldVisualProfile profile)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3 velocity = new(Randy.randomFloat(-0.28f, 0.28f), Randy.randomFloat(0.18f, 0.52f));
            SpawnParticle(i % 2 == 0 ? $"{PrimitiveRoot}/spark_short" : $"{PrimitiveRoot}/spark_hook",
                position + RandomOffset(0.1f), velocity, new Vector3(0f, -0.25f),
                0.48f, 0.11f, 0.035f, Randy.randomFloat(0f, 360f), 180f, profile.PrimaryColor);
        }
    }

    /// <summary>播放守护祝福的金属菱片。</summary>
    private static void SpawnGuardBlessing(Vector3 position, SkillWorldVisualProfile profile)
    {
        SpawnArc(ArcAnimationKind.LocalHalo, position, 0.5f, 0f, 0.38f, profile.SecondaryColor, 0.8f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = Direction(45f + i * 90f);
            SpawnParticle($"{PrimitiveRoot}/metal_shard", position, direction * 0.25f, Vector3.zero,
                0.5f, 0.13f, 0.08f, 45f + i * 90f, 80f, profile.GlowColor);
        }
    }

    /// <summary>播放迅捷祝福的短促青色流线。</summary>
    private static void SpawnHasteBlessing(Vector3 position, SkillWorldVisualProfile profile)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector3 start = position + new Vector3(Randy.randomFloat(-0.2f, 0.1f), Randy.randomFloat(-0.18f, 0.2f));
            SpawnParticle($"{PrimitiveRoot}/wind_streak", start, new Vector3(0.65f, Randy.randomFloat(-0.08f, 0.08f)),
                Vector3.zero, 0.36f, 0.16f, 0.05f, 0f, 0f, profile.PrimaryColor, i * 0.025f);
        }
    }

    /// <summary>按抬升或降低方向播放不同运动的尘土和碎石。</summary>
    private static void SpawnTerrain(Vector3 position, SkillWorldVisualProfile profile, bool raise)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 radial = Direction(i * 90f + Randy.randomFloat(-30f, 30f));
            Vector3 start = position + radial * (raise ? 0.06f : 0.28f);
            Vector3 velocity = raise
                ? radial * 0.22f + new Vector3(0f, 0.28f)
                : -radial * 0.35f + new Vector3(0f, -0.12f);
            string dust = $"{PrimitiveRoot}/dust_{i % 3}";
            SpawnParticle(dust, start, velocity, raise ? new Vector3(0f, -0.22f) : Vector3.zero,
                0.52f, 0.12f, 0.04f, Randy.randomFloat(0f, 360f), 100f, profile.PrimaryColor);
        }
        for (int i = 0; i < 2; i++)
        {
            Vector3 radial = Direction(i * 180f + Randy.randomFloat(-50f, 50f));
            Vector3 velocity = raise ? radial * 0.18f + new Vector3(0f, 0.34f) : -radial * 0.28f;
            SpawnParticle($"{PrimitiveRoot}/rock_{i}", position + radial * 0.12f, velocity,
                new Vector3(0f, -0.35f), 0.58f, 0.16f, 0.08f,
                Randy.randomFloat(0f, 360f), Randy.randomFloat(-180f, 180f), profile.SecondaryColor);
        }
    }

    /// <summary>播放填水的扩散水纹和上浮气泡。</summary>
    private static void SpawnFillWater(Vector3 position, SkillWorldVisualProfile profile)
    {
        SpawnArc(ArcAnimationKind.FillRipple, position, 0.5f, 0f, 0.48f, profile.PrimaryColor, 0.9f);
        for (int i = 0; i < 2; i++)
        {
            string bubble = i == 0 ? $"{PrimitiveRoot}/bubble_hollow" : $"{PrimitiveRoot}/bubble_point";
            SpawnParticle(bubble, position + RandomOffset(0.16f),
                new Vector3(Randy.randomFloat(-0.05f, 0.05f), Randy.randomFloat(0.18f, 0.32f)),
                Vector3.zero, 0.52f, 0.10f, 0.05f, 0f, 20f, profile.GlowColor, i * 0.05f);
        }
    }

    /// <summary>播放排水的三层收缩漩涡和向内下沉气泡。</summary>
    private static void SpawnDrainWater(Vector3 position, SkillWorldVisualProfile profile)
    {
        SpawnArc(ArcAnimationKind.DrainWhirlpool, position, 0.5f, 0f, 0.62f, profile.PrimaryColor, 0.95f);
        for (int i = 0; i < 3; i++)
        {
            Vector3 radial = Direction(i * 120f + Randy.randomFloat(-20f, 20f));
            Vector3 start = position + radial * 0.28f;
            SpawnParticle(i % 2 == 0 ? $"{PrimitiveRoot}/bubble_hollow" : $"{PrimitiveRoot}/bubble_point",
                start, -radial * 0.35f + new Vector3(0f, -0.08f), Vector3.zero,
                0.55f, 0.10f, 0.03f, 0f, -60f, profile.GlowColor, i * 0.04f);
        }
    }

    /// <summary>播放八帧萌芽动画并抛出不同形状的真实叶片。</summary>
    private static void SpawnNatureGrowth(Vector3 position, SkillWorldVisualProfile profile)
    {
        SpawnFlipbook($"{PrimitiveRoot}/sprout", position + new Vector3(0f, -0.18f), 0.07f, 0.62f, Color.white);
        string[] leaves =
        {
            $"{PrimitiveRoot}/leaf_pointed",
            $"{PrimitiveRoot}/leaf_broad",
            $"{PrimitiveRoot}/leaf_heart"
        };
        int count = Randy.randomInt(2, 5);
        for (int i = 0; i < count; i++)
        {
            Vector3 velocity = new(Randy.randomFloat(-0.28f, 0.28f), Randy.randomFloat(0.18f, 0.42f));
            SpawnParticle(leaves[i % leaves.Length], position + RandomOffset(0.1f), velocity,
                new Vector3(0f, -0.18f), 0.62f, 0.13f, 0.07f,
                Randy.randomFloat(0f, 360f), Randy.randomFloat(-150f, 150f), profile.PrimaryColor);
        }
    }

    /// <summary>播放从作物上方落下的原版肥料颗粒，并用短促脉冲强调成熟结算。</summary>
    private static void SpawnFertilize(Vector3 position, SkillWorldVisualProfile profile)
    {
        SpawnArc(ArcAnimationKind.LocalHalo, position, 0.5f, 0f, 0.36f, profile.PrimaryColor, 0.75f);
        Sprite[] fertilizerFrames = ResolveFrames("drops/drop_fertilizer");
        if (fertilizerFrames.Length > 0)
        {
            for (int i = 0; i < 5; i++)
            {
                Sprite frame = fertilizerFrames[i % fertilizerFrames.Length];
                Vector3 start = position + new Vector3(
                    Randy.randomFloat(-0.24f, 0.24f),
                    Randy.randomFloat(0.28f, 0.48f));
                SpawnParticle(frame, start,
                    new Vector3(Randy.randomFloat(-0.08f, 0.08f), Randy.randomFloat(-0.16f, -0.08f)),
                    new Vector3(0f, -0.2f),
                    0.58f, 0.14f, 0.07f,
                    Randy.randomFloat(0f, 360f), Randy.randomFloat(-80f, 80f), Color.white, i * 0.035f);
            }
        }
        SpawnParticle($"{PrimitiveRoot}/seed_light", position + new Vector3(0f, -0.05f),
            new Vector3(0f, 0.22f), Vector3.zero,
            0.42f, 0.09f, 0.035f, 0f, 30f, profile.GlowColor);
    }

    /// <summary>按净土实际移除的污染类别播放窄带扫掠和对应碎屑。</summary>
    private static void SpawnCleanLand(in SkillWorldVisualEvent visualEvent)
    {
        SpawnSweep(visualEvent.Center, visualEvent.Position, visualEvent.Profile.GlowColor);
        SkillEffectOutcomeFlags flags = visualEvent.Result.Flags;
        if ((flags & (SkillEffectOutcomeFlags.FireRemoved | SkillEffectOutcomeFlags.BurnRemoved)) != 0)
        {
            Color ember = new Color32(238, 91, 42, 255);
            for (int i = 0; i < 3; i++)
            {
                SpawnParticle(i % 2 == 0 ? $"{PrimitiveRoot}/spark_short" : $"{PrimitiveRoot}/spark_hook",
                    visualEvent.Position + RandomOffset(0.08f),
                    new Vector3(Randy.randomFloat(-0.16f, 0.16f), Randy.randomFloat(0.18f, 0.38f)),
                    new Vector3(0f, -0.2f), 0.46f, 0.10f, 0.03f,
                    Randy.randomFloat(0f, 360f), 150f, ember);
            }
        }
        if ((flags & SkillEffectOutcomeFlags.FrozenRemoved) != 0)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 direction = Direction(i * 120f + 30f);
                SpawnParticle(i % 2 == 0 ? $"{PrimitiveRoot}/ice_shard_tall" : $"{PrimitiveRoot}/ice_shard_short",
                    visualEvent.Position, direction * 0.3f, Vector3.zero,
                    0.5f, 0.12f, 0.04f, i * 120f, 120f, visualEvent.Profile.PrimaryColor);
            }
        }
        if ((flags & SkillEffectOutcomeFlags.WastelandRemoved) != 0)
        {
            for (int i = 0; i < 3; i++)
            {
                SpawnParticle(i % 2 == 0 ? $"{PrimitiveRoot}/wasteland_debris_a" :
                        $"{PrimitiveRoot}/wasteland_debris_b",
                    visualEvent.Position + RandomOffset(0.12f),
                    new Vector3(Randy.randomFloat(-0.18f, 0.18f), Randy.randomFloat(0.18f, 0.35f)),
                    Vector3.zero, 0.55f, 0.13f, 0.06f,
                    Randy.randomFloat(0f, 360f), 80f, visualEvent.Profile.SecondaryColor);
            }
        }
        if ((flags & SkillEffectOutcomeFlags.HeatRemoved) != 0)
        {
            for (int i = 0; i < 2; i++)
            {
                SpawnParticle($"{PrimitiveRoot}/bubble_hollow", visualEvent.Position + RandomOffset(0.08f),
                    new Vector3(Randy.randomFloat(-0.06f, 0.06f), 0.28f), Vector3.zero,
                    0.5f, 0.11f, 0.06f, 0f, 0f, visualEvent.Profile.GlowColor, i * 0.04f);
            }
        }
    }

    /// <summary>创建一个圆弧动画实例。</summary>
    private static void SpawnArc(
        ArcAnimationKind kind,
        Vector3 center,
        float radius,
        float delay,
        float duration,
        Color color,
        float alpha)
    {
        if (Arcs.Count >= MaxArcs) return;
        WorldArcRenderer view = ArcPool.Count > 0
            ? ArcPool.Pop()
            : WorldArcRenderer.Create(visualRoot, "TransientArc", -4);
        Arcs.Add(new ArcInstance
        {
            Kind = kind,
            Center = center,
            Radius = radius,
            Delay = delay,
            Duration = Mathf.Max(0.05f, duration),
            Color = color,
            Alpha = alpha,
            View = view
        });
    }

    /// <summary>创建一个从法阵中心扫向目标地块的窄带实例。</summary>
    private static void SpawnSweep(Vector3 start, Vector3 end, Color color)
    {
        if (Sweeps.Count >= MaxSweeps || Vector2.Distance(start, end) < 0.05f) return;
        WorldSweepRenderer view = SweepPool.Count > 0
            ? SweepPool.Pop()
            : WorldSweepRenderer.Create(visualRoot);
        Sweeps.Add(new SweepInstance
        {
            Start = start,
            End = end,
            Duration = 0.28f,
            Color = color,
            View = view
        });
    }

    /// <summary>从资源路径创建一个具有独立运动参数的真实精灵粒子。</summary>
    private static void SpawnParticle(
        string spritePath,
        Vector3 position,
        Vector3 velocity,
        Vector3 acceleration,
        float lifetime,
        float startWorldSize,
        float endWorldSize,
        float rotation,
        float angularVelocity,
        Color color,
        float delay = 0f)
    {
        Sprite sprite = ResolveSprite(spritePath);
        if (sprite == null) return;
        SpawnParticle(sprite, position, velocity, acceleration, lifetime, startWorldSize, endWorldSize,
            rotation, angularVelocity, color, delay);
    }

    /// <summary>以已经解析的精灵创建粒子，供多帧原版资源选择具体帧后复用。</summary>
    private static void SpawnParticle(
        Sprite sprite,
        Vector3 position,
        Vector3 velocity,
        Vector3 acceleration,
        float lifetime,
        float startWorldSize,
        float endWorldSize,
        float rotation,
        float angularVelocity,
        Color color,
        float delay = 0f)
    {
        if (Particles.Count >= MaxParticles || sprite == null) return;
        WorldSpriteView view = SpritePool.Count > 0 ? SpritePool.Pop() : WorldSpriteView.Create(visualRoot);
        SpriteParticle particle = ParticleStatePool.Count > 0 ? ParticleStatePool.Pop() : new SpriteParticle();
        particle.Sprite = sprite;
        particle.Position = position;
        particle.Velocity = velocity;
        particle.Acceleration = acceleration;
        particle.Lifetime = Mathf.Max(0.05f, lifetime);
        particle.StartWorldSize = startWorldSize;
        particle.EndWorldSize = endWorldSize;
        particle.Rotation = rotation;
        particle.AngularVelocity = angularVelocity;
        particle.Color = color;
        particle.Delay = Mathf.Max(0f, delay);
        particle.Elapsed = 0f;
        particle.View = view;
        Particles.Add(particle);
    }

    /// <summary>创建一个不循环的局部逐帧动画。</summary>
    private static void SpawnFlipbook(string path, Vector3 position, float frameInterval, float worldSize, Color color)
    {
        if (Flipbooks.Count >= MaxFlipbooks) return;
        Sprite[] frames = ResolveFrames(path);
        if (frames.Length == 0) return;
        WorldSpriteView view = SpritePool.Count > 0 ? SpritePool.Pop() : WorldSpriteView.Create(visualRoot);
        FlipbookInstance flipbook = FlipbookStatePool.Count > 0 ? FlipbookStatePool.Pop() : new FlipbookInstance();
        flipbook.Frames = frames;
        flipbook.Position = position;
        flipbook.FrameInterval = Mathf.Max(0.01f, frameInterval);
        flipbook.WorldSize = worldSize;
        flipbook.Color = color;
        flipbook.Elapsed = 0f;
        flipbook.View = view;
        Flipbooks.Add(flipbook);
    }

    /// <summary>解析并缓存一个按文件名排序的逐帧动画。</summary>
    private static Sprite[] ResolveFrames(string path)
    {
        if (FrameCache.TryGetValue(path, out Sprite[] cached)) return cached;
        Sprite[] frames = SkillEntityAsset.LoadOrderedFrames(path);
        FrameCache[path] = frames;
        return frames;
    }

    /// <summary>创建一个给定角度的二维单位方向。</summary>
    private static Vector3 Direction(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    /// <summary>创建一个位于圆形范围内的随机二维偏移。</summary>
    private static Vector3 RandomOffset(float radius)
    {
        return Direction(Randy.randomFloat(0f, 360f)) * Randy.randomFloat(0f, radius);
    }

    /// <summary>回收并移除指定法阵实例。</summary>
    private static void ReturnAndRemoveField(int index)
    {
        FieldInstance field = Fields[index];
        Fields.RemoveAt(index);
        ReturnField(field);
    }

    /// <summary>回收一个法阵视图。</summary>
    private static void ReturnField(FieldInstance field)
    {
        field.View.Hide();
        FieldPool.Push(field.View);
    }

    /// <summary>回收并移除指定圆弧实例。</summary>
    private static void ReturnAndRemoveArc(int index)
    {
        ArcInstance arc = Arcs[index];
        Arcs.RemoveAt(index);
        ReturnArc(arc);
    }

    /// <summary>回收一个圆弧视图。</summary>
    private static void ReturnArc(ArcInstance arc)
    {
        arc.View.Hide();
        ArcPool.Push(arc.View);
    }

    /// <summary>回收并移除指定扫掠实例。</summary>
    private static void ReturnAndRemoveSweep(int index)
    {
        SweepInstance sweep = Sweeps[index];
        Sweeps.RemoveAt(index);
        ReturnSweep(sweep);
    }

    /// <summary>回收一个扫掠视图。</summary>
    private static void ReturnSweep(SweepInstance sweep)
    {
        sweep.View.Hide();
        SweepPool.Push(sweep.View);
    }

    /// <summary>回收并移除指定精灵粒子。</summary>
    private static void ReturnAndRemoveParticle(int index)
    {
        SpriteParticle particle = Particles[index];
        Particles.RemoveAt(index);
        ReturnParticle(particle);
    }

    /// <summary>回收一个精灵粒子视图和状态对象。</summary>
    private static void ReturnParticle(SpriteParticle particle)
    {
        particle.View.Hide();
        SpritePool.Push(particle.View);
        particle.View = null;
        particle.Sprite = null;
        ParticleStatePool.Push(particle);
    }

    /// <summary>回收并移除指定逐帧动画。</summary>
    private static void ReturnAndRemoveFlipbook(int index)
    {
        FlipbookInstance flipbook = Flipbooks[index];
        Flipbooks.RemoveAt(index);
        ReturnFlipbook(flipbook);
    }

    /// <summary>回收一个逐帧动画视图和状态对象。</summary>
    private static void ReturnFlipbook(FlipbookInstance flipbook)
    {
        flipbook.View.Hide();
        SpritePool.Push(flipbook.View);
        flipbook.View = null;
        flipbook.Frames = null;
        FlipbookStatePool.Push(flipbook);
    }

    /// <summary>池化圆弧动画的具体运动方式。</summary>
    private enum ArcAnimationKind : byte
    {
        PurificationWave,
        FillRipple,
        DrainWhirlpool,
        LocalHalo,
    }

    /// <summary>一个持续法阵的运行时状态。</summary>
    private sealed class FieldInstance
    {
        public long SkillEntityKey;
        public SkillWorldVisualProfile Profile;
        public Vector3 Center;
        public float Radius;
        public float Duration;
        public float Elapsed;
        public bool Detached;
        public WorldGlyphFieldView View;
    }

    /// <summary>一个短时圆弧动画的运行时状态。</summary>
    private sealed class ArcInstance
    {
        public ArcAnimationKind Kind;
        public Vector3 Center;
        public float Radius;
        public float Delay;
        public float Duration;
        public float Elapsed;
        public Color Color;
        public float Alpha;
        public WorldArcRenderer View;
    }

    /// <summary>一个窄带扫掠动画的运行时状态。</summary>
    private sealed class SweepInstance
    {
        public Vector3 Start;
        public Vector3 End;
        public float Duration;
        public float Elapsed;
        public Color Color;
        public WorldSweepRenderer View;
    }

    /// <summary>一个真实精灵碎屑的运行时状态。</summary>
    private sealed class SpriteParticle
    {
        public Sprite Sprite;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Acceleration;
        public float Lifetime;
        public float Delay;
        public float Elapsed;
        public float StartWorldSize;
        public float EndWorldSize;
        public float Rotation;
        public float AngularVelocity;
        public Color Color;
        public WorldSpriteView View;
    }

    /// <summary>一个不循环逐帧动画的运行时状态。</summary>
    private sealed class FlipbookInstance
    {
        public Sprite[] Frames;
        public Vector3 Position;
        public float FrameInterval;
        public float Elapsed;
        public float WorldSize;
        public Color Color;
        public WorldSpriteView View;
    }

    /// <summary>等待范围波传播到目标位置的局部视觉事件。</summary>
    private sealed class DelayedEvent
    {
        public readonly SkillWorldVisualEvent Event;
        public float Remaining;

        public DelayedEvent(SkillWorldVisualEvent visualEvent)
        {
            Event = visualEvent;
            Remaining = visualEvent.Delay;
        }
    }
}
