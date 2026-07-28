using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 每个渲染帧在角色后台阶段启动前物化的短寿命效果。
/// </summary>
internal static class ActorTransientPresentationFrame
{
    private struct GhostSample
    {
        internal Vector3 Position;
        internal Vector3 Scale;
        internal Vector3 Rotation;
        internal Sprite Sprite;
        internal Color Color;
    }

    private struct PlotSample
    {
        internal Vector3 Position;
        internal Sprite Sprite;
        internal float CityScale;
        internal float OffsetScale;
        internal float Scale;
        internal float Progress;
    }

    private struct MagnetSample
    {
        internal Vector3 Position;
        internal float Scale;
        internal Sprite Sprite;
    }

    private static GhostSample[] damageSamples = Array.Empty<GhostSample>();
    private static GhostSample[] highlightSamples = Array.Empty<GhostSample>();
    private static PlotSample[] plotSamples = Array.Empty<PlotSample>();
    private static PlotSample[] plotRemovalSamples = Array.Empty<PlotSample>();
    private static MagnetSample[] magnetSamples = Array.Empty<MagnetSample>();
    private static int damageCount;
    private static int highlightCount;
    private static int plotCount;
    private static int plotRemovalCount;
    private static int magnetCount;
    private static bool controlledRechargeVisible;
    private static float controlledRechargeRatio;
    private static bool cursorSubspeciesVisible;
    private static Vector3 cursorSubspeciesStart;
    private static Vector3 cursorSubspeciesEnd;
    private static float magnetAngle;
    private static int preparedFrame = -1;

    internal static void Prepare()
    {
        damageCount = 0;
        highlightCount = 0;
        plotCount = 0;
        plotRemovalCount = 0;
        magnetCount = 0;
        controlledRechargeVisible = false;
        cursorSubspeciesVisible = false;
        magnetAngle = 0f;
        preparedFrame = Time.frameCount;
        ActorPresentationSnapshot snapshot =
            ActorPresentationRenderer.PreparedSnapshot;
        if (snapshot == null || World.world?.stack_effects == null)
        {
            return;
        }

        PrepareDamageEffects(
            World.world.stack_effects.actor_effect_hit);
        PrepareHighlightEffects(
            World.world.stack_effects.actor_effect_highlight);
        PreparePlots();
        PreparePlotRemovals(
            World.world.stack_effects.plot_removals);
        PrepareMagnetUnits();
        PrepareControlledRecharge();
        PrepareCursorSubspecies();
    }

    internal static bool TryDrawDamage(QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        Draw(asset, damageSamples, damageCount);
        return true;
    }

    internal static bool TryDrawHighlights(QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        Draw(asset, highlightSamples, highlightCount);
        return true;
    }

    internal static bool TryDrawPlots(QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("marks_plots"))
        {
            return true;
        }

