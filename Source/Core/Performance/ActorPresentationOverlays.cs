using System;
using System.Diagnostics;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 只读取已发布角色快照的原版叠加表现。
/// </summary>
internal static class ActorPresentationOverlays
{
    private static float metaFallOffsetTimer;
    private static MetaType lastMetaType = MetaType.None;

    internal static bool TryDrawAvatars()
    {
        ActorPresentationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample sample = ref snapshot.GetAt(i);
            if (!sample.HasFlag(ActorPresentationFlags.HasAvatar) ||
                !sample.HasFlag(ActorPresentationFlags.NormalRender))
            {
                continue;
            }

            Transform avatar = sample.AvatarTransform;
            if (avatar == null ||
                !ActorPresentationRenderer.TryGetPresentationState(
                    sample.Handle.ActorId,
                    out _,
                    out Vector3 position,
                    out bool visible,
                    out _))
            {
                continue;
            }

            if (!visible)
            {
                avatar.position = Globals.POINT_IN_VOID;
                continue;
            }

            avatar.position = position;
            avatar.localScale = sample.Scale;
            avatar.eulerAngles = sample.Rotation;
        }

        return true;
    }

    internal static bool TryDrawHealthbars(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        bool selectedOnly = SelectedUnit.isSet();
        bool showAll = HotkeyLibrary.isHoldingAlt();
        if (!showAll && !selectedOnly)
        {
            return true;
        }

        if (showAll)
        {
            selectedOnly = false;
        }

        if (Zones.getCurrentMapBorderMode().isNone())
        {
            return true;
        }

        ref Color background = ref ColorStyleLibrary.m.health_bar_background;
        ref Color green = ref ColorStyleLibrary.m.health_bar_main_green;
        ref Color red = ref ColorStyleLibrary.m.health_bar_main_red;
        float zoom = QuantumSpriteLibrary.getCameraScaleZoomMultiplier(asset) * 1.6f;
        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Alive) ||
                selectedOnly &&
                !ActorPresentationRenderer.IsSelected(sample.Handle.ActorId))
            {
                continue;
            }

            float healthRatio = sample.HealthRatio;
            if (healthRatio >= 1f)
            {
                continue;
            }

            float width = 0.9f * zoom;
            float height = 0.15f * zoom;
            Vector3 position = ActorPresentationRenderer.GetVisiblePosition(i);
            position.x -= width * 0.5f;
            position.y += 1.3f;

            QuantumSprite barBackground =
                QuantumSpriteLibrary.drawQuantumSprite(asset, position);
            barBackground.setSprite(QuantumSpriteLibrary._sprite_pixel);
            barBackground.transform.localScale = new Vector3(width, height);
            barBackground.setColor(ref background);

            position.z += 0.01f;
            QuantumSprite bar =
                QuantumSpriteLibrary.drawQuantumSprite(asset, position);
            bar.setSprite(QuantumSpriteLibrary._sprite_pixel);
            bar.transform.localScale = new Vector3(width * healthRatio, height);
            if (healthRatio < 0.4f)
            {
                bar.setColor(ref red);
            }
            else
            {
                bar.setColor(ref green);
            }
        }

        return true;
    }

    internal static bool TryDrawHappinessIcons(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("icons_happiness"))
        {
            return true;
        }

        float offset = 18f;
        if (PlayerConfig.optionBoolEnabled("icons_tasks"))
        {
            offset += 11f;
        }

        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Alive) ||
                !sample.HasFlag(ActorPresentationFlags.HasHappinessIcon) ||
                sample.HappinessSprite == null)
            {
                continue;
            }

            Vector3 position = ActorPresentationRenderer.GetVisiblePosition(i);
            position.z = offset;
            position.y += offset * sample.Scale.y;
            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale.y * 0.5f);
            sprite.setSprite(sample.HappinessSprite);
        }

        return true;
    }

    internal static bool TryDrawTaskIcons(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("icons_tasks"))
        {
            return true;
        }

        const float Offset = 17.5f;
        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Alive) ||
                !sample.HasFlag(ActorPresentationFlags.HasTaskIcon) ||
                sample.TaskSprite == null)
            {
                continue;
            }

            Vector3 position = ActorPresentationRenderer.GetVisiblePosition(i);
            position.z = Offset;
            position.y += Offset * sample.Scale.y;
            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale.y * 0.5f);
            sprite.setSprite(sample.TaskSprite);
        }

        return true;
    }

    internal static bool TryDrawUnitMetas(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        bool selectedMetaSet = ActorPresentationRenderer.HasSelectedMeta;
        bool enabled = PlayerConfig.optionBoolEnabled("unit_metas") ||
                       selectedMetaSet;
        if (!enabled)
        {
            lastMetaType = MetaType.None;
            return true;
        }

        MetaType metaType = selectedMetaSet
            ? ActorPresentationRenderer.SelectedMetaType
            : Zones.getCurrentMapBorderMode();
        if (metaType.isNone())
        {
            return true;
        }

        if (lastMetaType != metaType)
        {
            metaFallOffsetTimer = 0f;
        }

        lastMetaType = metaType;
        metaFallOffsetTimer = Mathf.Min(
            1f,
            metaFallOffsetTimer + Time.unscaledDeltaTime);
        float fallOffset =
            (1f - EaseOutBounce01(metaFallOffsetTimer)) * 5f;
        bool favoritesOnly =
            PlayerConfig.optionBoolEnabled("only_favorited_meta");
        long selectedMetaId = ActorPresentationRenderer.SelectedMetaId;
        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Alive) ||
                sample.MetaType != metaType ||
                selectedMetaSet && sample.MetaId != selectedMetaId ||
                favoritesOnly && !sample.MetaFavorite)
            {
                continue;
            }

            Vector3 position = ActorPresentationRenderer.GetVisiblePosition(i);
            position.y += fallOffset;
            position.z = -0.02f;
            QuantumSprite sprite = asset.group_system.getNext();
            sprite.setPosOnly(ref position);
            Vector3 scale = sample.Scale;
            sprite.setScale(ref scale);
            Color color = sample.MetaColor;
            sprite.setColor(ref color);
        }

        return true;
    }

    internal static bool TryDrawUnitLights(Actor actor, Color color)
    {
        ActorPresentationSnapshot snapshot = GetSnapshot();
        if (snapshot == null ||
            !ActorPresentationRenderer.TryGetActorId(actor, out long actorId) ||
            !ActorPresentationRenderer.TryGetPresentationState(
                actorId,
                out ActorPresentationSample sample,
                out Vector3 actorPosition,
                out _,
                out _))
        {
            return false;
        }

        int end = sample.LightStart + sample.LightCount;
        for (int lightIndex = sample.LightStart;
             lightIndex < end;
             lightIndex++)
        {
            ref readonly ActorLightPresentationSample light =
                ref snapshot.GetLightAt(lightIndex);
            Vector2 position = actorPosition;
            position += light.Offset;
            QuantumSpriteLibrary.showLightAt(position, color, light.Scale);
        }

        return true;
    }

    internal static bool TryDrawAllVisibleUnitLights(Color color)
    {
        ActorPresentationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (sample.LightCount == 0)
            {
                continue;
            }

            Vector3 actorPosition =
                ActorPresentationRenderer.GetVisiblePosition(i);
            int end = sample.LightStart + sample.LightCount;
            for (int lightIndex = sample.LightStart;
                 lightIndex < end;
                 lightIndex++)
            {
                ref readonly ActorLightPresentationSample light =
                    ref snapshot.GetLightAt(lightIndex);
                Vector2 position = actorPosition;
                position += light.Offset;
                QuantumSpriteLibrary.showLightAt(
                    position,
                    color,
                    light.Scale);
            }
        }

        return true;
    }

    internal static bool TryDrawUnexploredAugmentations(
        QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        if (!PowerLibrary.inspect_unit.isSelected() ||
            WorldLawLibrary.world_law_cursed_world.isEnabled())
        {
            return true;
        }

        Sprite effect = AnimationHelper.getSpriteFromListSessionTime(
            0,
            QuantumSpriteLibrary._unexplored_sprites,
            SimGlobals.m.unexplored_sprite_animation_speed);
        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Alive) ||
                !sample.HasFlag(
                    ActorPresentationFlags.UnexploredAugmentation))
            {
                continue;
            }

            Vector3 position = ActorPresentationRenderer.GetVisiblePosition(i);
            position += (Vector3)sample.HeadOffset;
            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale.y);
            sprite.setSprite(effect);
        }

        return true;
    }

    internal static bool TryDrawBanners(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Alive) ||
                !sample.HasFlag(ActorPresentationFlags.ArmyCaptain))
            {
                continue;
            }

            Vector3 actorPosition = ActorPresentationRenderer.GetVisiblePosition(i);
            Vector3 headPosition = actorPosition + (Vector3)sample.HeadOffset;
            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                headPosition,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale.y);
            Color color = sample.BannerColor;
            sprite.setColor(ref color);
            ApplyParentRotation(
                sprite,
                headPosition,
                actorPosition,
                sample.Rotation,
                -0.01f);
        }

        return true;
    }

    internal static bool TryDrawFavoritesMap(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("marks_favorites"))
        {
            return true;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample sample = ref snapshot.GetAt(i);
            long actorId = sample.Handle.ActorId;
            if (!sample.HasFlag(ActorPresentationFlags.Favorite) ||
                ActorPresentationRenderer.IsControlled(actorId) ||
                !ActorPresentationRenderer.TryGetPresentationState(
                    actorId,
                    out _,
                    out Vector3 position,
                    out _,
                    out bool zoneVisible) ||
                !zoneVisible)
            {
                continue;
            }

            position.y -= 3f;
            QuantumSpriteLibrary.drawQuantumSprite(asset, position);
        }

        return true;
    }

    internal static bool TryDrawFavoritesGame(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("marks_favorites"))
        {
            return true;
        }

        float offset = 20f;
        if (PlayerConfig.optionBoolEnabled("icons_tasks"))
        {
            offset += 11.5f;
        }

        if (PlayerConfig.optionBoolEnabled("icons_happiness"))
        {
            offset += 11.5f;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample sample = ref snapshot.GetAt(i);
            long actorId = sample.Handle.ActorId;
            if (!sample.HasFlag(ActorPresentationFlags.Favorite) ||
                sample.HasFlag(ActorPresentationFlags.InMagnet) ||
                ActorPresentationRenderer.IsControlled(actorId) ||
                !ActorPresentationRenderer.TryGetPresentationState(
                    actorId,
                    out _,
                    out Vector3 position,
                    out _,
                    out bool zoneVisible) ||
                !zoneVisible)
            {
                continue;
            }

            position.y += offset * sample.Scale.y;
            QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale.y);
        }

        return true;
    }

    internal static bool TryDrawSelectedUnits(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        if (!SelectedUnit.isSet())
        {
            return true;
        }

        Sprite selectedSprite = AnimationHelper.getSpriteFromListSessionTime(
            0,
            QuantumSpriteLibrary._unit_selection_effect,
            10f);
        Sprite mainSprite = AnimationHelper.getSpriteFromListSessionTime(
            0,
            QuantumSpriteLibrary._unit_selection_effect_main,
            10f);
        Color selectedColor = World.world.getArchitectColor();
        selectedColor.a = 0.8f;
        Color mainColor = World.world.getArchitectColor();
        long mainActorId =
            ActorPresentationRenderer.PrimarySelectedActorId;
        ActorPresentationSnapshot snapshot = GetSnapshot();
        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample captured =
                ref snapshot.GetAt(i);
            long actorId = captured.Handle.ActorId;
            if (!ActorPresentationRenderer.IsSelected(actorId) ||
                !ActorPresentationRenderer.TryGetPresented(
                    actorId,
                    out ActorPresentationSample sample,
                    out Vector3 position,
                    out _))
            {
                continue;
            }

            bool main = actorId == mainActorId;
            float scale = sample.Scale.y * (main ? 1.1f : 1f);
            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                1f,
                false,
                scale);
            sprite.setSprite(main ? mainSprite : selectedSprite);
            if (main)
            {
                sprite.setColor(ref mainColor);
            }
            else
            {
                sprite.setColor(ref selectedColor);
            }
        }

        return true;
    }

    internal static bool TryDrawSquareSelectionUnits(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        if (!World.world.player_control.square_selection_started)
        {
            return true;
        }

        Sprite effect = AnimationHelper.getSpriteFromListSessionTime(
            0,
            QuantumSpriteLibrary._unit_selection_effect,
            10f);
        Color color = World.world.getArchitectColor();
        color.a = 0.7f;
        ActorPresentationSnapshot snapshot = GetSnapshot();
        for (int i = 0; i < snapshot.Count; i++)
        {
            ref readonly ActorPresentationSample captured =
                ref snapshot.GetAt(i);
            long actorId = captured.Handle.ActorId;
            if (!ActorPresentationRenderer.IsSquareSelected(actorId) ||
                !ActorPresentationRenderer.TryGetPresented(
                    actorId,
                    out ActorPresentationSample sample,
                    out Vector3 position,
                    out _))
            {
                continue;
            }

            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale.y);
            sprite.setSprite(effect);
            sprite.setColor(ref color);
        }

        return true;
    }

    internal static bool TryDrawSocialize(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("talk_bubbles"))
        {
            return true;
        }

        double sessionTime = World.world.getCurSessionTime();
        int count = Math.Min(ActorPresentationRenderer.BaseVisibleCount, 1000);
        for (int i = 0; i < count; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.Socializing) ||
                sample.HasFlag(ActorPresentationFlags.Muted) ||
                sample.SocialBubbleSprite == null)
            {
                continue;
            }

            float elapsed = Mathf.Clamp(
                (float)(sessionTime - sample.SocialStartedAt),
                0f,
                1f);
            float tween = EaseOutCubic01(elapsed);
            Vector3 actorPosition = ActorPresentationRenderer.GetVisiblePosition(i);
            Vector2 headPosition = actorPosition + (Vector3)sample.HeadOffset;
            float noisePhase =
                Time.unscaledTime * 7f +
                sample.Handle.ActorId * 0.0137f;
            headPosition.x += Mathf.Sin(noisePhase) * 0.03f * sample.Scale.x;
            headPosition.y += Mathf.Cos(noisePhase * 1.17f) *
                              0.03f *
                              sample.Scale.y;

            Vector2 bubbleScale = sample.Scale;
            bubbleScale.y *= tween;
            QuantumSprite bubble = asset.group_system.getNext();
            bubble.set(ref headPosition, bubbleScale.y);
            bubble.setSprite(sample.SocialBubbleSprite);
            if (sample.SocialTopicSprite == null)
            {
                continue;
            }

            Vector3 topicPosition = headPosition;
            topicPosition.x -= 1.65f * sample.Scale.x;
            topicPosition.y += 10.04f * sample.Scale.y;
            topicPosition.z = headPosition.y + 3f * sample.Scale.y;
            QuantumSprite topic = asset.group_system.getNext();
            topic.set(ref topicPosition, bubbleScale.y * 0.35f);
            topic.setSprite(sample.SocialTopicSprite);
        }

        return true;
    }

    internal static bool TryDrawJustAte(QuantumSpriteAsset asset)
    {
        if (GetSnapshot() == null)
        {
            return false;
        }

        double sessionTime = World.world.getCurSessionTime();
        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample sample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (!sample.HasFlag(ActorPresentationFlags.JustAte) ||
                sample.JustAteSprite == null)
            {
                continue;
            }

            float elapsed = (float)(sessionTime - sample.JustAteAt);
            if (elapsed is < 0f or > 1f)
            {
                continue;
            }

            float tween = EaseOutCubic01(elapsed);
            Vector3 position = ActorPresentationRenderer.GetVisiblePosition(i);
            position.y += 1f + tween * 2f;
            float scale = Math.Min(tween, 0.5f);
            QuantumSprite sprite = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                position,
                null,
                null,
                null,
                null,
                scale);
            sprite.setSprite(sample.JustAteSprite);
            sprite.transform.eulerAngles = new Vector3(0f, 0f, tween * 360f);
            float alpha = elapsed > 0.6f
                ? (1f - elapsed) / 0.4f
                : 1f;
            Color color = new(alpha, alpha, alpha, alpha);
            sprite.setColor(ref color);
        }

        return true;
    }

    internal static bool TryDrawStatuses(QuantumSpriteAsset asset)
    {
        ActorPresentationSnapshot snapshot = GetSnapshot();
        if (snapshot == null)
        {
            return false;
        }

        GetStatusTiming(
            snapshot,
            out float snapshotAge,
            out float simulationRate);

        int visibleCount = ActorPresentationRenderer.BaseVisibleCount;
        for (int i = 0; i < visibleCount; i++)
        {
            ref readonly ActorPresentationSample actorSample =
                ref ActorPresentationRenderer.GetVisibleSample(i);
            if (actorSample.StatusCount == 0)
            {
                continue;
            }

            Vector3 actorPosition = ActorPresentationRenderer.GetVisiblePosition(i);
            DrawStatusRange(
                asset,
                snapshot,
                actorSample.StatusStart,
                actorSample.StatusCount,
                actorPosition,
                actorSample.Rotation,
                snapshotAge,
                simulationRate);
        }

        return true;
    }

    internal static void GetStatusTiming(
        ActorPresentationSnapshot snapshot,
        out float snapshotAge,
        out float simulationRate)
    {
        snapshotAge = (float)Math.Max(
            0.0,
            (Stopwatch.GetTimestamp() - snapshot.StatusCapturedAt) /
            (double)Stopwatch.Frequency);
        if (World.world.isPaused())
        {
            simulationRate = 0f;
            return;
        }

        simulationRate = WorldTimeRateTracker.HasActualSpeed
            ? Math.Max(0f, WorldTimeRateTracker.ActualSpeed)
            : WorldTimeRateTracker.GetRequestedSpeed();
    }

    internal static void DrawStatusRange(
        QuantumSpriteAsset asset,
        ActorPresentationSnapshot snapshot,
        int statusStart,
        int statusCount,
        Vector3 objectPosition,
        Vector3 objectRotation,
        float snapshotAge,
        float simulationRate)
    {
        int end = statusStart + statusCount;
        for (int statusIndex = statusStart;
             statusIndex < end;
             statusIndex++)
        {
            ref readonly ActorStatusPresentationSample status =
                ref snapshot.GetStatusAt(statusIndex);
            float elapsed = World.world.isPaused()
                ? status.AnimateWhenPaused ? snapshotAge : 0f
                : snapshotAge * simulationRate;
            int frameIndex = ResolveStatusFrame(in status, elapsed);
            ref readonly ActorStatusFramePresentationSample frame =
                ref snapshot.GetStatusFrameAt(
                    status.FrameStart + frameIndex);

            Vector3 position = objectPosition;
            position.x += status.BaseOffset.x + frame.Offset.x;
            position.y += status.BaseOffset.y + frame.Offset.y;
            position.z += frame.Offset.z;

            QuantumSprite sprite = asset.group_system.getNext();
            sprite.setScale(status.Scale);
            sprite.setSprite(frame.Sprite);
            if (status.UseParentRotation)
            {
                sprite.setFlipX(false);
                ApplyParentRotation(
                    sprite,
                    position,
                    objectPosition,
                    objectRotation,
                    status.PositionZ);
            }
            else
            {
                sprite.setFlipX(status.Flip);
                sprite.setPosOnly(ref position);
                Vector3 noRotation = default;
                sprite.setRotation(ref noRotation);
            }

            if (status.HasRotation)
            {
                Vector3 rotation = objectRotation;
                rotation.z += frame.RotationZ;
                sprite.setRotation(ref rotation);
            }

            sprite.setSharedMat(status.Material);
        }
    }

    private static ActorPresentationSnapshot GetSnapshot()
    {
        return PerformanceSettings.EnableFramePriorityScheduler
            ? ActorPresentationRenderer.PreparedSnapshot
            : null;
    }

    private static int ResolveStatusFrame(
        in ActorStatusPresentationSample status,
        float elapsed)
    {
        if (!status.Animated ||
            elapsed < status.TimeUntilNextFrame)
        {
            return status.CapturedFrame;
        }

        int advances = 1 + Mathf.FloorToInt(
            (elapsed - status.TimeUntilNextFrame) /
            Math.Max(0.0001f, status.FrameInterval));
        int frame = status.CapturedFrame + advances;
        if (status.Loop)
        {
            return frame % status.FrameCount;
        }

        return Math.Min(frame, status.FrameCount - 1);
    }

    private static float EaseOutCubic01(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseOutBounce01(float value)
    {
        float time = Mathf.Clamp01(value);
        if (time < 1f / 2.75f)
        {
            return 7.5625f * time * time;
        }

        if (time < 2f / 2.75f)
        {
            time -= 1.5f / 2.75f;
            return 7.5625f * time * time + 0.75f;
        }

        if (time < 2.5f / 2.75f)
        {
            time -= 2.25f / 2.75f;
            return 7.5625f * time * time + 0.9375f;
        }

        time -= 2.625f / 2.75f;
        return 7.5625f * time * time + 0.984375f;
    }

    private static void ApplyParentRotation(
        QuantumSprite sprite,
        Vector3 position,
        Vector3 pivot,
        Vector3 rotation,
        float positionZ)
    {
        position.z = positionZ;
        if (rotation.y != 0f || rotation.z != 0f)
        {
            Vector3 flatPivot = new(pivot.x, pivot.y, 0f);
            position = Toolbox.RotatePointAroundPivot(
                ref position,
                ref flatPivot,
                ref rotation);
            position.z = positionZ;
        }

        sprite.setPosOnly(ref position);
        sprite.setLocalEulerAngles(rotation);
    }
}
