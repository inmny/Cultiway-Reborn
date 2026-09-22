// 诊断横幅越界刷屏：找出 backgrounds 为空的 BannerAsset 和触发越界的王国
var lib = AssetManager.kingdom_banners_library;
var emptyAssets = new System.Collections.Generic.List<string>();
var assetInfo = new System.Collections.Generic.List<string>();
foreach (var asset in lib.list)
{
    if (asset == null) continue;
    int bg = asset.backgrounds?.Count ?? -1;
    int ic = asset.icons?.Count ?? -1;
    assetInfo.Add(asset.id + " 背景=" + bg + " 图标=" + ic);
    if (bg == 0) emptyAssets.Add(asset.id);
}

var badKingdoms = new System.Collections.Generic.List<string>();
foreach (Kingdom k in World.world.kingdoms)
{
    if (k == null || k.isRekt()) continue;
    var actorAsset = k.getActorAsset();
    string bannerId = actorAsset?.banner_id;
    int bgId = k.data.banner_background_id;
    if (string.IsNullOrEmpty(bannerId)) bannerId = "(空)";
    var bannerAsset = AssetManager.kingdom_banners_library.get(bannerId);
    int bgCount = bannerAsset?.backgrounds?.Count ?? -99;
    if (bgCount == 0 || bgId < 0 || bgId >= bgCount)
        badKingdoms.Add(k.name + " id=" + k.id + " 种族资产=" + actorAsset?.id +
                        " banner=" + bannerId + " (背景数=" + bgCount + ") 存储索引=" + bgId);
}
return new {
    空背景资产 = emptyAssets,
    异常王国数量 = badKingdoms.Count,
    异常王国 = badKingdoms.Take(10),
    全部资产 = assetInfo.Take(30)
};
