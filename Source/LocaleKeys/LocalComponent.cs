using System;
using System.Reflection;
using NeoModLoader.General;

namespace Cultiway.LocaleKeys;

public class LocalComponent
{
    private string _component;

    private string         _key;
    private LocalComponent _parent;
    private string         _postfix;
    private string         _prefix;

    protected LocalComponent(string given_component)
    {
        _component = given_component;
        init_fields();
    }

    public LocalComponent()
    {
        init_fields();
    }

    public string Key
    {
        get
        {
            if (string.IsNullOrEmpty(_key))
                _key = _parent == null
                    ? $"{_prefix}{_component}{_postfix}"
                    : $"{_prefix}{_parent.Key}.{_component}{_postfix}";

            return _key;
        }
    }

    public string Value => LocalizedTextManager.stringExists(Key) ? LM.Get(Key) : "";

    private void init_fields()
    {
        if (GetType() == typeof(LocalComponent)) return;

        var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        ModClass.LogInfo("Start Initialize a local component");
        foreach (FieldInfo field in fields)
        {
            ModClass.LogInfo($"Initialize {field.Name}");
            if (typeof(LocalComponent).IsAssignableFrom(field.FieldType))
            {
                if (field.IsInitOnly) continue;

                var component = (LocalComponent)Activator.CreateInstance(field.FieldType);
                component._parent = this;
                var prefix_attr = field.GetCustomAttribute<PrefixAttribute>();
                if (prefix_attr != null) component._prefix = prefix_attr.Prefix;

                var postfix_attr = field.GetCustomAttribute<PostfixAttribute>();
                if (postfix_attr != null) component._postfix = postfix_attr.Postfix;

                var overwrite_attr = field.GetCustomAttribute<OverwriteComponentAttribute>();
                if (overwrite_attr != null)
                    component._component = overwrite_attr.Component;
                else
                    component._component = field.Name;

                field.SetValue(this, component);
            }
        }
    }
}