using Cultiway;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;

public class GeoRegionCustomizeWindow : GenericCustomizeWindow<GeoRegion, GeoRegionData, GeoRegionBanner>
{
	public override MetaType meta_type
	{
		get
		{
			return MetaTypeExtend.GeoRegion.Back();
		}
	}
	public override GeoRegion meta_object
	{
		get
		{
			return WorldboxGame.I.SelectedGeoRegion;
		}
	}

	public override void onBannerChange()
	{
		this.meta_object.getActorAsset();
		this.image_banner_option_1.sprite = this.meta_object.getBannerBackground();
		this.image_banner_option_2.sprite = this.meta_object.getBannerIcon();
	}
	public GeoRegionCustomizeWindow()
	{
	}
}