using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Visuals;

public static class SpecialItemIconVfx
{
    private const float VanillaAteDuration = 1f;
    private const float GainDuration = 0.82f;
    private const int MaxAcceptPerFrame = 36;

    private static readonly ConcurrentQueue<Request> Requests = new();
    private static readonly List<Running> RunningItems = new();

    public static void QueueGain(Actor actor, Entity item)
    {
        if (actor == null || item.IsNull) return;
        if (!actor.is_visible) return;

        Requests.Enqueue(new Request
        {
            ActorId = actor.data?.id ?? -1L,
            ItemEntityId = item.Id,
            Mode = Mode.Gain,
            StartSessionTime = CurrentSessionTime()
        });
    }

    public static void QueueConsumeElixir(Actor actor, Entity item)
    {
        if (actor == null || item.IsNull) return;
        if (!actor.is_visible) return;
        var sprite = ResolveSprite(item);
        if (sprite == null) return;

        Requests.Enqueue(new Request
        {
            ActorId = actor.data?.id ?? -1L,
            Sprite = sprite,
            Mode = Mode.Consume,
            StartSessionTime = CurrentSessionTime()
        });
    }

    public static void Draw(QuantumSpriteAsset asset)
    {
        if (!MapBox.isRenderGameplay()) return;

        AcceptQueued();

        var curTime = World.world.getCurSessionTime();
        for (var i = RunningItems.Count - 1; i >= 0; i--)
        {
            var item = RunningItems[i];
            var duration = item.Mode == Mode.Consume ? VanillaAteDuration : GainDuration;
            var elapsed = (float)(curTime - item.StartSessionTime);
            if (elapsed > duration)
            {
                RunningItems.RemoveAt(i);
                continue;
            }

            var t = Mathf.Clamp01(elapsed / duration);
            if (item.Mode == Mode.Consume)
            {
                DrawConsume(asset, item, t);
            }
            else
            {
                DrawGain(asset, item, t);
            }
        }
    }

    private static void AcceptQueued()
    {
        var accepted = 0;
        while (accepted < MaxAcceptPerFrame && Requests.TryDequeue(out var request))
        {
            var actor = ResolveActor(request.ActorId);
            if (actor == null || actor.isRekt()) continue;

            var duration = request.Mode == Mode.Consume ? VanillaAteDuration : GainDuration;
            if (CurrentSessionTime() - request.StartSessionTime > duration) continue;

            var sprite = request.Sprite;
            if (sprite == null && request.ItemEntityId > 0)
            {
                sprite = ResolveSprite(request.ItemEntityId);
            }
            if (sprite == null) continue;

            RunningItems.Add(new Running
            {
                ActorId = request.ActorId,
                Sprite = sprite,
                Mode = request.Mode,
                StartSessionTime = request.StartSessionTime,
                FallbackPosition = actor.current_position
            });
            accepted++;
        }
    }

    private static void DrawConsume(QuantumSpriteAsset asset, Running item, float t)
    {
        var eased = EaseOutCubic(0f, 1f, t);
        var pos = CurrentActorPosition(item);
        pos.y += 1f + eased * 2f;

        var scaleProgress = Mathf.Min(eased, 0.5f);
        var alpha = t > 0.6f ? (1f - t) / 0.4f : 1f;

        DrawIcon(asset, item.Sprite, pos, scaleProgress, eased * 360f, new Color(alpha, alpha, alpha, alpha));
    }

    private static void DrawGain(QuantumSpriteAsset asset, Running item, float t)
    {
        var eased = EaseOutBack(0f, 1f, t);
        var fadeIn = Mathf.Clamp01(t / 0.16f);
        var fadeOut = t > 0.68f ? Mathf.Clamp01((1f - t) / 0.32f) : 1f;
        var alpha = fadeIn * fadeOut;

        var pos = CurrentActorPosition(item);
        pos.x += Mathf.Lerp(-0.34f, 0.12f, eased);
        pos.y += Mathf.Lerp(0.55f, 2.15f, eased);

        var scale = Mathf.Lerp(0.18f, 0.62f, Mathf.Clamp01(eased));
        var angle = Mathf.Sin(t * Mathf.PI) * -42f;
        DrawIcon(asset, item.Sprite, pos, scale, angle, new Color(0.72f, 1f, 0.7f, alpha));
    }

    private static Vector3 CurrentActorPosition(Running item)
    {
        var actor = ResolveActor(item.ActorId);
        if (actor != null && !actor.isRekt())
        {
            return actor.current_position;
        }
        return item.FallbackPosition;
    }

    private static void DrawIcon(QuantumSpriteAsset asset, Sprite sprite, Vector3 pos, float scale, float angle, Color color)
    {
        var quantumSprite = QuantumSpriteLibrary.drawQuantumSprite(asset, pos, null, null, null, null, scale);
        quantumSprite.setSprite(sprite);
        quantumSprite.transform.eulerAngles = new Vector3(0f, 0f, angle);
        quantumSprite.setColor(ref color);
    }

    private static Actor ResolveActor(long actorId)
    {
        if (actorId < 0 || World.world?.units == null) return null;
        return World.world.units.get(actorId);
    }

    private static Sprite ResolveSprite(int entityId)
    {
        lock (EntityStoreLock.GlobalLock)
        {
            var entity = ModClass.I.W.GetEntityById(entityId);
            return ResolveSprite(entity);
        }
    }

    private static Sprite ResolveSprite(Entity entity)
    {
        if (entity.IsNull || !entity.TryGetComponent(out SpecialItem item)) return null;
        if (item.self.IsNull)
        {
            item.self = entity;
        }
        return item.GetSprite();
    }

    private static double CurrentSessionTime()
    {
        return World.world?.getCurSessionTime() ?? 0.0;
    }

    private static float EaseOutCubic(float start, float end, float value)
    {
        value -= 1f;
        end -= start;
        return end * (value * value * value + 1f) + start;
    }

    private static float EaseOutBack(float start, float end, float value)
    {
        const float s = 1.70158f;
        end -= start;
        value -= 1f;
        return end * (value * value * ((s + 1f) * value + s) + 1f) + start;
    }

    private enum Mode
    {
        Gain,
        Consume
    }

    private struct Request
    {
        public long ActorId;
        public int ItemEntityId;
        public Sprite Sprite;
        public Mode Mode;
        public double StartSessionTime;
    }

    private struct Running
    {
        public long ActorId;
        public Sprite Sprite;
        public Mode Mode;
        public double StartSessionTime;
        public Vector3 FallbackPosition;
    }
}
