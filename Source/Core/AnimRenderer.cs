using Cultiway.Abstract;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core;


[RequireComponent(typeof(SpriteRenderer))]
public class AnimRenderer : MonoBehaviour
{
    public SpriteRenderer            bind;
    public MonoObjPool<AnimRenderer> pool;

    public void Return()
    {
        pool.Return(this);
    }
    public static MonoObjPool<AnimRenderer> NewPool(Transform parent)
    {
        var prefab = ModClass.NewPrefabPreview(nameof(AnimRenderer)).AddComponent<AnimRenderer>();
        prefab.bind = prefab.GetComponent<SpriteRenderer>();
        prefab.bind.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        return new MonoObjPool<AnimRenderer>(prefab, parent, s => s.pool = prefab.pool);
    }
}