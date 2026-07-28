using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Cultiway.Core.Performance;

[Flags]
internal enum ActorPresentationFlags : uint
{
    None = 0,
    Alive = 1 << 0,
    HasSpriteRenderer = 1 << 1,
    VisibleOnMinimap = 1 << 2,
    InMagnet = 1 << 3,
    InsideSomething = 1 << 4,
    Moving = 1 << 5,
    NormalRender = 1 << 6,
    HasItem = 1 << 7,
    HasShadow = 1 << 8,
    HasAvatar = 1 << 9,
    Favorite = 1 << 10,
    ArmyCaptain = 1 << 11,
    JustAte = 1 << 12,
    Socializing = 1 << 13,
    Muted = 1 << 14,
    HasHappinessIcon = 1 << 15,
    HasTaskIcon = 1 << 16,
    UnexploredAugmentation = 1 << 17,
    Flying = 1 << 18
}

internal readonly struct ActorPresentationHandle : IEquatable<ActorPresentationHandle>
{
    internal ActorPresentationHandle(int worldGeneration, long actorId)
    {
        WorldGeneration = worldGeneration;
        ActorId = actorId;
    }

    internal int WorldGeneration { get; }
    internal long ActorId { get; }

    public bool Equals(ActorPresentationHandle other)
    {
        return WorldGeneration == other.WorldGeneration &&
               ActorId == other.ActorId;
    }

    public override bool Equals(object obj)
    {
        return obj is ActorPresentationHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (WorldGeneration * 397) ^ ActorId.GetHashCode();
        }
    }
}

internal struct ActorPresentationSample
{
    internal ActorPresentationHandle Handle;
    // 只用于把原版少数 Actor 参数映射回稳定句柄，渲染不得从该引用读取状态。
    internal Actor ActorReference;
    internal Vector2 Position;
    internal Vector2 NextStepPosition;
    internal Vector2 ShakeOffset;
    internal Vector2 JumpOffset;
    internal Vector3 Scale;
    internal Vector3 Rotation;
    internal Color Color;
    internal bool Flip;
    internal float PositionHeight;
    internal float MovementSpeed;
    internal int ZoneId;
    internal Sprite MainSprite;
    internal Sprite ItemSprite;
    internal Sprite ShadowSprite;
    internal Vector2 ItemOffset;
    internal Vector2 ShadowSize;
    internal Vector2 FrameUnitSize;
    internal Vector2 HeadOffset;
    internal Transform AvatarTransform;
    internal float HealthRatio;
    internal float ScaleMod;
    internal float VisualScale;
    internal Sprite FlyingVehicleSprite;
    internal bool FlyingVehicleVertical;
    internal Sprite FlyingScaleReferenceSprite;
    internal Color BannerColor;
    internal double JustAteAt;
    internal Sprite JustAteSprite;
    internal double SocialStartedAt;
    internal Sprite SocialBubbleSprite;
    internal Sprite SocialTopicSprite;
    internal Sprite HappinessSprite;
    internal Sprite TaskSprite;
    internal MetaType MetaType;
    internal long MetaId;
    internal Color MetaColor;
    internal bool MetaFavorite;
    internal int LightStart;
    internal int LightCount;
    internal int StatusStart;
    internal int StatusCount;
    internal ActorPresentationFlags Flags;

    internal bool HasFlag(ActorPresentationFlags flag)
    {
        return (Flags & flag) != 0;
    }
}

internal struct ActorStatusPresentationSample
{
    internal int FrameStart;
    internal int FrameCount;
    internal int CapturedFrame;
    internal float TimeUntilNextFrame;
    internal float FrameInterval;
    internal float Scale;
    internal Vector2 BaseOffset;
    internal float PositionZ;
    internal Material Material;
    internal bool Animated;
    internal bool AnimateWhenPaused;
    internal bool Loop;
    internal bool UseParentRotation;
    internal bool Flip;
    internal bool HasRotation;
}

internal struct ActorStatusFramePresentationSample
{
    internal Sprite Sprite;
    internal Vector3 Offset;
    internal float RotationZ;
}

internal struct ActorLightPresentationSample
{
    internal Vector2 Offset;
    internal float Scale;
}

internal struct BuildingPresentationSample
{
    internal long BuildingId;
    // 只用于兼容原版可见建筑数组与灯光回调，渲染数据仍来自下方值拷贝。
    internal Building BuildingReference;
    internal int ZoneId;
    internal Vector3 Position;
    internal Vector3 Scale;
    internal Vector3 Rotation;
    internal Sprite MainSprite;
    internal Sprite ColoredSprite;
    internal Material Material;
    internal Color Color;
    internal bool Flip;
    internal bool HasShadow;
    internal Sprite ShadowSprite;
    internal bool Usable;
    internal bool UnderConstruction;
    internal bool Stockpile;
    internal bool StockpileVisible;
    internal Vector2 StockpileOffset;
    internal Color StockpileColor;
    internal int StockpileResourceStart;
    internal int StockpileResourceCount;
    internal Sprite LightWindowSprite;
    internal bool LightWindowVisible;
    internal int LightStart;
    internal int LightCount;
    internal int StatusStart;
    internal int StatusCount;
    internal bool Sparkle;
}

internal struct StockpileResourcePresentationSample
{
    internal Sprite Sprite;
    internal int IconCount;
}

internal struct BuildingLightPresentationSample
{
    internal Vector2 Position;
    internal float Scale;
}

internal struct WorldLightPresentationSample
{
    internal Vector2 Position;
    internal float Scale;
    internal bool UseEraColor;
}

internal struct FirePresentationSample
{
    internal Vector3 Position;
    internal int AnimationSet;
    internal int RandomSeed;
}

