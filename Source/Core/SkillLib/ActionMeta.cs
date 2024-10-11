using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Modifiers;
using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib;

/// <summary>
/// <para><paramref name="trigger"/>是触发器</para>
/// <para><paramref name="skill_entity"/>是触发的技能实体, 可以对它进行操作, 以及获取更多信息</para>
/// <para><paramref name="action_entity"/>是存储action实例的entity, 可以从中获取词条</para>
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TVal"></typeparam>
public delegate void TriggerAction<T, TVal>(ref T trigger, ref Entity skill_entity, ref Entity action_entity);

public class ActionMeta<T, TVal> where T : struct, ITriggerComponent<TVal>
{
    /// <summary>
    /// <para><paramref name="trigger"/>是触发器</para>
    /// <para><paramref name="skill_entity"/>是触发的技能实体, 可以对它进行操作, 以及获取更多信息</para>
    /// <para><paramref name="action_entity"/>是存储action实例的entity, 可以从中获取词条</para>
    /// </summary>
    public readonly TriggerAction<T, TVal> action;

    private ActionMeta(TriggerAction<T, TVal> action)
    {
        this.action = action;
    }

    public Entity DefaultActionContainer { get; private set; }

    public Entity NewEntity()
    {
        var entity = DefaultActionContainer.Store.CloneEntitySimply(DefaultActionContainer);

        entity.RemoveTag<PrefabTag>();

        return entity;
    }

    public class Builder<TActionContainerInfo> where TActionContainerInfo : struct, IActionContainerInfo<T, TVal>
    {
        private ActionMeta<T, TVal> _action_meta;

        public Builder(TriggerAction<T, TVal> action)
        {
            _action_meta = new(action);
            _action_meta.DefaultActionContainer = ModClass.I.Skill.NewPrefab();
            _action_meta.DefaultActionContainer.AddComponent(new TActionContainerInfo()
            {
                Meta = _action_meta
            });
        }

        public Builder<TActionContainerInfo> AllowModifier<TMod, TModVal>(TMod default_mod)
            where TMod : struct, IActionModifier<TModVal>
        {
            _action_meta.DefaultActionContainer.AddComponent(default_mod);
            return this;
        }

        public ActionMeta<T, TVal> Build()
        {
            return _action_meta;
        }
    }
}