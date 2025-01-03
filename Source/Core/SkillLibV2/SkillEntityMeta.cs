using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2;

public class SkillEntityMeta
{
    private Entity      _prefab;
    private EntityStore _world;

    private SkillEntityMeta()
    {
    }

    [Hotfixable]
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

    public static MetaBuilder StartBuild()
    {
        return new MetaBuilder();
    }

    public MetaBuilder StartModify()
    {
        return new MetaBuilder(this);
    }

    public class MetaBuilder
    {
        private readonly SkillEntityMeta _under_build;

        internal MetaBuilder(SkillEntityMeta meta)
        {
            _under_build = meta;
        }

        public MetaBuilder()
        {
            _under_build = new SkillEntityMeta();
            _under_build._world = ModClass.I.SkillV2.World;
            _under_build._prefab = _under_build._world.CreateEntity(new SkillEntity
            {
                Meta = _under_build
            }, new SkillCaster(), new SkillStrength(), new AliveTimer(), Tags.Get<TagPrefab>());
        }

        public MetaBuilder NewTrigger<TTrigger, TContext>(TTrigger trigger,           out int trigger_id,
                                                          TContext context = default, Tags    trigger_tags = default)
            where TContext : struct, IEventContext
            where TTrigger : struct, IEventTrigger<TTrigger, TContext>
        {
            trigger_tags.Add<TagPrefab>();
            Entity trigger_entity = _under_build._world.CreateEntity(trigger, context, trigger_tags);
            _under_build._prefab.AddChild(trigger_entity);
            trigger_id = trigger_entity.Id;
            return this;
        }

        public MetaBuilder AddTriggerComponent<TComponent>(int trigger_id, TComponent component)
            where TComponent : struct, IComponent
        {
            _under_build._world.GetEntityById(trigger_id).AddComponent(component);
            return this;
        }

        public MetaBuilder AddComponent<TComponent>(TComponent component)
            where TComponent : struct, IComponent
        {
            _under_build._prefab.AddComponent(component);
            return this;
        }

        public SkillEntityMeta Build()
        {
            if (!_under_build._prefab.HasComponent<AliveTimeLimit>())
            {
                _under_build._prefab.AddComponent(new AliveTimeLimit()
                {
                    value = SkillConst.recycle_time
                });
            }
            return _under_build;
        }
    }
}