internal struct ProjectilePresentationSample
{
    internal long ProjectileId;
    internal int RenderSeed;
    internal Vector3 Position;
    internal Vector3 ShadowPosition;
    internal Vector3 Velocity;
    internal Quaternion Rotation;
    internal float Height;
    internal float Scale;
    internal float TargetScale;
    internal float Alpha;
    internal float ShadowAngle;
    internal Sprite[] Frames;
    internal float AnimationSpeed;
    internal bool Animated;
    internal bool DeadAnimation;
    internal Sprite ShadowSprite;
}

internal struct ResourceThrowPresentationSample
{
    internal Vector2 Start;
    internal Vector2 End;
    internal double StartTime;
    internal double EndTime;
    internal float Height;
    internal Sprite Sprite;
}

/// <summary>
/// 一份发布后只读的角色表现快照。数组与索引只在该缓冲槽重新成为 writer 后复用。
/// </summary>
internal sealed class ActorPresentationSnapshot
{
    private ActorPresentationSample[] samples = Array.Empty<ActorPresentationSample>();
    private ActorStatusPresentationSample[] statuses =
        Array.Empty<ActorStatusPresentationSample>();
    private ActorStatusFramePresentationSample[] statusFrames =
        Array.Empty<ActorStatusFramePresentationSample>();
    private ActorLightPresentationSample[] lights =
        Array.Empty<ActorLightPresentationSample>();
    private BuildingPresentationSample[] buildings =
        Array.Empty<BuildingPresentationSample>();
    private StockpileResourcePresentationSample[] stockpileResources =
        Array.Empty<StockpileResourcePresentationSample>();
    private BuildingLightPresentationSample[] buildingLights =
        Array.Empty<BuildingLightPresentationSample>();
    private WorldLightPresentationSample[] worldLights =
        Array.Empty<WorldLightPresentationSample>();
    private FirePresentationSample[] fires =
        Array.Empty<FirePresentationSample>();
    private ProjectilePresentationSample[] projectiles =
        Array.Empty<ProjectilePresentationSample>();
    private ResourceThrowPresentationSample[] resourceThrows =
        Array.Empty<ResourceThrowPresentationSample>();
    private readonly Dictionary<long, int> indexes = new(4096);
    private int statusCount;
    private int statusFrameCount;
    private int lightCount;
    private int buildingCount;
    private int stockpileResourceCount;
    private int buildingLightCount;
    private int worldLightCount;
    private int fireCount;
    private int projectileCount;
    private int resourceThrowCount;

    internal int WorldGeneration { get; private set; }
    internal long TickSequence { get; private set; }
    internal double SimulationTimeValue { get; private set; }
    internal long CapturedAt { get; private set; }
    internal int Count { get; private set; }
    internal int StatusCount => statusCount;
    internal int StatusFrameCount => statusFrameCount;
    internal int LightCount => lightCount;
    internal int BuildingCount => buildingCount;
    internal int StockpileResourceCount => stockpileResourceCount;
    internal int BuildingLightCount => buildingLightCount;
    internal int WorldLightCount => worldLightCount;
    internal int FireCount => fireCount;
    internal int ProjectileCount => projectileCount;
    internal int ResourceThrowCount => resourceThrowCount;

