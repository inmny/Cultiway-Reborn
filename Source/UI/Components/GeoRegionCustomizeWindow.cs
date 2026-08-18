using Cultiway;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;

/// <summary>地区外观自定义窗口，玩家可在这里查看和更换当前地区的旗帜图案。</summary>
public class GeoRegionCustomizeWindow : GenericCustomizeWindow<GeoRegion, GeoRegionData, GeoRegionBanner>
{
	/// <summary>声明该窗口编辑的是地理区域。</summary>
	public override MetaType meta_type
	{
		get
		{
			return MetaTypeExtend.GeoRegion.Back();
		}
	}
	/// <summary>当前由玩家选中并准备修改外观的地区。</summary>
	public override GeoRegion meta_object
	{
		get
		{
			return WorldboxGame.I.SelectedGeoRegion;
		}
	}

	/// <summary>玩家切换旗帜选项后，立即更新背景和图标预览。</summary>
	public override void onBannerChange()
	{
		this.image_banner_option_1.sprite = this.meta_object.getBannerBackground();
		this.image_banner_option_2.sprite = this.meta_object.getBannerIcon();
	}

	/// <summary>准备窗口顶部图像，隐藏国家专用装饰并显示地区图标。</summary>
	public override void setupImages()
	{
		this.icon_banner.SetActiveIfPresent(false);
		this.icon_top.sprite = SpriteTextureLoader.getSprite("cultiway/icons/iconGeoRegion");
	}

	/// <summary>创建地区外观自定义窗口。</summary>
	public GeoRegionCustomizeWindow()
	{
	}
}
