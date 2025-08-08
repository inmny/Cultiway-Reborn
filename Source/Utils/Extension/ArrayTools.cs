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
    /// <summary>
    /// 将<paramref name="item"/>移动到<paramref name="target_pos_item"/>的位置，其他元素后移
    /// </summary>
    /// <param name="list"></param>
    /// <param name="item"></param>
    /// <param name="target_pos_item"></param>
    /// <typeparam name="T"></typeparam>
    public static void MoveTo<T>(this List<T> list, T item, T target_pos_item)
    {
        int index = list.IndexOf(item);
        list.RemoveAt( index);
        int target_index = list.IndexOf(target_pos_item);
        list.Insert(target_index, item);
    }
}