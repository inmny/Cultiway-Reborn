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
                dynamic_library.RecycleUnknownAssets();
        }
    }
}
