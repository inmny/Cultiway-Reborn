using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using HarmonyLib;
using NeoModLoader.General.Game.extensions;

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
        Load("banners/general_icons", BannerType.Kingdom, ElementType.Icon);

        new Harmony("Cultiway.Banners.KingdomPatch").Patch(
            AccessTools.Method(typeof(KingdomBannerLibrary), nameof(KingdomBannerLibrary.loadNewAssetRuntime)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(Banners), nameof(Postfix))));
    }

    private static Dictionary<ElementType, List<string>> appended = new Dictionary<ElementType, List<string>>();
    private static void Postfix(KingdomBannerLibrary __instance, BannerAsset __result)
    {
        if (appended.TryGetValue(ElementType.Icon, out var icons))
        {
            __result.icons?.AddRange(icons);
        }

        if (appended.TryGetValue(ElementType.Background, out var backgrounds))
        {
            __result.backgrounds?.AddRange(backgrounds);
        }
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

        if (type == BannerType.Kingdom)
        {
            switch (element)
            {
                case ElementType.Icon:
                    if (!appended.TryGetValue(ElementType.Icon, out var icons))
                    {
                        icons = new List<string>();
                        appended.Add(ElementType.Icon, icons);
                    }
                    icons.AddRange(paths);
                    break;
                case ElementType.Background:
                    if (!appended.TryGetValue(ElementType.Background, out var backgrounds))
                    {
                        backgrounds = new List<string>();
                        appended.Add(ElementType.Background, backgrounds);
                    }
                    backgrounds.AddRange(paths);
                    break;
                case ElementType.Frame:
                    if (!appended.TryGetValue(ElementType.Frame, out var frames))
                    {
                        frames = new List<string>();
                        appended.Add(ElementType.Frame, frames);
                    }
                    frames.AddRange(paths);
                    break;
            }
            return;
        }

        if (asset == null)
        {
            return;
        }
        switch (element)
        {
            case ElementType.Icon:
                asset.icons?.AddRange(paths);
                break;
            case ElementType.Background:
                asset.backgrounds?.AddRange(paths);
                break;
            case ElementType.Frame:
                asset.frames?.AddRange(paths);
                break;
        }
    }
}