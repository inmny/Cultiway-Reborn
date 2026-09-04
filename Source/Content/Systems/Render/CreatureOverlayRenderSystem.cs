using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.CreatureCompositions.Visuals;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>
///     给当前可见、拥有当前身体的生物单位绘制器官附加图层。
///     只借用池化绘制对象；不可见或离开查询后立即归还。
/// </summary>
public sealed class CreatureOverlayRenderSystem : QuerySystem<ActorBinder, CreaturePhenotype>
{
    private const string SortingLayerName = RenderSortingLayerNames.EffectsTop_5;

    /// <summary>原版当前渲染精灵方法为 internal，这里在系统构建时绑定一次开放委托。</summary>
    private static readonly Func<Actor, Sprite> getSpriteToRender = CreateSpriteDelegate();

    private readonly MonoObjPool<OverlayRenderer> pool;
    private readonly List<CreatureVisualLayerAsset> resolvedLayers = new();

    public CreatureOverlayRenderSystem()
    {
        var root = new GameObject("creature_overlay_layers");
        root.transform.SetParent(World.world.transform);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefabPreview(nameof(CreatureOverlayRenderSystem) + "_Layer")
            .AddComponent<OverlayRenderer>();
        prefab.renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.renderer.sortingLayerName = SortingLayerName;

        pool = new MonoObjPool<OverlayRenderer>(prefab, root.transform);
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        if (!MapBox.isRenderGameplay() || MapBox.isRenderMiniMap())
        {
            pool.ResetToStart();
            pool.ClearUnsed();
            return;
        }

        pool.ResetToStart();
        Query.ForEachEntity((ref ActorBinder binder, ref CreaturePhenotype phenotype, Entity entity) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive() || !actor.is_visible) return;
            if (!phenotype.IsValid || !CreaturePhenotypeCompiler.TryGetCompiled(
                    phenotype.CompiledIndex, phenotype.Signature, out CompiledCreaturePhenotype compiled))
                return;

            // 主体精灵既是帧名来源，也是锚点比例的基准；取不到时图层整体不画。
            Sprite mainSprite = getSpriteToRender?.Invoke(actor);
            if (mainSprite == null) return;

            if (!CreatureOverlayRenderService.TryResolveLayers(compiled, actor.asset.id, resolvedLayers))
                return;

            CreatureVisualRigAsset rig =
                Content.Libraries.Manager.CreatureVisualRigLibrary.get(compiled.Morph.VisualRigId);
            float actorScale = Mathf.Max(actor.stats[S.scale], 0.05f);
            bool lookingLeft = actor.is_looking_left;
            float side = lookingLeft ? -1f : 1f;
            Bounds mainBounds = mainSprite.bounds;

            for (int i = 0; i < resolvedLayers.Count; i++)
            {
                CreatureVisualLayerAsset layer = resolvedLayers[i];

                // 颜色先统一解析：淡染类把颜色烘进合成纹理，轮廓件直接用在渲染器上。
                Color tint = layer.TintPolicy switch
                {
                    CreatureLayerTintPolicy.KingdomColor => actor.kingdom != null
                        ? (Color)actor.kingdom.getColor().k_color_0
                        : Color.white,
                    CreatureLayerTintPolicy.Glow => new Color(1.4f, 1.4f, 1.2f, 1f),
                    CreatureLayerTintPolicy.FixedColor => layer.TintColor,
                    _ => Color.white,
                };

                Sprite sprite;
                if (layer.MaskToBody)
                {
                    // 淡染纹理按主体轮廓遮罩合成，只显示身体范围内的部分。
                    if (!CreatureOverlayRenderService.TryGetMaskedSprite(layer, mainSprite, tint, out sprite))
                        continue;
                    tint = Color.white;
                }
                else
                {
                    if (!CreatureOverlayRenderService.TryGetFrameSprite(layer, mainSprite.name, out sprite))
                        continue;
                }

                OverlayRenderer overlay = pool.GetNext();
                SpriteRenderer renderer = overlay.renderer;
                renderer.sprite = sprite;
                renderer.flipX = lookingLeft;
                renderer.color = tint;
                renderer.sortingOrder = 20 + i;

                if (layer.MaskToBody)
                {
                    // 合成体与主体共用枢轴与像素密度：零偏移、同缩放，才能与身体逐像素对齐。
                    overlay.transform.localPosition = actor.cur_transform_position;
                    overlay.transform.localScale = Vector3.one * actorScale;
                }
                else
                {
                    // 锚点按主体精灵包围盒取比例：x 沿朝向正方向（头为正、尾为负），y 向上。
                    Vector2 anchor = rig != null ? rig.ResolveAnchor(layer.Anchor) : Vector2.zero;
                    Vector3 offset = new(
                        (anchor.x * mainBounds.size.x + layer.Offset.x) * actorScale * side,
                        (anchor.y * mainBounds.size.y + layer.Offset.y) * actorScale,
                        0f);
                    overlay.transform.localPosition = actor.cur_transform_position + offset;
                    overlay.transform.localScale =
                        Vector3.one * actorScale * Mathf.Max(layer.Scale, 0.05f);
                }

                overlay.transform.localRotation = Quaternion.identity;
            }
        });

        pool.ClearUnsed();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class OverlayRenderer : MonoBehaviour
    {
        public SpriteRenderer renderer;
    }

    /// <summary>绑定原版内部方法，取不到时图层退化为按动画状态缺席绘制。</summary>
    private static Func<Actor, Sprite> CreateSpriteDelegate()
    {
        var method = typeof(Actor).GetMethod(
            "getSpriteToRender",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        return method == null
            ? null
            : (Func<Actor, Sprite>)Delegate.CreateDelegate(typeof(Func<Actor, Sprite>), method);
    }
}