    internal void Capture(MapBox world, long tickSequence)
    {
        if (world?.units == null)
        {
            throw new InvalidOperationException("无法从尚未初始化的世界采集角色表现快照");
        }

        world.units.checkContainer();
        world.units.prepareArray();
        Actor[] actors = world.units.getSimpleArray();
        int actorCount = world.units.Count;
        EnsureCapacity(actorCount);
        indexes.Clear();
        statusCount = 0;
        statusFrameCount = 0;
        lightCount = 0;
        buildingCount = 0;
        stockpileResourceCount = 0;
        buildingLightCount = 0;
        worldLightCount = 0;
        fireCount = 0;
        projectileCount = 0;
        resourceThrowCount = 0;
        MetaType requestedMetaType = GetRequestedMetaType();
        bool checkUnexplored =
            PowerLibrary.inspect_unit?.isSelected() == true &&
            !WorldLawLibrary.world_law_cursed_world.isEnabled();

        int worldGeneration = SimulationTime.Generation;
        double sessionTime = world.getCurSessionTime();
        int capturedCount = 0;
        for (int i = 0; i < actorCount; i++)
        {
            Actor actor = actors[i];
            if (actor?.data == null || !actor.exists)
            {
                continue;
            }

            long actorId = actor.data.id;
            ActorPresentationFlags flags = ActorPresentationFlags.None;
            if (actor.isAlive())
            {
                flags |= ActorPresentationFlags.Alive;
            }

            ActorAsset asset = actor.asset;
            if (asset?.has_sprite_renderer == true)
            {
                flags |= ActorPresentationFlags.HasSpriteRenderer;
            }

            if (asset?.visible_on_minimap == true)
            {
                flags |= ActorPresentationFlags.VisibleOnMinimap;
            }

            if (actor.isInMagnet())
            {
                flags |= ActorPresentationFlags.InMagnet;
            }

            if (actor.isInsideSomething())
            {
                flags |= ActorPresentationFlags.InsideSomething;
            }

            if (actor.is_moving)
            {
                flags |= ActorPresentationFlags.Moving;
            }

            if (asset.has_avatar_prefab && actor.avatar != null)
            {
                flags |= ActorPresentationFlags.HasAvatar;
            }

            if (actor.isFavorite() && !asset.hide_favorite_icon)
            {
                flags |= ActorPresentationFlags.Favorite;
            }

            if (actor.is_army_captain)
            {
                flags |= ActorPresentationFlags.ArmyCaptain;
            }

            bool normalRender = !asset.ignore_generic_render;
            bool hasItem = actor.checkHasRenderedItem();
            Sprite itemSprite = null;
            if (hasItem)
            {
                Sprite renderedItemSprite = actor.getRenderedItemSprite();
                IHandRenderer handRenderer = actor.getCachedHandRendererAsset();
                if (renderedItemSprite != null && handRenderer != null)
                {
                    int colorId = -900000;
                    if (handRenderer.is_colored)
                    {
                        colorId = actor.kingdom.getColor().GetHashCode();
                    }

                    itemSprite = DynamicSprites.getCachedAtlasItemSprite(
                        DynamicSprites.getItemSpriteID(renderedItemSprite, colorId),
                        renderedItemSprite);
                    flags |= ActorPresentationFlags.HasItem;
                }
            }

            Sprite mainSprite = null;
            if (normalRender)
            {
                mainSprite = actor.calculateMainSprite();
                mainSprite = actor.hasColoredSprite()
                    ? actor.calculateColoredSprite(mainSprite)
                    : mainSprite;
                flags |= ActorPresentationFlags.NormalRender;
            }

            float visualScale = actor.stats[strings.S.scale];
            Sprite flyingVehicleSprite = null;
            bool flyingVehicleVertical = false;
            Sprite flyingScaleReferenceSprite = null;
            if (actor.data.hasFlag(
                    global::Cultiway.Content.Const.ContentActorDataKeys
                        .IsFlying_flag))
            {
                flags |= ActorPresentationFlags.Flying;
                if (asset.has_override_sprite)
                {
                    flyingScaleReferenceSprite =
                        mainSprite ?? actor.calculateMainSprite();
                }
                else
                {
                    actor.checkAnimationContainer();
                    Sprite[] idleFrames =
                        actor.animation_container?.idle?.frames;
                    flyingScaleReferenceSprite =
                        idleFrames is { Length: > 0 }
                            ? idleFrames[0]
                            : mainSprite;
                }

                if (actor.hasWeapon())
                {
                    flyingVehicleSprite =
                        ItemRendering.getItemMainSpriteFrame(
                            actor.getWeaponAsset());
                    flyingVehicleVertical =
                        flyingVehicleSprite != null &&
                        flyingVehicleSprite.rect.width <
                        flyingVehicleSprite.rect.height;
                }
            }

            AnimationFrameData frameData = actor.getAnimationFrameData();
            Sprite shadowSprite = null;
            Vector2 shadowSize = default;
            bool hasShadow = false;
            if (actor.show_shadow)
            {
                ActorTextureSubAsset textureAsset =
                    !actor.hasSubspecies() || !actor.subspecies.has_mutation_reskin
                        ? asset.texture_asset
                        : actor.subspecies.mutation_skin_asset.texture_asset;
                hasShadow = textureAsset.shadow;
                if (hasShadow)
                {
                    if (actor.isEgg())
                    {
                        shadowSprite = textureAsset.shadow_sprite_egg;
                        shadowSize = textureAsset.shadow_size_egg;
                    }
                    else if (actor.isBaby())
                    {
                        shadowSprite = textureAsset.shadow_sprite_baby;
                        shadowSize = textureAsset.shadow_size_baby;
                    }
                    else
                    {
                        shadowSprite = textureAsset.shadow_sprite;
                        shadowSize = textureAsset.shadow_size;
                    }

                    flags |= ActorPresentationFlags.HasShadow;
                }
            }

            double justAteAt = actor.timestamp_session_ate_food;
            Sprite justAteSprite = null;
            if (justAteAt > 0.0 && sessionTime - justAteAt <= 1.0)
            {
                ResourceAsset resource = AssetManager.resources.get(actor.ate_last_item_id);
                if (resource != null)
                {
                    justAteSprite = resource.getSpriteIcon();
                    flags |= ActorPresentationFlags.JustAte;
                }
            }

            bool socializing = IsSocializing(actor);
            Sprite socialBubbleSprite = null;
            Sprite socialTopicSprite = null;
            if (socializing)
            {
                CommunicationAsset communication = CommunicationLibrary.normal;
                socialBubbleSprite = communication?.getSpriteBubble();
                if (communication?.show_topic == true)
                {
                    socialTopicSprite = actor.getSocializeTopic();
                }

                flags |= ActorPresentationFlags.Socializing;
                if (actor.hasTrait("mute"))
                {
                    flags |= ActorPresentationFlags.Muted;
                }
            }

            Sprite happinessSprite = null;
            if (actor.hasEmotions() && !actor.isInsideSomething())
            {
                happinessSprite =
                    HappinessHelper.getSpriteBasedOnHappinessValue(
                        actor.getHappiness());
                flags |= ActorPresentationFlags.HasHappinessIcon;
            }

            Sprite taskSprite = null;
            ai.behaviours.BehaviourTaskActor task = actor.ai?.task;
            if (!actor.isInsideSomething() &&
                asset.show_task_icon &&
                task?.show_icon == true)
            {
                taskSprite = task.getSprite();
                flags |= ActorPresentationFlags.HasTaskIcon;
            }

            if (checkUnexplored &&
                QuantumSpriteLibrary.checkShouldDrawUnexploredSpriteFor(actor))
            {
                flags |= ActorPresentationFlags.UnexploredAugmentation;
            }

            MetaType metaType = MetaType.None;
            long metaId = 0L;
            Color metaColor = default;
            bool metaFavorite = false;
            if (!requestedMetaType.isNone() &&
                actor.getMetaObjectOfType(requestedMetaType) is IMetaObject metaObject)
            {
                ColorAsset color = metaObject.getColor();
                if (color != null)
                {
                    metaType = requestedMetaType;
                    metaId = metaObject.getID();
                    metaColor = color.getColorText();
                    metaFavorite = metaObject.isFavorite();
                }
            }

            int lightStart = lightCount;
            CaptureLights(actor);
            int actorLightCount = lightCount - lightStart;
            int statusStart = statusCount;
            CaptureStatuses(actor);
            int actorStatusCount = statusCount - statusStart;
            Kingdom kingdom = actor.kingdom;
            samples[capturedCount] = new ActorPresentationSample
            {
                Handle = new ActorPresentationHandle(worldGeneration, actorId),
                ActorReference = actor,
                Position = actor.current_position,
                NextStepPosition = actor.next_step_position,
                ShakeOffset = actor.shake_offset,
                JumpOffset = actor.move_jump_offset,
                Scale = actor.current_scale,
                Rotation = actor.target_angle,
                Color = actor.color,
                Flip = actor.flip,
                PositionHeight = actor.position_height,
                MovementSpeed = actor._current_combined_movement_speed,
                ZoneId = actor.current_tile?.zone?.id ?? -1,
                MainSprite = mainSprite,
                ItemSprite = itemSprite,
                ShadowSprite = shadowSprite,
                ItemOffset = frameData?.pos_item ?? default,
                ShadowSize = shadowSize,
                FrameUnitSize = frameData?.size_unit ?? default,
                HeadOffset = frameData == null
                    ? default
                    : Vector2.Scale(frameData.pos_head, actor.current_scale),
                AvatarTransform = actor.avatar?.transform,
                HealthRatio = actor.getHealthRatio(),
                ScaleMod = actor.getScaleMod(),
                VisualScale = visualScale,
                FlyingVehicleSprite = flyingVehicleSprite,
                FlyingVehicleVertical = flyingVehicleVertical,
                FlyingScaleReferenceSprite =
                    flyingScaleReferenceSprite,
                BannerColor = kingdom == null
                    ? Color.white
                    : kingdom.getColor().getColorText(),
                JustAteAt = justAteAt,
                JustAteSprite = justAteSprite,
                SocialStartedAt = actor.timestamp_tween_session_social,
                SocialBubbleSprite = socialBubbleSprite,
                SocialTopicSprite = socialTopicSprite,
                HappinessSprite = happinessSprite,
                TaskSprite = taskSprite,
                MetaType = metaType,
                MetaId = metaId,
                MetaColor = metaColor,
                MetaFavorite = metaFavorite,
                LightStart = lightStart,
                LightCount = actorLightCount,
                StatusStart = statusStart,
                StatusCount = actorStatusCount,
                Flags = flags
            };
            indexes[actorId] = capturedCount;
            capturedCount++;
        }

        CaptureBuildings(world);
        CaptureProjectiles(world);
        CaptureResourceThrows(world);
        CaptureWorldLights(world);
        WorldGeneration = worldGeneration;
        TickSequence = tickSequence;
        SimulationTimeValue = SimulationTime.DiagnosticTime;
        CapturedAt = Stopwatch.GetTimestamp();
        Count = capturedCount;
    }

