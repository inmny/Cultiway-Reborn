using System.Collections.Generic;
using Cultiway.Content;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Patch;

/// <summary>
/// 元神王国的横幅背景解析。
/// 王国人口元神化后 getActorAsset() 返回无横幅资源的元神种族，
/// 原版背景渲染会在空列表上越界。前缀只在该情况下降级接管：
/// 按成员记忆投票选出原肉身种族的横幅（多数派），无记忆时回落
/// 开国种族，再回落人类横幅；正常种族完全走原版路径。
/// </summary>
[HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getElementBackground))]
internal class PatchKingdomBannerBackground
{
    /// <summary>聚合结果的重新统计间隔（世界秒），避免名牌渲染每帧遍历成员。</summary>
    private const float ReaggregateInterval = 5f;

    private static readonly Dictionary<long, CachedBackground> Cache = new();

    private struct CachedBackground
    {
        public Sprite Sprite;
        public double NextCheckAt;
    }

    public static bool Prefix(Kingdom __instance, ref Sprite __result)
    {
        var actorAsset = __instance.getActorAsset();
        if (actorAsset == null || !string.IsNullOrEmpty(actorAsset.banner_id)) return true;

        double now = World.world.getCurWorldTime();
        if (Cache.TryGetValue(__instance.getID(), out var cached) && now < cached.NextCheckAt)
        {
            __result = cached.Sprite;
            return false;
        }

        var sprite = ResolveBackground(__instance);
        if (sprite == null) return true;
        Cache[__instance.getID()] = new CachedBackground { Sprite = sprite, NextCheckAt = now + ReaggregateInterval };
        __result = sprite;
        return false;
    }

    /// <summary>按"成员记忆多数派 → 开国种族 → 人类横幅"的顺序解析背景贴图。</summary>
    private static Sprite ResolveBackground(Kingdom kingdom)
    {
        BannerAsset banner = null;

        var tally = new Dictionary<string, int>();
        foreach (Actor unit in kingdom.getUnits())
        {
            if (unit == null || unit.isRekt()) continue;
            if (!YuanshenOriginBannerStore.TryGet(unit.getID(), out var originAssetId)) continue;
            tally.TryGetValue(originAssetId, out var count);
            tally[originAssetId] = count + 1;
        }
        string bestAssetId = null;
        int bestCount = 0;
        foreach (var pair in tally)
        {
            if (pair.Value <= bestCount) continue;
            bestCount = pair.Value;
            bestAssetId = pair.Key;
        }
        if (bestAssetId != null)
        {
            banner = GetBannerOfAsset(AssetManager.actor_library.get(bestAssetId));
        }

        if (banner == null)
        {
            banner = GetBannerOfAsset(kingdom.getFounderSpecies());
        }

        if (banner == null)
        {
            banner = AssetManager.kingdom_banners_library.main;
        }

        if (banner?.backgrounds == null || banner.backgrounds.Count == 0) return null;
        int index = Mathf.Clamp(kingdom.data.banner_background_id, 0, banner.backgrounds.Count - 1);
        return SpriteTextureLoader.getSprite(banner.backgrounds[index]);
    }

    /// <summary>解析种族资产的横幅资产；种族没有横幅或横幅背景为空时返回空。</summary>
    private static BannerAsset GetBannerOfAsset(ActorAsset actorAsset)
    {
        var bannerId = actorAsset?.banner_id;
        if (string.IsNullOrEmpty(bannerId)) return null;
        var banner = AssetManager.kingdom_banners_library.get(bannerId);
        return banner?.backgrounds is { Count: > 0 } ? banner : null;
    }
}
