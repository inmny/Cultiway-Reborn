using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class SkillEntityAsset : Asset
{
    public Entity PrefabEntity;
    public ElementComposition Element;
    public EntityStore World => ModClass.I.SkillV3.World;
    public Entity NewEntity()
    {
        Entity entity = World.CloneEntity(PrefabEntity);
        foreach (Entity child in PrefabEntity.ChildEntities) entity.AddChild(World.CloneEntity(child));

        var list = new EntityList(World);
        list.AddTree(entity);
        var batch = new EntityBatch();
        batch.RemoveTag<TagPrefab>();
        list.ApplyBatch(batch);

        if (entity.HasComponent<AnimData>())
            entity.GetComponent<AnimData>().next_frame_time = (float)(WorldboxGame.I.GetGameTime() + Time.deltaTime);

        return entity;
    }
}