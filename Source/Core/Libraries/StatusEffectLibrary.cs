using Cultiway.Abstract;

namespace Cultiway.Core.Libraries;

public class StatusEffectLibrary : DynamicAssetLibrary<StatusEffectAsset>
{
    protected override void OnRemoveDynamic(StatusEffectAsset asset)
    {
        asset.DeletePrefab();
        base.OnRemoveDynamic(asset);
    }
}
