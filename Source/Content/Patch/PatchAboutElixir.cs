using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Content.Patch;

internal static class PatchAboutElixir
{
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.updateAge))]
    private static void updateAge_postfix(Actor __instance)
    {
        if (Toolbox.randomChance(XianSetting.TakenRestoreElixirProb))
        {
            var ae = __instance.GetExtend();
            var elixirs = ae.GetItems().Where(x => x.HasComponent<Elixir>() && x.Tags.Has<TagElixirRestore>()).ToList();
            if (elixirs.Any())
            {
                var elixir_to_consume = elixirs.GetRandom();
                ae.ConsumeElixir(elixir_to_consume);
            }
        }
    }
}