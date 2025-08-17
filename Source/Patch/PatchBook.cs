using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchBook
{
    [HarmonyPostfix, HarmonyPatch(typeof(Book), nameof(Book.newBook))]
    private static void newBook_postfix(Book __instance, BookTypeAsset pBookType)
    {
        var bte = pBookType.GetExtend<BookTypeAssetExtend>();
        if (!string.IsNullOrEmpty(bte.custom_cover_name))
        {
            __instance.data.path_cover = GetCustomCoverPath(bte.custom_cover_name);
        }
    }

    private static string GetCustomCoverPath(string custom_cover_name)
    {
        if (!_cache_custom_covers.TryGetValue(custom_cover_name, out var names))
        {
            names = SpriteTextureLoader.getSpriteList($"books/custom_book_covers/{custom_cover_name}")
                .Select(x => x.name).ToList();
            _cache_custom_covers.Add(custom_cover_name, names);
        }
        return $"../custom_book_covers/{custom_cover_name}/{names.GetRandom()}";
    }
    private static Dictionary<string, List<string>> _cache_custom_covers = new();
    [HarmonyPostfix, HarmonyPatch(typeof(BehFinishReading), nameof(BehFinishReading.checkBookAssetAction))]
    private static void onRead_postfix(Actor pActor, Book pBook)
    {
        var bt_asset = pBook.getAsset();
        var bte = bt_asset.GetExtend<BookTypeAssetExtend>();
        bte.instance_read_action?.Invoke(pActor, pBook, bt_asset);
    }
}