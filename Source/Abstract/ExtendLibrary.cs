using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    }

    public virtual void OnReload()
    {
    }

    protected abstract void OnInit();

    protected virtual TAsset Add(TAsset asset)
    {
        t = cached_library.add(asset);
        return t;
    }

    protected virtual TAsset Clone(string new_id, string from_id)
    {
        t = cached_library.clone(new_id, from_id);
        return t;
    }
}