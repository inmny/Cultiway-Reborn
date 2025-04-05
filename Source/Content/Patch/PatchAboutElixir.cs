using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Patch;

internal static class PatchAboutElixir
{
    [Hotfixable]
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.updateAge))]
    private static void Actor_updateAge_postfix(Actor __instance)
    {
        if (Randy.randomChance(XianSetting.TakenElixirProb))
        {
            var ae = __instance.GetExtend();
            var elixir_to_consume = ae.GetRandomSpecialItem(x=>x.HasComponent<Elixir>() && !x.Tags.Has<TagElixirStatusGain>());
            if (!elixir_to_consume.self.IsNull)
            {
                ae.TryConsumeElixir(elixir_to_consume.self);
            }
        }
    }
}