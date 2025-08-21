using Cultiway.Abstract;

namespace Cultiway.Content;

public class PlotCategories : ExtendLibrary<PlotCategoryAsset, PlotCategories>
{
    public static PlotCategoryAsset Sect { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        Sect.color = "#E4A857";
    }

    protected override PlotCategoryAsset Add(PlotCategoryAsset asset)
    {
        asset.name = $"plot_group_{asset.id}";
        return base.Add(asset);
    }
}