using Cultiway.Abstract;
using strings;

namespace Cultiway.Content;

public class PlotCategories : ExtendLibrary<PlotCategoryAsset, PlotCategories>
{
    public static PlotCategoryAsset Sect { get; private set; }
    [GetOnly(S_PlotGroup.plots_others)]
    public static PlotCategoryAsset Others {get; private set;}
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        Sect.color = "#E4A857";
    }

    protected override PlotCategoryAsset Add(PlotCategoryAsset asset)
    {
        asset.name = $"plot_group_{asset.id}";
        return base.Add(asset);
    }
}