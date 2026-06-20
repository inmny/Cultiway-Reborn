using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class ComponentTools
{
    private const BindingFlags SerializedFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void CopyCompatibleSerializedFieldsTo(this Component source, Component target)
    {
        var source_fields = GetSerializedFields(source.GetType());
        foreach (var target_field in GetSerializedFields(target.GetType()).Values)
        {
            if (!source_fields.TryGetValue(target_field.Name, out var source_field))
            {
                continue;
            }

            if (!target_field.FieldType.IsAssignableFrom(source_field.FieldType))
            {
                continue;
            }

            target_field.SetValue(target, source_field.GetValue(source));
        }
    }

    public static void DisableSerializedObjectsMissingFrom(this Component source, Component target)
    {
        var target_fields = GetSerializedFields(target.GetType());
        foreach (var source_field in GetSerializedFields(source.GetType()).Values)
        {
            if (target_fields.TryGetValue(source_field.Name, out var target_field) &&
                target_field.FieldType.IsAssignableFrom(source_field.FieldType))
            {
                continue;
            }

            DisableSerializedObject(source_field.GetValue(source));
        }
    }

    public static void RequireCopiedSerializedField(this Component target, string field_name, string context, bool require_non_empty_array = false)
    {
        var field = FindField(target.GetType(), field_name)
                    ?? throw new InvalidOperationException($"{context} 缺少字段 {field_name}");
        var value = field.GetValue(target);
        if (value == null)
        {
            throw new InvalidOperationException($"{context} 字段 {field_name} 未复制");
        }

        if (require_non_empty_array && value is Array array && array.Length == 0)
        {
            throw new InvalidOperationException($"{context} 字段 {field_name} 为空数组");
        }
    }

    private static Dictionary<string, FieldInfo> GetSerializedFields(Type type)
    {
        var fields = new Dictionary<string, FieldInfo>();
        while (type != null && type != typeof(MonoBehaviour))
        {
            foreach (var field in type.GetFields(SerializedFieldFlags))
            {
                if (field.IsStatic || field.IsNotSerialized)
                {
                    continue;
                }

                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                {
                    continue;
                }

                fields[field.Name] = field;
            }

            type = type.BaseType;
        }

        return fields;
    }

    private static void DisableSerializedObject(object value)
    {
        if (value is Component component)
        {
            component.gameObject.SetActive(false);
            return;
        }

        if (value is GameObject game_object)
        {
            game_object.SetActive(false);
        }
    }

    private static FieldInfo FindField(Type type, string field_name)
    {
        while (type != null && type != typeof(MonoBehaviour))
        {
            var field = type.GetField(field_name, SerializedFieldFlags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }
}
