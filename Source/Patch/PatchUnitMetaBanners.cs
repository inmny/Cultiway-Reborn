using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchUnitMetaBanners
{
    [HarmonyTranspiler, HarmonyPatch(typeof(UnitMetaBanners), nameof(UnitMetaBanners.Awake))]
    private static IEnumerable<CodeInstruction> Awake_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Callvirt &&
            (x.operand as MethodInfo)?.Name == nameof(IBaseMetaBanners.enableClickAnimation)) - 1;
        list.InsertRange(insert_idx, new []
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchUnitMetaBanners), nameof(AddBanners)))
        });

        return list;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(ActorSelectedMetaBanners), nameof(ActorSelectedMetaBanners.update))]
    private static void update_prefix(ActorSelectedMetaBanners __instance)
    {
        AddBanners(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ActorSelectedMetaBanners), nameof(ActorSelectedMetaBanners.update))]
    private static void update_postfix(ActorSelectedMetaBanners __instance, Actor pActor)
    {
        __instance.GetComponent<GeoRegionUnitMetaBannerBinding>()?.CaptureCurrentState(pActor);
    }
    private static void AddBanners(UnitMetaBanners container)
    {
        if (container._banners.Count == 0) return;

        if (!HasBanner<SectBanner>(container))
        {
            container._banners.Add(new MetaBannerElement()
            {
                banner = Object.Instantiate(SectBanner.Prefab, container._banners[0].banner.transform.parent),
                check = () => GetSect(container) != null,
                nano = () => GetSect(container),
            });
        }

        MetaBannerElement geoRegionElement = FindBanner<GeoRegionBanner>(container);
        if (geoRegionElement == null)
        {
            geoRegionElement = new MetaBannerElement
            {
                banner = Object.Instantiate(GeoRegionBanner.Prefab, container._banners[0].banner.transform.parent),
                check = () => GetGeoRegion(container) != null,
                nano = () => GetGeoRegion(container),
            };
            container._banners.Add(geoRegionElement);
        }

        GeoRegionUnitMetaBannerBinding binding =
            container.GetComponent<GeoRegionUnitMetaBannerBinding>() ??
            container.gameObject.AddComponent<GeoRegionUnitMetaBannerBinding>();
        binding.Configure(container, geoRegionElement);
    }

    private static MetaBannerElement FindBanner<TBanner>(UnitMetaBanners container) where TBanner : Component
    {
        return container._banners.FirstOrDefault(x => x.banner != null && x.banner.HasComponent<TBanner>());
    }

    private static bool HasBanner<TBanner>(UnitMetaBanners container) where TBanner : Component
    {
        return container._banners.Any(x => x.banner != null && x.banner.HasComponent<TBanner>());
    }

    private static Sect GetSect(UnitMetaBanners container)
    {
        Actor actor = container.actor;
        if (actor == null || actor.isRekt()) return null;

        Sect sect = actor.GetExtend().sect;
        return sect == null || sect.isRekt() ? null : sect;
    }

    internal static Actor GetActor(UnitMetaBanners container)
    {
        Actor actor = container?.actor;
        return actor == null || actor.isRekt() ? null : actor;
    }

    internal static GeoRegion GetGeoRegion(UnitMetaBanners container)
    {
        Actor actor = GetActor(container);

        GeoRegion geoRegion = WorldboxGame.I?.GeoRegions?.GetPrimaryGeoRegionForTile(actor.current_tile);
        return geoRegion == null || geoRegion.isRekt() ? null : geoRegion;
    }
}