    internal ref readonly ActorStatusPresentationSample GetStatusAt(int index)
    {
        if ((uint)index >= (uint)statusCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref statuses[index];
    }

    internal ref readonly ActorStatusFramePresentationSample GetStatusFrameAt(int index)
    {
        if ((uint)index >= (uint)statusFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref statusFrames[index];
    }

    internal ref readonly ActorLightPresentationSample GetLightAt(int index)
    {
        if ((uint)index >= (uint)lightCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref lights[index];
    }

    internal ref readonly BuildingPresentationSample GetBuildingAt(int index)
    {
        if ((uint)index >= (uint)buildingCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref buildings[index];
    }

    internal ref readonly StockpileResourcePresentationSample
        GetStockpileResourceAt(int index)
    {
        if ((uint)index >= (uint)stockpileResourceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref stockpileResources[index];
    }

    internal ref readonly BuildingLightPresentationSample
        GetBuildingLightAt(int index)
    {
        if ((uint)index >= (uint)buildingLightCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref buildingLights[index];
    }

    internal ref readonly WorldLightPresentationSample GetWorldLightAt(
        int index)
    {
        if ((uint)index >= (uint)worldLightCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref worldLights[index];
    }

    internal ref readonly FirePresentationSample GetFireAt(int index)
    {
        if ((uint)index >= (uint)fireCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref fires[index];
    }

    internal ref readonly ProjectilePresentationSample GetProjectileAt(int index)
    {
        if ((uint)index >= (uint)projectileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref projectiles[index];
    }

    internal ref readonly ResourceThrowPresentationSample GetResourceThrowAt(
        int index)
    {
        if ((uint)index >= (uint)resourceThrowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref resourceThrows[index];
    }

    internal bool TryGet(long actorId, out ActorPresentationSample sample)
    {
        if (TryGetIndex(actorId, out int index))
        {
            sample = samples[index];
            return true;
        }

        sample = default;
        return false;
    }

    internal bool TryGetIndex(long actorId, out int index)
    {
        return indexes.TryGetValue(actorId, out index) &&
               (uint)index < (uint)Count;
    }

    internal ref readonly ActorPresentationSample GetAt(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref samples[index];
    }

    internal void Reset()
    {
        indexes.Clear();
        WorldGeneration = 0;
        TickSequence = 0;
        SimulationTimeValue = 0.0;
        CapturedAt = 0L;
        Count = 0;
        statusCount = 0;
        statusFrameCount = 0;
        lightCount = 0;
        buildingCount = 0;
        stockpileResourceCount = 0;
        buildingLightCount = 0;
        worldLightCount = 0;
        fireCount = 0;
        projectileCount = 0;
        resourceThrowCount = 0;
    }

    private void EnsureCapacity(int capacity)
    {
        if (samples.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(4096, samples.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref samples, nextCapacity);
    }

    private void CaptureStatuses(Actor actor)
    {
        ActorAsset actorAsset = actor.asset;
        if (!actorAsset.render_status_effects ||
            !actor.hasAnyStatusEffectToRender())
        {
            return;
        }

        foreach (Status status in actor.getStatuses())
        {
            StatusAsset asset = status.asset;
            if (!asset.need_visual_render ||
                !asset.render_check(actorAsset))
            {
                continue;
            }

            int frameCount = status.get_sprites_count;
            if (frameCount <= 0)
            {
                continue;
            }

            EnsureStatusCapacity(statusCount + 1);
            EnsureStatusFrameCapacity(statusFrameCount + frameCount);
            int frameStart = statusFrameCount;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                statusFrames[statusFrameCount++] =
                    new ActorStatusFramePresentationSample
                    {
                        Sprite = asset.has_override_sprite
                            ? asset.get_override_sprite(actor, frameIndex)
                            : asset.sprite_list[frameIndex],
                        Offset = asset.has_override_sprite_position
                            ? asset.get_override_sprite_position(actor, frameIndex)
                            : default,
                        RotationZ = asset.has_override_sprite_rotation_z
                            ? asset.get_override_sprite_rotation_z(actor, frameIndex)
                            : asset.rotation_z
                    };
            }

            float frameInterval = status._anim_time_between_frames;
            if (frameInterval <= 0f)
            {
                frameInterval = Math.Max(0.0001f, asset.animation_speed);
            }

            statuses[statusCount++] = new ActorStatusPresentationSample
            {
                FrameStart = frameStart,
                FrameCount = frameCount,
                CapturedFrame = Mathf.Clamp(status.anim_frame, 0, frameCount - 1),
                TimeUntilNextFrame = Math.Max(0f, status._anim_timer),
                FrameInterval = frameInterval,
                Scale = actor.current_scale.y * asset.scale,
                BaseOffset = new Vector2(
                    asset.offset_x * actor.getScaleMod(),
                    asset.offset_y * actor.getScaleMod()),
                PositionZ = asset.position_z,
                Material = asset.material,
                Animated = asset.animated && asset.texture != null,
                AnimateWhenPaused = asset.is_animated_in_pause,
                Loop = asset.loop,
                UseParentRotation = asset.use_parent_rotation,
                Flip = !asset.use_parent_rotation && asset.can_be_flipped && actor.flip,
                HasRotation = asset.rotation_z != 0f
            };
        }
    }

    private void CaptureLights(Actor actor)
    {
        if (actor.a.has_tag_generate_light)
        {
            AddLight(new Vector2(0f, actor.getHeight()), 0.3f);
            return;
        }

        if (!actor.hasAnyStatusEffect())
        {
            return;
        }

        foreach (Status status in actor.getStatuses())
        {
            StatusAsset asset = status.asset;
            if (asset.draw_light_area)
            {
                AddLight(default, asset.draw_light_size);
            }
        }
    }

    private void CaptureBuildings(MapBox world)
    {
        BuildingManager manager = world.buildings;
        manager.checkContainer();
        manager.prepareArray();
        Building[] source = manager.getSimpleArray();
        int count = manager.Count;
        EnsureBuildingCapacity(count);
        bool captureShadows =
            world.quality_changer.shouldRenderBuildingShadows();
        for (int i = 0; i < count; i++)
        {
            Building building = source[i];
            if (building?.data == null ||
                !building.exists ||
                !building.isAlive())
            {
                continue;
            }

            BuildingAsset asset = building.asset;
            Sprite mainSprite = building.calculateMainSprite();
            Sprite coloredSprite =
                building.isColoredSpriteNeedsCheck(mainSprite)
                    ? building.calculateColoredSprite(mainSprite)
                    : building.getLastColoredSprite();
            bool hasShadow =
                captureShadows &&
                asset.shadow &&
                !building.chopped;
            bool usable = building.isUsable();
            bool underConstruction =
                building.isUnderConstruction();
            int stockpileResourceStart = stockpileResourceCount;
            bool stockpileVisible =
                asset.is_stockpile &&
                building.is_visible &&
                usable &&
                !underConstruction &&
                building.resources != null;
            if (stockpileVisible)
            {
                CaptureStockpileResources(building);
            }

            bool usableForLights =
                usable &&
                !building.isAbandoned() &&
                (!asset.hasHousingSlots() || building.hasResidents());
            bool lightWindowVisible =
                asset.city_building &&
                usableForLights;
            Sprite lightWindowSprite = lightWindowVisible
                ? DynamicSprites.getBuildingLight(building)
                : null;
            int buildingLightStart = buildingLightCount;
            CaptureBuildingLights(
                building,
                asset,
                usableForLights);
            int buildingStatusStart = statusCount;
            CaptureBuildingStatuses(building);
            buildings[buildingCount++] = new BuildingPresentationSample
            {
                BuildingId = building.getID(),
                BuildingReference = building,
                ZoneId = building.current_tile?.zone?.id ?? -1,
                Position = building.cur_transform_position,
                Scale = building.getCurrentScale(),
                Rotation = building.current_rotation,
                MainSprite = mainSprite,
                ColoredSprite = coloredSprite,
                Material = building.material,
                Color = building.kingdom?.asset?.color_building ??
                        Color.white,
                Flip = building.flip_x,
                HasShadow = hasShadow,
                ShadowSprite = hasShadow
                    ? DynamicSprites.getShadowBuilding(asset, mainSprite)
                    : null,
                Usable = usable,
                UnderConstruction = underConstruction,
                Stockpile = asset.is_stockpile,
                StockpileVisible = stockpileVisible,
                StockpileOffset = asset.stockpile_top_left_offset,
                StockpileColor = building.hasCity()
                    ? Toolbox.color_white
                    : Toolbox.color_abandoned_building,
                StockpileResourceStart = stockpileResourceStart,
                StockpileResourceCount =
                    stockpileResourceCount - stockpileResourceStart,
                LightWindowSprite = lightWindowSprite,
                LightWindowVisible =
                    lightWindowVisible && lightWindowSprite != null,
                LightStart = buildingLightStart,
                LightCount = buildingLightCount - buildingLightStart,
                StatusStart = buildingStatusStart,
                StatusCount = statusCount - buildingStatusStart,
                Sparkle = asset.sparkle_effect
            };
        }
    }

    private void CaptureBuildingStatuses(Building building)
    {
        if (!building.hasAnyStatusEffectToRender())
        {
            return;
        }

        foreach (Status status in building.getStatuses())
        {
            StatusAsset asset = status.asset;
            if (!asset.need_visual_render)
            {
                continue;
            }

            int frameCount = status.get_sprites_count;
            if (frameCount <= 0)
            {
                continue;
            }

            EnsureStatusCapacity(statusCount + 1);
            EnsureStatusFrameCapacity(statusFrameCount + frameCount);
            int frameStart = statusFrameCount;
            for (int frameIndex = 0;
                 frameIndex < frameCount;
                 frameIndex++)
            {
                statusFrames[statusFrameCount++] =
                    new ActorStatusFramePresentationSample
                    {
                        Sprite = asset.has_override_sprite
                            ? asset.get_override_sprite(
                                building,
                                frameIndex)
                            : asset.sprite_list[frameIndex],
                        Offset = asset.has_override_sprite_position
                            ? asset.get_override_sprite_position(
                                building,
                                frameIndex)
                            : default,
                        RotationZ =
                            asset.has_override_sprite_rotation_z
                                ? asset.get_override_sprite_rotation_z(
                                    building,
                                    frameIndex)
                                : asset.rotation_z
                    };
            }

            float frameInterval = status._anim_time_between_frames;
            if (frameInterval <= 0f)
            {
                frameInterval = Math.Max(
                    0.0001f,
                    asset.animation_speed);
            }

            statuses[statusCount++] =
                new ActorStatusPresentationSample
                {
                    FrameStart = frameStart,
                    FrameCount = frameCount,
                    CapturedFrame = Mathf.Clamp(
                        status.anim_frame,
                        0,
                        frameCount - 1),
                    TimeUntilNextFrame =
                        Math.Max(0f, status._anim_timer),
                    FrameInterval = frameInterval,
                    Scale =
                        building.current_scale.y * asset.scale,
                    BaseOffset = default,
                    PositionZ = asset.position_z,
                    Material = asset.material,
                    Animated =
                        asset.animated && asset.texture != null,
                    AnimateWhenPaused =
                        asset.is_animated_in_pause,
                    Loop = asset.loop,
                    UseParentRotation =
                        asset.use_parent_rotation,
                    Flip = false,
                    HasRotation = asset.rotation_z != 0f
                };
        }
    }

    private void CaptureStockpileResources(Building building)
    {
        foreach (CityStorageSlot slot in building.resources.getSlots())
        {
            if (slot.amount == 0)
            {
                continue;
            }

            ResourceAsset resource = slot.asset;
            Sprite sprite = resource?.getGameplaySprite();
            if (sprite == null)
            {
                continue;
            }

            EnsureStockpileResourceCapacity(stockpileResourceCount + 1);
            stockpileResources[stockpileResourceCount++] =
                new StockpileResourcePresentationSample
                {
                    Sprite = sprite,
                    IconCount =
                        slot.amount / Math.Max(1, resource.stack_size) + 1
                };
        }
    }

    private void CaptureBuildingLights(
        Building building,
        BuildingAsset asset,
        bool usableForLights)
    {
        if (building.hasAnyStatusEffect())
        {
            foreach (Status status in building.getStatuses())
            {
                StatusAsset statusAsset = status.asset;
                if (statusAsset.draw_light_area)
                {
                    AddBuildingLight(
                        building.current_position,
                        statusAsset.draw_light_size);
                }
            }
        }

        if (!asset.draw_light_area || !usableForLights)
        {
            return;
        }

        Vector2 position = building.current_position;
        position.x += asset.draw_light_area_offset_x;
        position.y += asset.draw_light_area_offset_y;
        AddBuildingLight(position, asset.draw_light_size);
    }

    private void AddBuildingLight(Vector2 position, float scale)
    {
        EnsureBuildingLightCapacity(buildingLightCount + 1);
        buildingLights[buildingLightCount++] =
            new BuildingLightPresentationSample
            {
                Position = position,
                Scale = scale
            };
    }

    private void CaptureWorldLights(MapBox world)
    {
        List<LightBlobData> blobs = world.stack_effects.light_blobs;
        EnsureWorldLightCapacity(blobs.Count);
        for (int i = 0; i < blobs.Count; i++)
        {
            LightBlobData blob = blobs[i];
            AddWorldLight(
                blob.position,
                blob.radius,
                useEraColor: false);
        }

        if (!MapBox.isRenderGameplay() ||
            !WorldBehaviourActionFire.hasFires())
        {
            return;
        }

        List<TileZone> visibleZones =
            world.zone_camera.getVisibleZones();
        for (int zoneIndex = 0;
             zoneIndex < visibleZones.Count;
             zoneIndex++)
        {
            TileZone zone = visibleZones[zoneIndex];
            if (!WorldBehaviourActionFire.hasFires(zone))
            {
                continue;
            }

            WorldTile[] tiles = zone.tiles;
            for (int tileIndex = 0;
                 tileIndex < tiles.Length;
                 tileIndex++)
            {
                WorldTile tile = tiles[tileIndex];
                if (tile.isOnFire())
                {
                    int tileId = tile.tile_id;
                    AddFire(
                        world.tile_manager.positions_vector3[tileId],
                        world.tile_manager.fire_animation_set[tileId],
                        world.tile_manager.random_seeds[tileId]);
                    AddWorldLight(
                        tile.pos,
                        0.2f,
                        useEraColor: true);
                }
            }
        }
    }

    private void AddFire(
        Vector3 position,
        int animationSet,
        int randomSeed)
    {
        EnsureFireCapacity(fireCount + 1);
        fires[fireCount++] = new FirePresentationSample
        {
            Position = position,
            AnimationSet = animationSet,
            RandomSeed = randomSeed
        };
    }

    private void AddWorldLight(
        Vector2 position,
        float scale,
        bool useEraColor)
    {
        EnsureWorldLightCapacity(worldLightCount + 1);
        worldLights[worldLightCount++] =
            new WorldLightPresentationSample
            {
                Position = position,
                Scale = scale,
                UseEraColor = useEraColor
            };
    }

    private void CaptureProjectiles(MapBox world)
    {
        ProjectileManager manager = world.projectiles;
        manager.checkLists();
        List<Projectile> source = manager.list;
        EnsureProjectileCapacity(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            Projectile projectile = source[i];
            ProjectileAsset asset = projectile?.asset;
            if (projectile == null ||
                asset == null ||
                !projectile.exists ||
                projectile.isFinished())
            {
                continue;
            }

            Vector3 position = projectile.getTransformedPositionWithHeight();
            position.z = projectile.getCurrentHeight();
            Sprite shadowSprite = string.IsNullOrEmpty(asset.texture_shadow)
                ? null
                : SpriteTextureLoader.getSprite(asset.texture_shadow);
            projectiles[projectileCount++] =
                new ProjectilePresentationSample
                {
                    ProjectileId = projectile.getID(),
                    RenderSeed = projectile.GetHashCode(),
                    Position = position,
                    ShadowPosition = projectile.getCurrentPosition(),
                    Velocity = projectile._velocity,
                    Rotation = projectile.rotation,
                    Height = projectile.getCurrentHeight(),
                    Scale = projectile.getCurrentScale(),
                    TargetScale = projectile._target_scale,
                    Alpha = projectile.getAlpha(),
                    ShadowAngle = projectile.getAngleForShadow(),
                    Frames = asset.frames,
                    AnimationSpeed = asset.animation_speed,
                    Animated = asset.animated,
                    DeadAnimation = projectile.isDeadAnimation(),
                    ShadowSprite = shadowSprite
                };
        }
    }

    private void CaptureResourceThrows(MapBox world)
    {
        List<ResourceThrowData> source =
            world.resource_throw_manager.getList();
        EnsureResourceThrowCapacity(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            ResourceThrowData item = source[i];
            ResourceAsset resource =
                AssetManager.resources.get(item.resource_asset_id);
            resourceThrows[resourceThrowCount++] =
                new ResourceThrowPresentationSample
                {
                    Start = item.position_start,
                    End = item.position_end,
                    StartTime = item.start_time,
                    EndTime = item.end_time,
                    Height = item.height,
                    Sprite = resource?.getGameplaySprite()
                };
        }
    }

    private void AddLight(Vector2 offset, float scale)
    {
        EnsureLightCapacity(lightCount + 1);
        lights[lightCount++] = new ActorLightPresentationSample
        {
            Offset = offset,
            Scale = scale
        };
    }

    private void EnsureStatusCapacity(int capacity)
    {
        if (statuses.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, statuses.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref statuses, nextCapacity);
    }

    private void EnsureStatusFrameCapacity(int capacity)
    {
        if (statusFrames.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(1024, statusFrames.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref statusFrames, nextCapacity);
    }

    private void EnsureLightCapacity(int capacity)
    {
        if (lights.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, lights.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref lights, nextCapacity);
    }

    private void EnsureBuildingCapacity(int capacity)
    {
        if (buildings.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(2048, buildings.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref buildings, nextCapacity);
    }

    private void EnsureStockpileResourceCapacity(int capacity)
    {
        if (stockpileResources.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, stockpileResources.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref stockpileResources, nextCapacity);
    }

    private void EnsureBuildingLightCapacity(int capacity)
    {
        if (buildingLights.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, buildingLights.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref buildingLights, nextCapacity);
    }

    private void EnsureWorldLightCapacity(int capacity)
    {
        if (worldLights.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, worldLights.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref worldLights, nextCapacity);
    }

    private void EnsureFireCapacity(int capacity)
    {
        if (fires.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, fires.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref fires, nextCapacity);
    }

    private void EnsureProjectileCapacity(int capacity)
    {
        if (projectiles.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, projectiles.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref projectiles, nextCapacity);
    }

    private void EnsureResourceThrowCapacity(int capacity)
    {
        if (resourceThrows.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(256, resourceThrows.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref resourceThrows, nextCapacity);
    }

    private static MetaType GetRequestedMetaType()
    {
        if (SelectedObjects.isNanoObjectSet())
        {
            NanoObject selected = SelectedObjects.getSelectedNanoObject();
            return selected?.getMetaType() ?? MetaType.None;
        }

        return PlayerConfig.optionBoolEnabled("unit_metas")
            ? Zones.getCurrentMapBorderMode()
            : MetaType.None;
    }

    private static bool IsSocializing(Actor actor)
    {
        ai.behaviours.BehaviourActionActor action = actor.ai.action;
        if (action?.socialize == true)
        {
            return true;
        }

        return actor.is_forced_socialize_icon &&
               !actor.is_moving &&
               !actor.isLying() &&
               actor.isAttackReady() &&
               Date.getMonthsSince(actor.is_forced_socialize_timestamp) < 1;
    }
}

/// <summary>
/// 角色表现快照的单 writer、单 reader 三缓冲交换器。
/// render、ready 与 writer 槽位在任意时刻互不重叠。
/// </summary>
internal static class ActorPresentationSnapshots
{
    private const int SlotCount = 3;

    private static readonly object gate = new();
    private static readonly ActorPresentationSnapshot[] slots =
    {
        new(),
        new(),
        new()
    };
    private static readonly Stack<int> freeSlots = new(SlotCount);

    private static int writerIndex;
    private static int readyIndex = -1;
    private static int renderIndex = -1;
    private static int requestedGeneration;
    private static int capturedRequestGeneration;
    private static long completedCaptures;
    private static long acquiredCaptures;
    private static long supersededCaptures;
    private static long capturedActors;
    private static long totalCaptureTicks;
    private static long maximumCaptureTicks;
    private static long lastCaptureTicks;

    static ActorPresentationSnapshots()
    {
        ResetSlotOwnership();
    }

    internal static ActorPresentationSnapshot Current
    {
        get
        {
            lock (gate)
            {
                return renderIndex >= 0 ? slots[renderIndex] : null;
            }
        }
    }

    internal static bool HasPublishedSnapshot
    {
        get
        {
            lock (gate)
            {
                int index = readyIndex >= 0
                    ? readyIndex
                    : renderIndex;
                return index >= 0 &&
                       slots[index].WorldGeneration ==
                       SimulationTime.Generation;
            }
        }
    }

    internal static void RequestCapture()
    {
        Interlocked.Increment(ref requestedGeneration);
    }

    internal static bool CaptureIfRequested(MapBox world, long tickSequence)
    {
        int requestGeneration = Volatile.Read(ref requestedGeneration);
        if (requestGeneration == Volatile.Read(ref capturedRequestGeneration))
        {
            return false;
        }

        ActorPresentationSnapshot writer;
        lock (gate)
        {
            writer = slots[writerIndex];
        }

        long startedAt = Stopwatch.GetTimestamp();
        writer.Capture(world, tickSequence);
        RecordCaptureDuration(Stopwatch.GetTimestamp() - startedAt);
        PublishWriter(requestGeneration, writer.Count);
        return true;
    }

    internal static ActorPresentationSnapshot AcquireLatest()
    {
        lock (gate)
        {
            if (readyIndex >= 0)
            {
                int previousRender = renderIndex;
                renderIndex = readyIndex;
                readyIndex = -1;
                if (previousRender >= 0)
                {
                    freeSlots.Push(previousRender);
                }

                Interlocked.Increment(ref acquiredCaptures);
            }

            return renderIndex >= 0 ? slots[renderIndex] : null;
        }
    }

    internal static bool TryGetCurrent(long actorId, out ActorPresentationSample sample)
    {
        ActorPresentationSnapshot snapshot = Current;
        if (snapshot != null &&
            snapshot.WorldGeneration == SimulationTime.Generation)
        {
            return snapshot.TryGet(actorId, out sample);
        }

        sample = default;
        return false;
    }

    internal static void Reset()
    {
        lock (gate)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Reset();
            }

            ResetSlotOwnership();
            Volatile.Write(ref capturedRequestGeneration, 0);
            Volatile.Write(ref requestedGeneration, 0);
        }
    }

    internal static string GetDiagnostics()
    {
        ActorPresentationSnapshot current = Current;
        return string.Format(
            CultureInfo.InvariantCulture,
            "requested={0} captured={1} acquired={2} superseded={3} " +
            "actors={4} current_tick={5} current_count={6} " +
            "statuses={7}/{8} lights={12}+{18} buildings={13} " +
            "stockpile_resources={16} building_lights={17} " +
            "projectiles={14} throws={15} fires={19} " +
            "capture={9:0.00}ms(avg={10:0.00},max={11:0.00})",
            Volatile.Read(ref requestedGeneration),
            Interlocked.Read(ref completedCaptures),
            Interlocked.Read(ref acquiredCaptures),
            Interlocked.Read(ref supersededCaptures),
            Interlocked.Read(ref capturedActors),
            current?.TickSequence ?? 0L,
            current?.Count ?? 0,
            current?.StatusCount ?? 0,
            current?.StatusFrameCount ?? 0,
            TicksToMilliseconds(Interlocked.Read(ref lastCaptureTicks)),
            TicksToMilliseconds(Interlocked.Read(ref totalCaptureTicks)) /
            Math.Max(1L, Interlocked.Read(ref completedCaptures)),
            TicksToMilliseconds(Interlocked.Read(ref maximumCaptureTicks)),
            current?.LightCount ?? 0,
            current?.BuildingCount ?? 0,
            current?.ProjectileCount ?? 0,
            current?.ResourceThrowCount ?? 0,
            current?.StockpileResourceCount ?? 0,
            current?.BuildingLightCount ?? 0,
            current?.WorldLightCount ?? 0,
            current?.FireCount ?? 0);
    }

    private static void PublishWriter(int requestGeneration, int actorCount)
    {
        lock (gate)
        {
            int completedWriter = writerIndex;
            if (readyIndex >= 0)
            {
                writerIndex = readyIndex;
                readyIndex = completedWriter;
                Interlocked.Increment(ref supersededCaptures);
            }
            else
            {
                if (freeSlots.Count == 0)
                {
                    throw new InvalidOperationException("角色表现快照三缓冲所有权损坏");
                }

                writerIndex = freeSlots.Pop();
                readyIndex = completedWriter;
            }

            Volatile.Write(ref capturedRequestGeneration, requestGeneration);
            Interlocked.Increment(ref completedCaptures);
            Interlocked.Add(ref capturedActors, actorCount);
        }
    }

    private static void ResetSlotOwnership()
    {
        freeSlots.Clear();
        writerIndex = 0;
        readyIndex = -1;
        renderIndex = -1;
        freeSlots.Push(2);
        freeSlots.Push(1);
    }

    private static void RecordCaptureDuration(long elapsedTicks)
    {
        Interlocked.Exchange(ref lastCaptureTicks, elapsedTicks);
        Interlocked.Add(ref totalCaptureTicks, elapsedTicks);
        long maximum = Interlocked.Read(ref maximumCaptureTicks);
        while (elapsedTicks > maximum)
        {
            long previous = Interlocked.CompareExchange(
                ref maximumCaptureTicks,
                elapsedTicks,
                maximum);
            if (previous == maximum)
            {
                break;
            }

            maximum = previous;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}
