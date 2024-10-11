using System;
using System.Collections.Generic;

namespace Cultiway.Utils.Extension;

public static class ArrayTools
{
    public static T[] Sorted<T>(this T[] array, IComparer<T> comparer)
    {
        T[] a = new T[array.Length];
        array.CopyTo(a, 0);
        Array.Sort(a, comparer);
        return a;
    }
}