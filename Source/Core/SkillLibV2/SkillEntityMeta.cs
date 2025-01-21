using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2;
public delegate void ModifierApplicationAction(Entity entity, Entity modifiers);
public class SkillEntityMeta
{
    private Entity      _prefab;
    public Entity default_modifier_container;
    private EntityStore _world;
    public readonly string id;
    public static readonly            ReadOnlyDictionary<string, SkillEntityMeta> AllDict;
    private protected static readonly Dictionary<string, SkillEntityMeta>         dict;
    private ModifierApplicationAction _apply_modifiers;
    static SkillEntityMeta()
    {
        dict = new();
        AllDict = new(dict);
    }
    private SkillEntityMeta(string id)
    {
        this.id = id;
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
            entity.GetComponent<AnimData>().next_frame_time = (float)(WorldboxGame.I.GetGameTime() + Time.deltaTime);

        return entity;
    }
    public Entity NewModifierContainer()
    {
        Entity entity = _world.CloneEntity(default_modifier_container);
        entity.RemoveTag<TagPrefab>();

        return entity;
    }

    public void ApplyModifiers(Entity entity, Entity modifiers)
    {
        _apply_modifiers?.Invoke(entity, modifiers);
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
        private readonly SkillEntityMeta _under_build;

        internal MetaBuilder(SkillEntityMeta meta)
        {
            _under_build = meta;
        }

        public MetaBuilder(string id)
        {
            if (AllDict.ContainsKey(id)) throw new DuplicateNameException(id);
            _under_build = new SkillEntityMeta(id);
            _under_build._world = ModClass.I.SkillV2.World;
            _under_build._prefab = _under_build._world.CreateEntity(new SkillEntity
            {
                Meta = _under_build
            }, new SkillCaster(), new SkillStrength(), new AliveTimer(), Tags.Get<TagPrefab>());
            _under_build.default_modifier_container = _under_build._world.CreateEntity(Tags.Get<TagPrefab>());
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
        public MetaBuilder AllowModifier<TModifier, TValue>(TModifier modifier)
            where TModifier : struct, IModifier<TValue>
        {
            _under_build.default_modifier_container.AddComponent(modifier);
            return this;
        }
        public MetaBuilder AppendModifierApplication(ModifierApplicationAction action)
        {
            _under_build._apply_modifiers += action;
            return this;
        }

        public SkillEntityMeta Build()
        {
            dict[_under_build.id] = _under_build;
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