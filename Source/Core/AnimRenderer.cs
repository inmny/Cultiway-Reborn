using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Core;


[RequireComponent(typeof(SpriteRenderer))]
public class AnimRenderer : MonoBehaviour
{
    public SpriteRenderer            bind;
    public MonoObjPool<AnimRenderer> pool;
    public bool                       hasTint;
    public Material                   defaultMaterial;
    private SpriteRenderer[]          _afterimages;

    public void Return()
    {
        ResetVisualState();
        pool.Return(this);
    }

    public void ResetVisualState()
    {
        HideAfterimage();
        hasTint = false;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        if (bind == null) return;

        bind.sprite = null;
        bind.color = Color.white;
        bind.flipX = false;
        bind.flipY = false;
        bind.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        bind.sortingOrder = 0;
        bind.drawMode = SpriteDrawMode.Simple;
        bind.tileMode = SpriteTileMode.Continuous;
        bind.size = Vector2.one;
        if (defaultMaterial != null)
        {
            bind.sharedMaterial = defaultMaterial;
        }
    }

    public void HideAfterimage()
    {
        if (_afterimages == null) return;

        for (var i = 0; i < _afterimages.Length; i++)
        {
            var renderer = _afterimages[i];
            if (renderer == null) continue;

            renderer.sprite = null;
            renderer.color = Color.clear;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.gameObject.SetActive(false);
        }
    }

    public void SetAfterimage(Sprite sprite, Color baseColor, ref AnimAfterimage afterimage, Vector2 localDirection,
        float movementAngle)
    {
        var count = Mathf.Clamp(afterimage.Count, 0, AnimAfterimage.MaxLayers);
        if (count <= 0 || sprite == null)
        {
            HideAfterimage();
            return;
        }

        EnsureAfterimages(count);

        var direction = localDirection;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = afterimage.LocalDirection;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.left;
            }
        }
        direction.Normalize();

        var bounds = sprite.bounds.size;
        var spriteSize = Mathf.Max(bounds.x, bounds.y);
        var spacing = Mathf.Max(afterimage.MinSpacing, spriteSize * afterimage.SpacingRatio);
        var tint = afterimage.Tint;

        for (var i = 0; i < _afterimages.Length; i++)
        {
            var renderer = _afterimages[i];
            if (i >= count)
            {
                renderer.gameObject.SetActive(false);
                continue;
            }

            var t = count <= 1 ? 1f : 1f - (float)i / (count - 1);
            var color = new Color(
                baseColor.r * tint.r,
                baseColor.g * tint.g,
                baseColor.b * tint.b,
                baseColor.a * Mathf.Lerp(afterimage.OldestAlpha, afterimage.NewestAlpha, t) * tint.a);

            renderer.sprite = sprite;
            renderer.flipX = bind.flipX;
            renderer.flipY = bind.flipY;
            renderer.sortingLayerID = bind.sortingLayerID;
            renderer.sortingOrder = bind.sortingOrder - i - 1;
            renderer.sharedMaterial = bind.sharedMaterial;
            renderer.color = color;
            if (afterimage.Layout == AnimAfterimageLayout.Angular)
            {
                SetAngularAfterimageTransform(renderer.transform, ref afterimage, movementAngle, i + 1);
            }
            else
            {
                renderer.transform.localPosition = new Vector3(direction.x * spacing * (i + 1),
                    direction.y * spacing * (i + 1), 0f);
                renderer.transform.localRotation = Quaternion.identity;
            }
            renderer.transform.localScale = Vector3.one;
            if (!renderer.gameObject.activeSelf)
            {
                renderer.gameObject.SetActive(true);
            }
        }
    }

    private void SetAngularAfterimageTransform(Transform afterimageTransform, ref AnimAfterimage afterimage,
        float movementAngle, int layer)
    {
        var angle = afterimage.ArcDirection * afterimage.ArcDegreesPerLayer * layer;
        var currentDirection = Quaternion.Euler(0f, 0f, movementAngle) * Vector3.right;
        var previousDirection = Quaternion.Euler(0f, 0f, -angle) * currentDirection;
        var worldOffset = (previousDirection - currentDirection) * afterimage.ArcRadius;

        afterimageTransform.localPosition = transform.InverseTransformVector(worldOffset);
        afterimageTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void EnsureAfterimages(int count)
    {
        if (_afterimages != null && _afterimages.Length >= count) return;

        var oldLength = _afterimages == null ? 0 : _afterimages.Length;
        var newLength = Mathf.Max(count, oldLength);
        var renderers = new SpriteRenderer[newLength];
        if (_afterimages != null)
        {
            for (var i = 0; i < _afterimages.Length; i++)
            {
                renderers[i] = _afterimages[i];
            }
        }

        for (var i = oldLength; i < newLength; i++)
        {
            var obj = new GameObject($"{nameof(AnimAfterimage)}_{i}");
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.gameObject.SetActive(false);
            renderers[i] = renderer;
        }

        _afterimages = renderers;
    }

    public static MonoObjPool<AnimRenderer> NewPool(Transform parent)
    {
        var prefab = ModClass.NewPrefabPreview(nameof(AnimRenderer)).AddComponent<AnimRenderer>();
        prefab.bind = prefab.GetComponent<SpriteRenderer>();
        prefab.bind.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.defaultMaterial = prefab.bind.sharedMaterial;
        prefab.pool = new MonoObjPool<AnimRenderer>(prefab, parent, s =>
        {
            s.pool = prefab.pool;
            s.defaultMaterial = prefab.defaultMaterial;
            s.ResetVisualState();
        }, s => s.ResetVisualState());
        return prefab.pool;
    }
}
