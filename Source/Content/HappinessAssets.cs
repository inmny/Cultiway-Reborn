using Cultiway.Abstract;
using strings;

namespace Cultiway.Content;

public class HappinessAssets : ExtendLibrary<HappinessAsset, HappinessAssets>
{
    public static HappinessAsset LevelUp { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        LevelUp.value = 100;
        LevelUp.pot_task_id = S_Task.happy_laughing;
        LevelUp.pot_amount = 3;
        LevelUp.path_icon = "ui/icons/iconKings";
    }

    protected override void PostInit(HappinessAsset asset)
    {
        base.PostInit(asset);
        asset.index = cached_library.list.IndexOf(asset);
    }
}