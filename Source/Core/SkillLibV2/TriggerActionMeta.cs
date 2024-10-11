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
}

public class TriggerActionMeta<TTrigger, TContext> : TriggerActionBaseMeta
    where TContext : struct, IEventContext
    where TTrigger : struct, IEventTrigger<TTrigger, TContext>
{
    public delegate void ActionType(ref TTrigger trigger, ref TContext context, Entity skill_entity,
                                    Entity       modifier_container);

    private ActionType _action;

    private TriggerActionMeta(string id, Entity default_modifier_container) : base(id, default_modifier_container)
    {
    }

    public void Invoke(ref TTrigger trigger, ref TContext context, Entity trigger_entity)
    {
        Entity skill_entity = trigger_entity.Parent;
        ref Entity skill_caster = ref skill_entity.GetComponent<SkillCaster>().value;
        Entity modifier_container = default_modifier_container;

        if (!skill_caster.IsNull)
            modifier_container = skill_caster.GetComponent<ActorBinder>().AE
                .GetSkillActionEntity(trigger.TriggerActionMeta.id, default_modifier_container);

        _action(ref trigger, ref context, skill_entity, modifier_container);
    }

    public class ActionBuilder
    {
        protected BuiltAction under_build_action;

        public ActionBuilder()
        {
            under_build_action = new BuiltAction();
        }

        public virtual BuiltAction Build()
        {
            return under_build_action;
        }
    }

    public class BuiltAction
    {
        internal ActionType       action;
        internal List<IComponent> modifiers = new();
    }

    public class MetaBuilder
    {
        private static readonly HashSet<string>                       _registries = new();
        private readonly        TriggerActionMeta<TTrigger, TContext> _under_build;

        public MetaBuilder(string id)
        {
            var actual_id = $"{typeof(TTrigger)}-{typeof(TContext)}.{id}";
            if (AllDict.ContainsKey(actual_id)) throw new DuplicateNameException(id);

            _under_build = new TriggerActionMeta<TTrigger, TContext>(actual_id, ModClass.I.SkillV2.World.CreateEntity(
                new ModifierContainerEntity(),
                Tags.Get<TagPrefab>()
            ));
        }

        public MetaBuilder AppendAction(ActionType action)
        {
            _under_build._action += action;
            return this;
        }

        public MetaBuilder AllowModifier<TModifier, TValue>(TModifier default_modifier)
            where TModifier : struct, IModifier<TValue>
        {
            _under_build.default_modifier_container.AddComponent(default_modifier);
            return this;
        }

        public MetaBuilder CombineWith(BuiltAction built_action)
        {
            _under_build._action += built_action.action;
            foreach (IComponent mod in built_action.modifiers)
                _under_build.default_modifier_container.AddNonGeneric(mod);

            return this;
        }

        public MetaBuilder CombineWith(TriggerActionMeta<TTrigger, TContext> another_action_meta,
                                       bool                                  overwrite_modifiers = false)
        {
            _under_build._action += another_action_meta._action;

            foreach (EntityComponent mod in another_action_meta.default_modifier_container.Components)
            {
#pragma warning disable CS0618
                if (overwrite_modifiers || !_under_build.default_modifier_container.HasComponent(mod.GetType()))
                    _under_build.default_modifier_container.AddNonGeneric(mod.Value);
#pragma warning restore CS0618
            }

            return this;
        }

        public TriggerActionMeta<TTrigger, TContext> Build()
        {
            dict.Add(_under_build.id, _under_build);
            return _under_build;
        }
    }
}