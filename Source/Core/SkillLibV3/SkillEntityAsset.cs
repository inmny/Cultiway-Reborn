using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public enum SkillEntityType
{
    Attack
}
public delegate bool OnObjCollision(ref SkillContext context, Entity skill_container, Entity skill_entity, BaseSimObject target);
public class SkillEntityAsset : Asset
{
    public Entity PrefabEntity;
    public ElementComposition Element;
    public EntityStore World => ModClass.I.SkillV3.World;
    public OnObjCollision OnObjCollision;
    public SkillEntityType Type;

    public SkillEntityAsset SetupColliderSphere(float radius, ColliderConfig config)
    {
        PrefabEntity.Add(new ColliderSphere()
        {
            Radius = radius
        }, config);
        return this;
    }

    public SkillEntityAsset SetupDefaultTraj(TrajectoryAsset traj)
    {
        var traj_component = new Trajectory()
        {
            ID = traj.id
        };
        PrefabEntity.AddComponent(traj_component);
        traj.OnInit?.Invoke(PrefabEntity);
        return this;
    }
    public SkillEntityAsset SetupCommonPrefab(string effect_path, float scale = 0.1f, bool anim_loop = true)
    {
        PrefabEntity = World.CreateEntity(
            new SkillEntity()
            {
                SkillContainer = default,
                Asset = this
            },
            new SkillContext(),
            new Position(),
            new Rotation(Vector3.right),
            new Scale(scale),
            new AnimBindRenderer(),
            new AnimController()
            {
                meta = new ()
                {
                    frame_interval = 0.1f,
                    loop = anim_loop
                }
            },
            new AnimData()
            {
                frames = SpriteTextureLoader.getSpriteList(effect_path)
            },
            new AliveTimer()
            {
                value = 0f  
            },
            new AliveTimeLimit()
            {
                value  = 5f
            },
            Tags.Get<TagPrefab>());
        return this;
    }
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

    public override string ToString()
    {
        return id;
    }
}