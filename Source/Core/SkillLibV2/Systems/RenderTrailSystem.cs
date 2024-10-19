using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Systems;

internal class RenderTrailSystem : QuerySystem<AnimBindRenderer, TrailBindRenderer>
{
    public enum TrailDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    private readonly MonoObjPool<CustomTrailRenderer>         _pool;
    private readonly ArchetypeQuery<Trail, TrailBindRenderer> _trail_query;

    public RenderTrailSystem(EntityStore world)
    {
        var prefab = ModClass.NewPrefab("CustomTrailRenderer").AddComponent<CustomTrailRenderer>();
        prefab.renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        _pool = new MonoObjPool<CustomTrailRenderer>(prefab, null, active_action: r => r.disabled = false,
            deactive_action: r => r.disabled = true);

        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        _trail_query = world.Query<Trail, TrailBindRenderer>(Filter);
    }

    protected override void OnUpdate()
    {
        Query.ForEachComponents((ref AnimBindRenderer anim_binder, ref TrailBindRenderer trail_binder) =>
        {
            if (anim_binder.value  == null) return;
            if (trail_binder.value != null) return;

            trail_binder.value = _pool.GetNext(parent: anim_binder.value.transform);
            trail_binder.value.parentRenderer = anim_binder.value.bind;
        });
        _trail_query.ForEachComponents((ref Trail trail, ref TrailBindRenderer trail_binder) => { });
    }

    [RequireComponent(typeof(SpriteRenderer))]
    internal class CustomTrailRenderer : MonoBehaviour
    {
        public SpriteRenderer parentRenderer;
        public SpriteRenderer renderer;
        public TrailDirection direction;
        public float          trailLength = 100;
        public float          trailBend   = 0.25f;

        public AnimationCurve trailCurve = new(new Keyframe(0f, 0f, 0f, 1f), new Keyframe(1f, 1f, 1f, 0f));

        public bool removeSpaces;
        public bool disabled;

        /// <summary>
        ///     Build a trail
        /// </summary>
        public void Build()
        {
            if (disabled || parentRenderer.sprite == null) return;

            Texture2D texture = CopyNotReadableSprite(parentRenderer.sprite);
            var trail = new Texture2D(texture.width, texture.height);

            ClearTexture(trail);

            var pixels = CreateTrailLine(texture, trail);

            FadeTrailLine(pixels, trail);

            if (trailBend > 0 && removeSpaces) FillSpaces(trail);

            trail.Apply();

            var pivot = new Vector2(parentRenderer.sprite.pivot.x / parentRenderer.sprite.rect.width,
                parentRenderer.sprite.pivot.y                     / parentRenderer.sprite.rect.height);

            renderer.sprite = Sprite.Create(trail, new Rect(0, 0, texture.width, texture.height), pivot,
                parentRenderer.sprite.pixelsPerUnit);
        }

        private Texture2D CopyNotReadableSprite(Sprite sprite)
        {
            var buffer = new Texture2D(sprite.texture.width, sprite.texture.height);
            RenderTexture render_texture = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            Graphics.Blit(sprite.texture, render_texture);
            RenderTexture.active = render_texture;
            buffer.ReadPixels(new Rect(0, 0, render_texture.width, render_texture.height), 0, 0);
            buffer.Apply();

            var texture = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
            var pixels = buffer.GetPixels((int)sprite.textureRect.x, (int)sprite.textureRect.y,
                (int)sprite.textureRect.width, (int)sprite.textureRect.height);

            ClearTexture(texture);
            texture.SetPixels((int)sprite.textureRectOffset.x, (int)sprite.textureRectOffset.y,
                (int)sprite.textureRect.width, (int)sprite.textureRect.height, pixels);
            texture.Apply();

            return texture;
        }

