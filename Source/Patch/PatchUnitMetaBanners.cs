using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
    private static void AddBanners(UnitMetaBanners container)
    {
        if (container._banners.Any(x => x.banner.HasComponent<SectBanner>())) return;
        container._banners.Add(new MetaBannerElement()
        {
            banner = Object.Instantiate(SectBanner.Prefab, container._banners[0].banner.transform.parent),
            check = () => container.actor.HasSect(),
            nano = () => container.actor.GetExtend().sect,
        });
    }
}