        DrawPlots(asset, plotSamples, plotCount);
        return true;
    }

    internal static bool TryDrawPlotRemovals(QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        if (!PlayerConfig.optionBoolEnabled("marks_plots"))
        {
            return true;
        }

        DrawPlots(asset, plotRemovalSamples, plotRemovalCount);
        return true;
    }

    internal static bool TryDrawMagnetUnits(QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        for (int i = 0; i < magnetCount; i++)
        {
            ref MagnetSample sample = ref magnetSamples[i];
            QuantumSprite visual = QuantumSpriteLibrary.drawQuantumSprite(
                asset,
                sample.Position,
                null,
                null,
                null,
                null,
                1f,
                false,
                sample.Scale);
            visual.setSprite(sample.Sprite);
            visual.transform.rotation =
                Quaternion.Euler(0f, 0f, magnetAngle);
        }

        return true;
    }

    internal static bool TryDrawControlledRecharge(
        QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        if (!controlledRechargeVisible ||
            !InputHelpers.mouseSupported ||
            World.world.isBusyWithUI())
        {
            return true;
        }

        float zoom =
            QuantumSpriteLibrary.getCameraScaleZoomMultiplier(asset);
        Vector2 position = World.world.getMousePos();
        position.x += 2.5f * zoom;
        position.y -= 2.5f * zoom;
        CircleIconShaderMod component =
            QuantumSpriteLibrary.drawQuantumSprite(asset, position)
                .GetComponent<CircleIconShaderMod>();
        component.sprite_renderer_with_mat.sprite =
            QuantumSpriteLibrary._sprite_attack_reload;
        component.setShaderVal(controlledRechargeRatio);
        return true;
    }

    internal static bool TryDrawCursorSubspecies(
        QuantumSpriteAsset asset)
    {
        if (!IsPrepared())
        {
            return false;
        }

        if (cursorSubspeciesVisible)
        {
            Color color = Toolbox.color_white;
            QuantumSpriteLibrary.drawArrowQuantumSprite(
                asset,
                cursorSubspeciesStart,
                cursorSubspeciesEnd,
                ref color);
        }

        return true;
    }

    internal static void Reset()
    {
        damageCount = 0;
        highlightCount = 0;
        plotCount = 0;
        plotRemovalCount = 0;
        magnetCount = 0;
        controlledRechargeVisible = false;
        cursorSubspeciesVisible = false;
        magnetAngle = 0f;
        preparedFrame = -1;
    }

    private static void PrepareDamageEffects(
        List<ActorDamageEffectData> effects)
    {
        EnsureCapacity(ref damageSamples, effects.Count);
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            ActorDamageEffectData effect = effects[i];
            float elapsed =
                World.world.getRealTimeElapsedSince(effect.timestamp);
            if (!TryCreateGhost(
                    effect.actor,
                    elapsed,
                    out GhostSample sample))
            {
                if (elapsed > 0.3f ||
                    effect.actor == null ||
                    !effect.actor.isAlive())
                {
                    effects.RemoveAt(i);
                }

                continue;
            }

            damageSamples[damageCount++] = sample;
        }
    }

    private static void PrepareHighlightEffects(
        List<ActorHighlightEffectData> effects)
    {
        EnsureCapacity(ref highlightSamples, effects.Count);
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            ActorHighlightEffectData effect = effects[i];
            float elapsed =
                World.world.getRealTimeElapsedSince(effect.timestamp);
            if (!TryCreateGhost(
                    effect.actor,
                    elapsed,
                    out GhostSample sample))
            {
                if (elapsed > 0.3f ||
                    effect.actor == null ||
                    !effect.actor.isAlive())
                {
                    effects.RemoveAt(i);
                }

                continue;
            }

            highlightSamples[highlightCount++] = sample;
        }
    }

    private static bool TryCreateGhost(
        Actor actor,
        float elapsed,
        out GhostSample result)
    {
        if (elapsed is < 0f or > 0.3f ||
            !ActorPresentationRenderer.TryGetActorId(
                actor,
                out long actorId) ||
            !ActorPresentationRenderer.TryGetPresentationState(
                actorId,
                out ActorPresentationSample actorSample,
                out Vector3 position,
                out bool visible,
                out _) ||
            !visible ||
            !actorSample.HasFlag(ActorPresentationFlags.Alive) ||
            actorSample.MainSprite == null)
        {
            result = default;
            return false;
        }

        float alpha = 1f - elapsed / 0.3f;
        result = new GhostSample
        {
            Position = position,
            Scale = actorSample.Scale,
            Rotation = actorSample.Rotation,
            Sprite = actorSample.MainSprite,
            Color = new Color(1f, 1f, 1f, alpha)
        };
        return true;
    }

    private static void PreparePlots()
    {
        if (!PlayerConfig.optionBoolEnabled("marks_plots"))
        {
            return;
        }

        foreach (Plot plot in World.world.plots)
        {
            if (!plot.isActive())
            {
                continue;
            }

            Sprite sprite = plot.getSprite();
            float scale = plot.transition_animation;
            float progress = plot.getProgressMod();
            foreach (Actor actor in plot.units)
            {
                if (!TryCreatePlotSample(
                        actor,
                        sprite,
                        scale,
                        progress,
                        out PlotSample sample))
                {
                    continue;
                }

                EnsureCapacity(ref plotSamples, plotCount + 1);
                plotSamples[plotCount++] = sample;
            }
        }
    }

    private static void PreparePlotRemovals(
        List<PlotIconData> effects)
    {
        if (!PlayerConfig.optionBoolEnabled("marks_plots"))
        {
            return;
        }

        EnsureCapacity(ref plotRemovalSamples, effects.Count);
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            PlotIconData effect = effects[i];
            Actor actor = effect.actor;
            float elapsed =
                World.world.getRealTimeElapsedSince(effect.timestamp);
            if (elapsed > 1f ||
                actor == null ||
                !actor.isAlive())
            {
                effects.RemoveAt(i);
                continue;
            }

            float scale = Mathf.Lerp(1.3f, 0f, elapsed);
            if (TryCreatePlotSample(
                    actor,
                    SpriteTextureLoader.getSprite(effect.sprite),
                    scale,
                    1f,
                    out PlotSample sample))
            {
                plotRemovalSamples[plotRemovalCount++] = sample;
            }
        }
    }

    private static bool TryCreatePlotSample(
        Actor actor,
        Sprite sprite,
        float scale,
        float progress,
        out PlotSample result)
    {
        if (!ActorPresentationRenderer.TryGetActorId(
                actor,
                out long actorId) ||
            !ActorPresentationRenderer.TryGetPresentationState(
                actorId,
                out ActorPresentationSample actorSample,
                out Vector3 position,
                out _,
                out bool zoneVisible) ||
            !zoneVisible ||
            !actorSample.HasFlag(ActorPresentationFlags.Alive))
        {
            result = default;
            return false;
        }

        City city = actor.city;
        float cityMarkScale = city?.mark_scale_effect ?? 0.5f;
        result = new PlotSample
        {
            Position = position,
            Sprite = sprite,
            CityScale = cityMarkScale,
            OffsetScale = city == null ? 1f : cityMarkScale,
            Scale = scale,
            Progress = progress
        };
        return true;
    }

    private static void PrepareMagnetUnits()
    {
        Magnet magnet = World.world.magnet;
        if (magnet == null || !magnet.hasUnits())
        {
            return;
        }

        magnetAngle = magnet.moving_angle;
        List<Actor> actors = magnet.magnet_units;
        EnsureCapacity(ref magnetSamples, actors.Count);
        for (int i = 0; i < actors.Count; i++)
        {
            Actor actor = actors[i];
            if (!ActorPresentationRenderer.TryGetActorId(
                    actor,
                    out long actorId) ||
                !ActorPresentationRenderer.TryGetPresentationState(
                    actorId,
                    out ActorPresentationSample sample,
                    out Vector3 position,
                    out _,
                    out _) ||
                !sample.HasFlag(ActorPresentationFlags.Alive))
            {
                continue;
            }

            magnetSamples[magnetCount++] = new MagnetSample
            {
                Position = position,
                Scale = sample.Scale.y,
                Sprite = sample.MainSprite
            };
        }
    }

    private static void PrepareControlledRecharge()
    {
        if (!ControllableUnit.isControllingUnit())
        {
            return;
        }

        Actor actor = ControllableUnit.getControllableUnit();
        if (actor == null ||
            actor.asset?.id == "crabzilla" ||
            actor.isAttackReady())
        {
            return;
        }

        controlledRechargeVisible = true;
        controlledRechargeRatio = actor.getAttackCooldownRatio();
    }

    private static void PrepareCursorSubspecies()
    {
        if (!MapBox.isRenderGameplay() ||
            !InputHelpers.mouseSupported ||
            World.world.selected_buttons?.selectedButton == null ||
            World.world.isBusyWithUI() ||
            ControllableUnit.isControllingUnit() ||
            MoveCamera.inSpectatorMode() ||
            Input.GetMouseButton(0) ||
            Input.GetMouseButton(1) ||
            Input.GetMouseButton(2))
        {
            return;
        }

        WorldTile mouseTile = World.world.getMouseTilePosCachedFrame();
        GodPower power = World.world.getSelectedPowerAsset();
        if (mouseTile == null ||
            power?.type != PowerActionType.PowerSpawnActor)
        {
            return;
        }

        ActorAsset actorAsset = power.getActorAsset();
        if (actorAsset?.can_have_subspecies != true ||
            World.world.subspecies.getNearbySpecies(
                actorAsset,
                mouseTile,
                out Actor actor) == null ||
            !ActorPresentationRenderer.TryGetActorId(
                actor,
                out long actorId) ||
            !ActorPresentationRenderer.TryGetPresentationState(
                actorId,
                out ActorPresentationSample sample,
                out Vector3 position,
                out bool visible,
                out _) ||
            !visible)
        {
            return;
        }

        cursorSubspeciesVisible = true;
        cursorSubspeciesStart = World.world.getMousePos();
        cursorSubspeciesEnd = position + (Vector3)sample.HeadOffset;
    }

    private static void Draw(
        QuantumSpriteAsset asset,
        GhostSample[] samples,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            ref GhostSample sample = ref samples[i];
            QuantumSprite visual = asset.group_system.getNext();
            visual.setSprite(sample.Sprite);
            visual.setPosOnly(ref sample.Position);
            visual.setScale(ref sample.Scale);
            visual.setRotation(ref sample.Rotation);
            visual.setColor(ref sample.Color);
        }
    }

    private static void DrawPlots(
        QuantumSpriteAsset asset,
        PlotSample[] samples,
        int count)
    {
        float zoom =
            QuantumSpriteLibrary.getCameraScaleZoomMultiplier(asset);
        for (int i = 0; i < count; i++)
        {
            ref PlotSample sample = ref samples[i];
            Vector3 position = sample.Position;
            position.y += 5.5f * zoom * sample.OffsetScale;

            float finalScale = asset.base_scale * sample.Scale;
            if (asset.add_camera_zoom_multiplier)
            {
                finalScale *= zoom;
            }

            if (asset.selected_city_scale)
            {
                finalScale *= sample.CityScale;
            }

            QuantumSprite visual =
                QuantumSpriteLibrary.drawQuantumSprite(
                    asset,
                    position,
                    null,
                    null,
                    null,
                    null,
                    1f,
                    false,
                    finalScale);
            visual.setSprite(sample.Sprite);
            CircleIconShaderMod component =
                visual.GetComponent<CircleIconShaderMod>();
            component.sprite_renderer_with_mat.sprite = sample.Sprite;
            component.setShaderVal(sample.Progress);
        }
    }

    private static bool IsPrepared()
    {
        return preparedFrame == Time.frameCount &&
               ActorPresentationRenderer.PreparedSnapshot != null;
    }

    private static void EnsureCapacity(
        ref GhostSample[] samples,
        int capacity)
    {
        if (samples.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(32, samples.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref samples, nextCapacity);
    }

    private static void EnsureCapacity(
        ref PlotSample[] samples,
        int capacity)
    {
        if (samples.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(16, samples.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref samples, nextCapacity);
    }

    private static void EnsureCapacity(
        ref MagnetSample[] samples,
        int capacity)
    {
        if (samples.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(16, samples.Length);
        while (nextCapacity < capacity)
        {
            nextCapacity = checked(nextCapacity * 2);
        }

        Array.Resize(ref samples, nextCapacity);
    }
}
