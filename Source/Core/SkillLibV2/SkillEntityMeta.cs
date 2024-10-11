using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2;

public class SkillEntityMeta
{
    private Entity _prefab;

    private SkillEntityMeta()
    {
    }

    public Entity NewEntity()
    {
        EntityStore world = _prefab.Store;
        Entity entity = world.CloneEntitySimply(_prefab);
        foreach (Entity child in _prefab.ChildEntities) entity.AddChild(world.CloneEntitySimply(child));

        var list = new EntityList();
        list.AddTree(entity);
        var batch = new EntityBatch();
        batch.RemoveTag<TagPrefab>();
        list.ApplyBatch(batch);

        return entity;
    }

    public class MetaBuilder
    {
        private readonly SkillEntityMeta _under_build;

        public MetaBuilder()
        {
            _under_build = new SkillEntityMeta();
            _under_build._prefab = ModClass.I.SkillV2.World.CreateEntity(new SkillEntity
            {
                Meta = _under_build
            }, new SkillCaster(), new AliveTimer(), Tags.Get<TagPrefab>());
        }

        public MetaBuilder NewTrigger<TTrigger, TContext>(TTrigger trigger, TContext context, out int trigger_id)
            where TContext : struct, IEventContext
            where TTrigger : struct, IEventTrigger<TTrigger, TContext>
        {
            Entity trigger_entity = ModClass.I.SkillV2.World.CreateEntity(trigger, context, Tags.Get<TagPrefab>());
            _under_build._prefab.AddChild(trigger_entity);
            trigger_id = trigger_entity.Id;
            return this;
        }

        public MetaBuilder AddTriggerComponent<TComponent>(int trigger_id, TComponent component)
            where TComponent : struct, IComponent
        {
            ModClass.I.SkillV2.World.GetEntityById(trigger_id).AddComponent(component);
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
            return _under_build;
        }
    }
}