using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Systems;

public class RenderAnimFrameSystem : BaseSystem
{
    private readonly MonoObjPool<SkillRenderer>                                  _pool;
    private readonly ArchetypeQuery<Position, Scale, AnimData, AnimBindRenderer> init_query;
    private readonly ArchetypeQuery<Position, AnimBindRenderer>                  pos_query;
    private readonly ArchetypeQuery<Rotation, AnimBindRenderer>                  rot_query;
    private readonly ArchetypeQuery<Scale, AnimBindRenderer>                     scale_query;
    private readonly ArchetypeQuery<AnimBindRenderer>                            single_query;

    public RenderAnimFrameSystem(EntityStore world)
    {
        var obj = new GameObject("skill_anims");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefab("SkillRenderer").AddComponent<SkillRenderer>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        _pool = new MonoObjPool<SkillRenderer>(prefab, obj.transform);


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
        single_query.ForEachComponents((ref AnimBindRenderer bind_renderer) => bind_renderer.value = null);
        _pool.ResetToStart();
        init_query.ForEachComponents(
            (ref Position pos, ref Scale scale, ref AnimData anim_data, ref AnimBindRenderer bind_renderer) =>
            {
                Sprite sprite = anim_data.CurrentFrame;
                if (!NeedRender(sprite, ref pos, ref scale)) return;

                bind_renderer.value = _pool.GetNext().sprite_renderer;
                bind_renderer.value.sprite = sprite;
            });
        _pool.ClearUnsed();
        pos_query.ForEachComponents((ref Position pos, ref AnimBindRenderer bind_renderer) =>
            bind_renderer.value.transform.localPosition = pos.value);
        rot_query.ForEachComponents((ref Rotation rot, ref AnimBindRenderer bind_renderer) =>
            bind_renderer.value.transform.localRotation = rot.value);
        scale_query.ForEachComponents((ref Scale scale, ref AnimBindRenderer bind_renderer) =>
            bind_renderer.value.transform.localScale = scale.value);
    }

    private bool NeedRender(Sprite sprite, ref Position pos, ref Scale scale)
    {
        return true;
    }

    [RequireComponent(typeof(SpriteRenderer))]
    private class SkillRenderer : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}