using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Cultiway.Abstract;

public abstract class ExtendLibrary<TAsset, T> : ICanInit, ICanReload
    where TAsset : Asset where T : ExtendLibrary<TAsset, T>
{
    private   List<TAsset>               _assets_added = new();
    protected ReadOnlyCollection<TAsset> assets_added;
    protected AssetLibrary<TAsset>       cached_library;
    protected TAsset                     t;

    protected ExtendLibrary()
    {
        cached_library =
            AssetManager.instance.list.Find(x => x is (AssetLibrary<TAsset>)) as AssetLibrary<TAsset>;
        assets_added = _assets_added.AsReadOnly();
    }

    public void Init()
    {
        OnInit();
        foreach (TAsset asset in assets_added) PostInit(asset);
    }

    public virtual void OnReload()
    {
    }

    protected virtual void ActionAfterCreation(PropertyInfo prop, TAsset asset)
    {
        
    }
    protected void RegisterAssets(string prefix = "Cultiway")
    {
        if (typeof(TAsset).GetConstructors().All(x => x.GetParameters().Length > 0)) return;

        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        foreach (PropertyInfo prop in props)
            if (prop.PropertyType == typeof(TAsset))
            {
                TAsset item;
                var get_only_attr = prop.GetCustomAttribute<GetOnlyAttribute>();
                if (get_only_attr != null)
                {
                    item = Get(string.IsNullOrEmpty(get_only_attr.SourceID) ? prop.Name : get_only_attr.SourceID);
                }
                else
                {
                    var item_id = $"{prefix}.{prop.Name}";
                    var asset_id_attr = prop.GetCustomAttribute<AssetIdAttribute>();
                    if (asset_id_attr != null && !string.IsNullOrEmpty(asset_id_attr.Id)) item_id = asset_id_attr.Id;

                    var clone_source_attr = prop.GetCustomAttribute<CloneSourceAttribute>();
                    if (clone_source_attr != null)
                    {
                        item = Clone(item_id, clone_source_attr.clone_source_id);
                    }
                    else
                    {
                        item = Activator.CreateInstance<TAsset>();
                        item.id = item_id;
                        ActionAfterCreation(prop, item);
                        item = Add(item);
                    }

                    ModClass.LogInfo($"({typeof(T).Name}) Initializes {item_id}");
                }

                prop.SetValue(null, item);
            }
    }

    protected abstract void OnInit();

    protected virtual TAsset Add(TAsset asset)
    {
        t = cached_library.add(asset);
        _assets_added.Add(t);
        return t;
    }

    protected virtual void PostInit(TAsset asset)
    {
    }

    protected virtual TAsset Clone(string new_id, string from_id)
    {
        t = cached_library.clone(new_id, from_id);
        _assets_added.Add(t);
        return t;
    }

    public TAsset Get(string id)
    {
        return cached_library.get(id);
    }
}