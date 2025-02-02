using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2;

public class TriggerActionBaseMeta
{
    public static readonly            ReadOnlyDictionary<string, TriggerActionBaseMeta> AllDict;
    private protected static readonly Dictionary<string, TriggerActionBaseMeta>         dict;
    public readonly                   Entity                                            default_modifier_container;
    public readonly                   string                                            id;

    static TriggerActionBaseMeta()
    {
        dict = new Dictionary<string, TriggerActionBaseMeta>();
        AllDict = new ReadOnlyDictionary<string, TriggerActionBaseMeta>(dict);
    }

    protected TriggerActionBaseMeta(string id, Entity default_modifier_container)
    {
        this.id = id;
        this.default_modifier_container = default_modifier_container;
    }

    public Entity NewModifierContainer()
    {
        EntityStore world = default_modifier_container.Store;
        Entity entity = world.CloneEntity(default_modifier_container);
        entity.RemoveTag<TagPrefab>();

        return entity;
    }
}

public class TriggerActionMeta<TTrigger, TContext> : TriggerActionBaseMeta
    where TContext : struct, IEventContext
    where TTrigger : struct, IEventTrigger<TTrigger, TContext>
{
    public delegate void ActionType(ref TTrigger trigger, ref TContext context, Entity skill_entity,
                                    Entity       action_modifiers, Entity entity_modifiers);

    private TriggerActionMeta(string id, Entity default_modifier_container) : base(id, default_modifier_container)
    {
    }

    internal ActionType Action { get; private set; }

    public void Invoke(ref TTrigger trigger, ref TContext context, Entity trigger_entity)
    {
        Entity skill_entity = trigger_entity.Parent;
        var entity_meta = skill_entity.GetComponent<SkillEntity>().Meta;
        Action(ref trigger, ref context, skill_entity,
            skill_entity.GetComponent<SkillCaster>().value
                .GetSkillActionModifiers(trigger.TriggerActionMeta.id, default_modifier_container),
            skill_entity.GetComponent<SkillCaster>().value
                .GetSkillEntityModifiers(entity_meta.id, entity_meta.default_modifier_container));
    }

    public static MetaBuilder StartBuild(string id)
    {
        return new MetaBuilder(id);
    }

    public MetaBuilder StartModify()
    {
        return new MetaBuilder(this);
    }

    public class MetaBuilder
    {
        private readonly        TriggerActionMeta<TTrigger, TContext> _under_build;

        public MetaBuilder(string id)
        {
            var actual_id = $"{typeof(TTrigger)}-{typeof(TContext)}.{id}";
            if (AllDict.ContainsKey(actual_id)) throw new DuplicateNameException(actual_id);

            _under_build = new TriggerActionMeta<TTrigger, TContext>(actual_id, ModClass.I.SkillV2.World.CreateEntity(
                new ModifierContainerEntity(),
                Tags.Get<TagPrefab>()
            ));
        }

        public MetaBuilder(TriggerActionMeta<TTrigger, TContext> meta)
        {
            _under_build = meta;
        }

        public MetaBuilder AppendAction(ActionType action)
        {
            _under_build.Action += action;
            return this;
        }
        public MetaBuilder PrependAction(ActionType action)
        {
            _under_build.Action = action + _under_build.Action;
            return this;
        }

        public MetaBuilder AllowModifier<TModifier, TValue>(TModifier default_modifier = default)
            where TModifier : struct, IModifier<TValue>
        {
            _under_build.default_modifier_container.AddComponent(default_modifier);
            return this;
        }

        public TriggerActionMeta<TTrigger, TContext> Build()
        {
            dict[_under_build.id] = _under_build;
            return _under_build;
        }
    }
}