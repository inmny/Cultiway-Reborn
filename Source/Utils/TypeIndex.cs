using System;
using System.Collections.Generic;

namespace Cultiway.Utils;

public static class TypeIndex<TGroup, TType>
{
    public static readonly int Index = SingleTypeIndexUtils<TGroup>.GetIndex<TType>();
}

internal static class SingleTypeIndexUtils<TGroup>
{
    private static          int           next_idx;
    private static readonly HashSet<Type> _assigned_types = new();

    public static int GetIndex<TType>()
    {
        if (_assigned_types.Add(typeof(TType))) return next_idx++;

        return TypeIndex<TGroup, TType>.Index;
    }
}