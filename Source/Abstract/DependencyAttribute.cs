using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cultiway.Abstract;

public class DependencyAttribute(params Type[] types) : Attribute
{
    public ReadOnlyCollection<Type> Types { get; } = Array.AsReadOnly(types);

    public static List<Type> SortManagerTypes(List<Type> types)
    {
        var graph = new Dictionary<Type, List<Type>>();
        var in_degree = new Dictionary<Type, int>();

        foreach (Type type in types)
        {
            graph[type] = new List<Type>();
            in_degree[type] = 0;
        }

        foreach (Type type in types)
        {
            DependencyAttribute attribute = type.GetCustomAttributes(typeof(DependencyAttribute), false)
                .Cast<DependencyAttribute>()
                .FirstOrDefault();

            if (attribute == null) continue;
            foreach (Type dependency in attribute.Types)
            {
                if (!types.Contains(dependency)) continue;
                graph[dependency].Add(type);
                in_degree[type]++;
            }
        }

        var sorted = new List<Type>();
        var queue = new Queue<Type>(in_degree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (queue.Count > 0)
        {
            Type current = queue.Dequeue();
            sorted.Add(current);

            foreach (Type dependent in graph[current])
            {
                in_degree[dependent]--;
                if (in_degree[dependent] == 0) queue.Enqueue(dependent);
            }
        }

        if (sorted.Count != types.Count()) throw new InvalidOperationException("Circular dependency detected.");

        return sorted;
    }
}