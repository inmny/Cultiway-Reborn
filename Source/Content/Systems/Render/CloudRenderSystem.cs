using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
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

        var prefab = ModClass.NewPrefabPreview("XianCloud").AddComponent<Cloud>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        prefab.sprite_renderer.sprite = SpriteTextureLoader.getSprite("cultiway/special_effects/clouds/simple_cloud");
        _pool = new(prefab, obj.transform,
            active_action: [Hotfixable](cloud) => { cloud.transform.localScale = Vector3.one * 0.01f; });
    }

    [Hotfixable]
    protected override void OnUpdate()
    {
        _pool.ResetToStart();
        if (!MapBox.isRenderMiniMap())
            Query.ForEachEntity([Hotfixable](ref ActorBinder actor_binder, ref Xian xian, Entity e) =>
            {
                Actor a = actor_binder.Actor;
                if (!a.is_visible || !a.data.hasFlag(ContentActorDataKeys.IsFlying_flag)) return;
                Cloud cloud = _pool.GetNext();
                cloud.transform.localPosition = actor_binder.Actor.transform.localPosition;
                cloud.transform.localScale = Vector3.one * a.stats[S.scale];
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