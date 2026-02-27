using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class BookExtend : ExtendComponent<Book>, IDisposable
{
    private Entity e;
    public override Entity E => e;
    public override Book Base => e.HasComponent<BookBinder>() ? e.GetComponent<BookBinder>().Book : null;


    private Dictionary<Type, Dictionary<IDeleteWhenUnknown, float>>  _master_items = new();
    public void Master<T>(T item, float value) where T : Asset, IDeleteWhenUnknown
    {
        if (!_master_items.TryGetValue(typeof(T), out var dict))
        {
            dict = new Dictionary<IDeleteWhenUnknown, float>();
            _master_items.Add(typeof(T), dict);
        }

        if (!dict.ContainsKey(item))
        {
            item.Current++;
        }
        dict[item] = value;
    }

    public bool HasMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) && dict.Count > 0;
    }

    public float GetMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) ? (dict.TryGetValue(item, out var value) ? value : 0) : 0;
    }

    public IEnumerable<(T, float)> GetAllMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) ? dict.Select(x => ((T)x.Key, x.Value)) : Array.Empty<(T, float)>();
    }
    public BookExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<BookBinder>()._be = this;
    }

    public void Dispose()
    {
        if (!e.IsNull)
        {
            e.AddTag<TagRecycle>();
            ModClass.LogInfo($"Disposing BookExtend for Book {Base.data.id} ({e})");
            ModClass.I.BookExtendManager.Remove(Base);
        }

        if (_master_items != null)
        {
            foreach (var items in _master_items.Values)
            {
                if (items != null)
                {
                    foreach (var item in items.Keys)
                    {
                        item.Current--;
                    }
                }
            }

            _master_items = null;
        }
    }
}