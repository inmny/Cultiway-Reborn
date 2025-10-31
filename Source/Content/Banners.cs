using System;
using System.Linq;
using Cultiway.Abstract;

namespace Cultiway.Content;

public class Banners : ICanInit
{
    enum ElementType
    {
        Background,
        Frame,
        Icon
    }

    enum BannerType
    {
        Culture,
        Clan,
        Kingdom,
        Family,
        Language
    }
    public void Init()
    {
        Load("banners/general_icons", BannerType.Clan, ElementType.Icon);
    }

    private void Load(string folder, BannerType type, ElementType element)
    {
        var sprites = SpriteTextureLoader.getSpriteList(folder);
        
        var paths = sprites.Select(x => $"{folder}/{x.name}").ToArray();

        BannerAsset asset;
        switch (type)
        {
            case BannerType.Clan:
                asset = AssetManager.clan_banners_library.main;
                break;
            case BannerType.Culture:
                asset = AssetManager.culture_banners_library.main;
                break;
            case BannerType.Family:
                asset = AssetManager.family_banners_library.main;
                break;
            case BannerType.Kingdom:
                asset = AssetManager.kingdom_banners_library.main;
                break;
            case BannerType.Language:
                asset = AssetManager.language_banners_library.main;
                break;
            default:
                asset = null;
                break;
        }

        if (asset == null)
        {
            return;
        }

        switch (element)
        {
            case ElementType.Icon:
                asset.icons.AddRange(paths);
                break;
            case ElementType.Background:
                asset.backgrounds.AddRange(paths);
                break;
            case ElementType.Frame:
                asset.frames.AddRange(paths);
                break;
        }
    }
}