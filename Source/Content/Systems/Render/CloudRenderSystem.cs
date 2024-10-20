using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Const;
using Cultiway.Content.CultisysComponents;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

public class CloudRenderSystem : QuerySystem<ActorBinder, Xian>
{
    private MonoObjPool<Cloud> _pool;

    public CloudRenderSystem()
    {
        var obj = new GameObject("xian_clouds");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefab("XianCloud").AddComponent<Cloud>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sprite = SpriteTextureLoader.getSprite("cultiway/special_effects/clouds/clouds");
        _pool = new(prefab, obj.transform,
            active_action: [Hotfixable](cloud) => { cloud.transform.localScale = Vector3.one * 0.01f; });
    }

    protected override void OnUpdate()
    {
        _pool.ResetToStart();
        Query.ForEachEntity([Hotfixable](ref ActorBinder actor_binder, ref Xian xian, Entity e) =>
        {
            var a = actor_binder.Actor;
            if (!a.data.hasFlag(ContentActorDataKeys.IsFlying_flag)) return;
            var cloud = _pool.GetNext();
            cloud.transform.localPosition = actor_binder.Actor.transform.localPosition;
            cloud.sprite_renderer.flipX = a.flip;
        });
        _pool.ClearUnsed();
    }

    [RequireComponent(typeof(SpriteRenderer))]
    class Cloud : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}