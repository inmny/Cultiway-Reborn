using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.SkillLib.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.SkillLib.Components.Position;
using Rotation = Cultiway.Core.SkillLib.Components.Rotation;

namespace Cultiway.Core.SkillLib.Systems.Render;

public class AnimSystem : QuerySystem<Position, SkillAnimData, SkillEntityComponent>
{
    private readonly MonoObjPool<SkillRenderer>                              _pool;
    private readonly ArchetypeQuery<Position, OverAnimFrames, SkillAnimData> over_frames_query;
    private readonly ArchetypeQuery<Position, SkillAnimData>                 pos_query;
    private readonly ArchetypeQuery<Rotation, SkillAnimData>                 rot_query;
    private readonly ArchetypeQuery<Scale, SkillAnimData>                    scale_query;

    private ArchetypeQuery<SkillAnimData> single_query;

    public AnimSystem(EntityStore world)
    {
        var obj = new GameObject("skill_anims");
        obj.transform.SetParent(World.world.transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = Vector3.one;

        var prefab = ModClass.NewPrefab("SkillRenderer").AddComponent<SkillRenderer>();
        prefab.sprite_renderer = prefab.GetComponent<SpriteRenderer>();
        prefab.sprite_renderer.sortingLayerName = RenderSortingLayerNames.EffectsTop_5;
        _pool = new(prefab, obj.transform);

        Filter.WithoutAnyTags(Tags.Get<PrefabTag>());

        single_query = world.Query<SkillAnimData>(Filter);
        pos_query = world.Query<Position, SkillAnimData>(Filter);
        scale_query = world.Query<Scale, SkillAnimData>(Filter);
        rot_query = world.Query<Rotation, SkillAnimData>(Filter);
        over_frames_query = world.Query<Position, OverAnimFrames, SkillAnimData>(Filter);
    }

    protected override void OnUpdate()
    {
        single_query.ForEachEntity((ref SkillAnimData anim_data, Entity _) => { anim_data.bind_renderer = null; });
        _pool.ResetToStart();
        Query.WithoutAllComponents(ComponentTypes.Get<OverAnimFrames>());
        Query.ForEachEntity([Hotfixable](ref Position             pos,                ref SkillAnimData anim_data,
                                         ref SkillEntityComponent skill_entity_asset, Entity            _) =>
        {
            var anim_setting = skill_entity_asset.asset.anim_setting;
            if (anim_setting == null) return;
            var sprite = anim_setting.frames[anim_data.idx];
            if (!NeedRender(sprite, ref pos))
            {
                return;
            }

            var renderer = _pool.GetNext().sprite_renderer;
            anim_data.bind_renderer = renderer;
            renderer.sprite = sprite;
        });
        over_frames_query.ForEachEntity((ref Position pos, ref OverAnimFrames over_frames, ref SkillAnimData anim_data,
                                         Entity       _) =>
        {
            Sprite sprite = over_frames.frames[anim_data.idx];
            if (!NeedRender(sprite, ref pos)) return;

            SpriteRenderer renderer = _pool.GetNext().sprite_renderer;
            anim_data.bind_renderer = renderer;
            renderer.sprite = sprite;
        });
        _pool.ClearUnsed();
        foreach (var renderer in _pool.ActiveObjs)
        {
            renderer.transform.localScale = Vector3.one;
            renderer.transform.localRotation = Quaternion.identity;
        }

        pos_query.ForEachEntity((ref Position pos, ref SkillAnimData anim_data, Entity _) =>
        {
            if (anim_data.bind_renderer == null) return;
            anim_data.bind_renderer.transform.localPosition = pos.value;
        });
        scale_query.ForEachEntity(((ref Scale scale, ref SkillAnimData anim_data, Entity _) =>
        {
            if (anim_data.bind_renderer == null) return;
            anim_data.bind_renderer.transform.localScale = scale.value;
        }));
        rot_query.ForEachEntity(((ref Rotation rot, ref SkillAnimData anim_data, Entity _) =>
        {
            if (anim_data.bind_renderer == null) return;
            anim_data.bind_renderer.transform.localRotation = rot.value;
        }));
    }

    private static bool NeedRender(Sprite sprite, ref Position pos)
    {
        return true;
    }

    [RequireComponent(typeof(SpriteRenderer))]
    class SkillRenderer : MonoBehaviour
    {
        public SpriteRenderer sprite_renderer;
    }
}