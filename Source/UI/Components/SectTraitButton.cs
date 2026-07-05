using Cultiway.Core.Libraries;

namespace Cultiway.UI.Components;

public class SectTraitButton : TraitButton<SectTrait>
{
    public override string tooltip_type => WorldboxGame.Tooltips.SectTrait.id;

    public override void load(string pTraitID)
    {
        load(ModClass.L.SectTraitLibrary.get(pTraitID));
    }

    public override TooltipData tooltipDataBuilder()
    {
        return new TooltipData
        {
            custom_data_string = new CustomDataContainer<string>
            {
                ["sect_trait"] = augmentation_asset.id
            }
        };
    }
}