        private List<Vector2> CreateTrailLine(Texture2D texture, Texture2D trail)
        {
            var line = new List<Vector2>();
            Color color = Color.white;
            var pixels = texture.GetPixels();
            var width = texture.width;
            var height = texture.height;

            switch (direction)
            {
                case TrailDirection.Left:
                    for (var y = 0; y < width; y++)
                    for (var x = 0; x < height; x++)
                        if (FindEdge(trail, pixels, x, y, width, color, line))
                            break;
                    break;
                case TrailDirection.Right:
                    for (var y = 0; y < height; y++)
                    for (var x = width - 1; x >= 0; x--)
                        if (FindEdge(trail, pixels, x, y, width, color, line))
                            break;
                    break;
                case TrailDirection.Up:
                    for (var x = 0; x < width; x++)
                    for (var y = height - 1; y >= 0; y--)
                        if (FindEdge(trail, pixels, x, y, width, color, line))
                            break;
                    break;
                case TrailDirection.Down:
                    for (var x = 0; x < width; x++)
                    for (var y = 0; y < height; y++)
                        if (FindEdge(trail, pixels, x, y, width, color, line))
                            break;
                    break;
            }

            return line;
        }

        private static bool FindEdge(Texture2D     trail, Color[] pixels, int x, int y, int width, Color color,
                                     List<Vector2> line)
        {
            if (pixels[x + y * width].a > 0.5f)
            {
                trail.SetPixel(x, y, color);
                line.Add(new Vector2(x, y));
                return true;
            }

            return false;
        }

        private void FadeTrailLine(IList<Vector2> pixels, Texture2D trail)
        {
            var width = trail.width;
            var height = trail.height;
            var length = direction == TrailDirection.Left || direction == TrailDirection.Right
                ? pixels.Last().y - pixels.First().y
                : pixels.Last().x - pixels.First().x;

            foreach (Vector2 pixel in pixels)
            {
                var x = (int)pixel.x;
                var y = (int)pixel.y;
                var delta = direction == TrailDirection.Left || direction == TrailDirection.Right
                    ? y - pixels[0].y
                    : x - pixels[0].x;
                var iterations = trailLength * trailCurve.Evaluate(delta / length);
                Color color = trail.GetPixel(x, y);

                for (var i = 1; i < iterations; i++)
                {
                    color.a = 1 - i / iterations;

                    switch (direction)
                    {
                        case TrailDirection.Left:
                            if (x - i >= 0)
                                trail.SetPixel(x - i, (int)(y * Mathf.Cos(i * Mathf.Deg2Rad * trailBend)), color);
                        {
                            trail.SetPixel(x - i, (int)(y * Mathf.Cos(i * Mathf.Deg2Rad * trailBend)), color);
                        }
                            break;
                        case TrailDirection.Right:
                            if (x + i < width)
                                trail.SetPixel(x + i, (int)(y * Mathf.Cos(i * Mathf.Deg2Rad * trailBend)), color);

                            break;
                        case TrailDirection.Up:
                            if (y + i < height)
                                trail.SetPixel((int)(x * Mathf.Cos(i * Mathf.Deg2Rad * trailBend)), y + i, color);

                            break;
                        case TrailDirection.Down:
                            if (y - i >= 0)
                                trail.SetPixel((int)(x * Mathf.Cos(i * Mathf.Deg2Rad * trailBend)), y - i, color);

                            break;
                    }
                }
            }
        }

        private static void ClearTexture(Texture2D texture)
        {
            var pixels = new Color[texture.width * texture.height];
            Color clear = Color.clear;

            for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

            texture.SetPixels(pixels);
            texture.Apply();
        }

        private static void FillSpaces(Texture2D texture)
        {
            var pixels = texture.GetPixels();
            var width = texture.width;
            var height = texture.height;

            for (var y = 1; y < width - 1; y++)
            for (var x = 1; x < height - 1; x++)
            {
                if (pixels[x + y * width].a > 0) continue;
                Color above = pixels[x + (y + 1) * width];
                Color below = pixels[x + (y - 1) * width];

                if (above != Color.clear && below != Color.clear) texture.SetPixel(x, y, (above + below) / 2);
            }
        }
    }
}