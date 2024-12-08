using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Cultiway.Abstract;

namespace Cultiway.Utils.Extension;

public static class FunctionalInterfaceTools
{
    public static T DeepCopy<T>(this T obj) where T : ICanCopy, new()
    {
        return DeepCopier<T>.Copy(obj, true);
    }

    private static class DeepCopier<T>
    {
        private static Func<T, bool, T> cache;

        private static Func<T, bool, T> GetFunc(bool check_ignored)
        {
            ParameterExpression parameter_expression = Expression.Parameter(typeof(T),    "obj");
            ParameterExpression check_ignored_fields = Expression.Parameter(typeof(bool), "check_ignored_fields");
            var member_binding_list = new List<MemberBinding>();

            foreach (FieldInfo item in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                           BindingFlags.NonPublic))
            {
                if (check_ignored && item.GetCustomAttribute<IgnoreDeepCopyAttribute>() != null) continue;
                MemberExpression field = Expression.Field(parameter_expression, item);
                member_binding_list.Add(Expression.Bind(item, field));
            }

            MemberInitExpression member_init_expression =
                Expression.MemberInit(Expression.New(typeof(T)), member_binding_list.ToArray());
            var lambda = Expression.Lambda<Func<T, bool, T>>(
                member_init_expression, parameter_expression, check_ignored_fields);

            return lambda.Compile();
        }

        public static T Copy(T obj, bool check_ignored = false)
        {
            if (obj is string || typeof(T).IsValueType) return obj;
            cache ??= GetFunc(check_ignored);
            return cache(obj, check_ignored);
        }
    }
}