using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core.Systems.Render;

public class RenderAnimFrameSystem : BaseSystem
{
    private readonly MonoObjPool<AnimRenderer>                                  _pool;
    private readonly ArchetypeQuery<Position, Scale, AnimData, AnimBindRenderer> init_query;
    private readonly ArchetypeQuery<Position, AnimBindRenderer>                  pos_query;
    private readonly ArchetypeQuery<Rotation, AnimBindRenderer>                  rot_query;
    private readonly ArchetypeQuery<Scale, AnimBindRenderer>                     scale_query;
    private readonly ArchetypeQuery<AnimBindRenderer>                            single_query;

    public RenderAnimFrameSystem(EntityStore world)
    {
        var obj = new GameObject("general_anims");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;

        _pool = AnimRenderer.NewPool(obj.transform);


        var filter = new QueryFilter();
        filter.WithoutAnyTags(Tags.Get<TagPrefab>());

        single_query = world.Query<AnimBindRenderer>(filter);
        init_query = world.Query<Position, Scale, AnimData, AnimBindRenderer>(filter);
        pos_query = world.Query<Position, AnimBindRenderer>(filter);
        rot_query = world.Query<Rotation, AnimBindRenderer>(filter);
        scale_query = world.Query<Scale, AnimBindRenderer>(filter);
    }

    protected override void OnUpdateGroup()
    {
        if (!MapBox.isRenderMiniMap())
        {
            init_query.ForEachComponents(
                (ref Position pos, ref Scale scale, ref AnimData anim_data, ref AnimBindRenderer bind_renderer) =>
                {
                    Sprite sprite = anim_data.CurrentFrame;
                    if (!NeedRender(sprite, ref pos, ref scale))
                    {
                        if (bind_renderer.value != null && bind_renderer.value.gameObject.activeSelf)
                        {
                            bind_renderer.value.gameObject.SetActive(false);
                        }
                        return;
                    }

                    if (bind_renderer.value != null)
                    {
                        bind_renderer.value.bind.sprite = sprite;
                        if (!bind_renderer.value.gameObject.activeSelf)
                        {
                            bind_renderer.value.gameObject.SetActive(true);
                        }
                    }
                    else
                    {
                        AnimRenderer renderer = _pool.GetNext();
                        renderer.bind.sprite = sprite;
                        bind_renderer.value = renderer;
                    }
                });
            pos_query.ForEachComponents((ref Position pos, ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value == null) return;
                bind_renderer.value.transform.localPosition = new Vector3(pos.x, pos.y + pos.z);
            });
            scale_query.ForEachComponents((ref Scale scale, ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value == null) return;
                bind_renderer.value.transform.localScale = scale.value;
            });
            rot_query.ForEachComponents((ref Rotation rot, ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value == null) return;
                bind_renderer.value.transform.localRotation =
                    Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, rot.in_plane + new Vector2(0, rot.z)));
            });
        }
        else
        {
            single_query.ForEachComponents((ref AnimBindRenderer bind_renderer) =>
            {
                if (bind_renderer.value!= null && bind_renderer.value.gameObject.activeSelf)
                {
                    bind_renderer.value.gameObject.SetActive(false);
                }
            });
        }
        /*
        rot_query.ForEachComponents((ref Rotation rot, ref AnimBindRenderer bind_renderer) =>
        {
            if (bind_renderer.value == null) return;
            bind_renderer.value.transform.localScale = Vector3.Scale(bind_renderer.value.transform.localScale,
                new Vector3(0.5f + 1 / Mathf.Max(rot.z / Mathf.Max(0.01f, rot.in_plane.magnitude) + 1, 1), 1, 1));
        });
        */
    }

    private bool NeedRender(Sprite sprite, ref Position pos, ref Scale scale)
    {
        return true;
    }
}