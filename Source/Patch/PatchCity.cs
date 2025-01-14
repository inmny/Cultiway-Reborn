using System.Linq;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchCity
{
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.takeAllItemsFromActor))]
    private static void takeAllItemsFromActor_postfix(City __instance, Actor pActor)
    {
        var ae = pActor.GetExtend();
        var ce =__instance.GetExtend();
        using var pool = new ListPool<Entity>(ae.GetItems());
        foreach (var item in pool)
        {
            ce.AddSpecialItem(item);
        }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.destroyCity))]
    private static void destroyCity_postfix(City __instance)
    {
        ModClass.I.CityExtendManager.Destroy(__instance.data.id);
    }
}