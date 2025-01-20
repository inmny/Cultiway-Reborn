using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class StatusEffectAsset : Asset
{
    public BaseStats stats = new();
    private Entity _prefab;
    private EntityStore _world;
    public StatusEffectAsset()
    {
        
    }

    private string f_desc_key;
    private string f_name_key;
    private string name_key => f_name_key ??= $"Cultiway.StatusEffect.{id}";
    private string desc_key => f_desc_key ??= $"Cultiway.StatusEffect.{id}.Info";
    private string given_name;
    private string given_desc;

    public override string ToString()
    {
        return id;
    }

    public string GetName()
    {
        return string.IsNullOrEmpty(given_name) ?LM.Get(name_key) : given_name;
    }

    public string GetDescription()
    {
        return string.IsNullOrEmpty(given_desc) ? LM.Get(desc_key) : given_desc;
    }
    public Entity NewEntity()
    {
        Entity entity = _world.CloneEntity(_prefab);
        foreach (Entity child in _prefab.ChildEntities) entity.AddChild(_world.CloneEntity(child));

        var list = new EntityList(_world);
        list.AddTree(entity);
        var batch = new EntityBatch();
        batch.RemoveTag<TagPrefab>();
        list.ApplyBatch(batch);

        if (entity.HasComponent<AnimData>())
            entity.GetComponent<AnimData>().next_frame_time = (float)(World.world.mapStats.worldTime + Time.deltaTime);

        return entity;
    }

    public static Builder StartBuild(string id)
    {
        return new Builder(id);
    }
    
    public class Builder
    {
        private StatusEffectAsset _under_build;
        public Builder(string id)
        {
            _under_build = new StatusEffectAsset()
            {
                id = id
            };
            _under_build._world = ModClass.I.W;
            _under_build._prefab = _under_build._world.CreateEntity(new StatusComponent()
            {
                id = id
            }, new AliveTimer(), Tags.Get<TagPrefab>());
        }
        public Builder SetStats(BaseStats stats)
        {
            _under_build.stats = stats;
            return this;
        }
        public Builder SetDuration(float duration)
        {
            _under_build._prefab.AddComponent(new AliveTimeLimit()
            {
                value = duration
            });
            return this;
        }
        public Builder SetName(string name)
        {
            _under_build.given_name = name;
            return this;
        }
        public Builder SetDescription(string desc)
        {
            _under_build.given_desc = desc;
            return this;
        }
        public StatusEffectAsset Build()
        {
            ModClass.L.StatusEffectLibrary.add(_under_build);
            return _under_build;
        }
    }
}