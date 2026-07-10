using System;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>
/// 将角色装备的法器图标绘制在世界空间中，并随角色移动。
/// </summary>
public class EquippedArtifactRenderSystem : QuerySystem<ActorBinder>
{
    private const int RingCapacity = 6;
    private const float TargetWorldSize = 0.34f;

    private readonly MonoObjPool<ArtifactRenderer> _pool;

    public EquippedArtifactRenderSystem()
    {
        var root = new GameObject("equipped_artifacts");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview(nameof(EquippedArtifactRenderSystem) + "_Artifact")
            .AddComponent<ArtifactRenderer>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.Objects_4;
        prefab.sprite_renderer.sortingOrder = 6;

        _pool = new MonoObjPool<ArtifactRenderer>(
            prefab,
            root.transform,
            active_action: renderer => renderer.transform.localScale = Vector3.one);
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        _pool.ResetToStart();
        if (!MapBox.isRenderGameplay() || MapBox.isRenderMiniMap())
        {
            _pool.ClearUnsed();
            return;
        }

        float time = (float)World.world.getCurSessionTime();
        Query.ForEachEntity([Hotfixable](ref ActorBinder binder, Entity owner) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive() || !actor.is_visible) return;

            var relations = owner.GetRelations<EquippedArtifactRelation>();
            int artifactCount = 0;
            for (int i = 0; i < relations.Length; i++)
            {
                if (CanRender(relations[i].artifact)) artifactCount++;
            }
            if (artifactCount == 0) return;

            float actorScale = Mathf.Max(actor.stats[S.scale], 0.1f);
            int visualIndex = 0;
            for (int i = 0; i < relations.Length; i++)
            {
                Entity artifact = relations[i].artifact;
                if (!TryResolveSprite(artifact, out Sprite sprite)) continue;

                ArtifactRenderer renderer = _pool.GetNext();
                renderer.sprite_renderer.sprite = sprite;
                renderer.sprite_renderer.color = Color.white;
                renderer.sprite_renderer.flipX = false;
                renderer.transform.localRotation = Quaternion.identity;
                renderer.transform.localScale = Vector3.one * ResolveScale(sprite, actorScale, time, visualIndex) * 40;
                renderer.transform.localPosition = actor.cur_transform_position +
                                                   ResolveOffset(actor, visualIndex, artifactCount, actorScale, time);
                visualIndex++;
            }
        });

        _pool.ClearUnsed();
    }

    private static bool CanRender(Entity artifact)
    {
        return !artifact.IsNull && artifact.HasComponent<Artifact>() && artifact.HasComponent<SpecialItem>();
    }

    private static bool TryResolveSprite(Entity artifact, out Sprite sprite)
    {
        ref SpecialItem item = ref artifact.GetComponent<SpecialItem>();
        if (item.self.IsNull)
        {
            item.self = artifact;
        }
        sprite = item.GetSprite();
        return sprite != null;
    }

    private static float ResolveScale(Sprite sprite, float actorScale, float time, int index)
    {
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (spriteSize <= 0f) return actorScale;

        float breath = 1f + Mathf.Sin(time * 2.2f + index * 0.73f) * 0.035f;
        return TargetWorldSize * actorScale / spriteSize * breath;
    }

    private static Vector3 ResolveOffset(Actor actor, int index, int count, float actorScale, float time)
    {
        float bob = Mathf.Sin(time * 2.2f + index * 0.73f) * 0.04f * actorScale;
        if (count == 1)
        {
            float side = actor.flip ? -1f : 1f;
            return new Vector3(side * 0.38f * actorScale, 0.62f * actorScale + bob, 0f);
        }

        int ring = index / RingCapacity;
        int indexInRing = index % RingCapacity;
        int ringCount = Math.Min(RingCapacity, count - ring * RingCapacity);
        float direction = ring % 2 == 0 ? 1f : -1f;
        float angle = direction * time * (0.42f + ring * 0.05f) +
                      indexInRing * Mathf.PI * 2f / ringCount;
        float radius = (0.44f + ring * 0.18f) * actorScale;
        float height = (0.62f + ring * 0.08f) * actorScale;
        return new Vector3(
            Mathf.Cos(angle) * radius,
            height + Mathf.Sin(angle) * 0.24f * actorScale + bob,
            0f);
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class ArtifactRenderer : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}
