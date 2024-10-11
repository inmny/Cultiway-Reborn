using Cultiway.UI;
using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

public class CustomMapModeLibrary : AssetLibrary<CustomMapModeAsset>
{
    public override void init()
    {
    }

    public override CustomMapModeAsset add(CustomMapModeAsset pAsset)
    {
        GodPower power = new GodPower()
        {
            id = pAsset.toggle_name,
            name = pAsset.toggle_name,
            unselectWhenWindow = true,
            map_modes_switch = true,
            toggle_name = pAsset.toggle_name,
            toggle_action = _ => ModClass.I.CustomMapModeManager.SetAllDirty()
        };
        AssetManager.powers.add(power);
        UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateToggleButton(pAsset.toggle_name, SpriteTextureLoader.getSprite(pAsset.icon_path)));
        return base.add(pAsset);
    }
}