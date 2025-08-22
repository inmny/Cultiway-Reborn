using Cultiway.Abstract;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class RecycleUnknownAssetsSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        foreach (var library in AssetManager._instance._list)
        {
            if (library is IDynamicAssetLibrary dynamic_library)
            {
                using var list_to_remove = new ListPool<string>();
                foreach (var asset in dynamic_library.GetDynamicAssets())
                {
                    if (asset is IDeleteWhenUnknown delete_when_unknown)
                    {
                        if (delete_when_unknown.Current <= 0)
                        {
                            list_to_remove.Add(asset.id);
                        }
                    }
                }
                dynamic_library.RemoveAll(list_to_remove);
            }
        }
    }
}