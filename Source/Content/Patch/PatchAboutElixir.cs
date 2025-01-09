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
        if (Toolbox.randomChance(XianSetting.TakenRestoreElixirProb))
        {
            var ae = __instance.GetExtend();
            var elixirs = ae.GetItems().Where(x => x.HasComponent<Elixir>() && x.Tags.Has<TagElixirRestore>()).ToList();
            if (elixirs.Any())
            {
                var elixir_to_consume = elixirs.GetRandom();
                ae.TryConsumeElixir(elixir_to_consume);
            }
            else
            {
                var city = ae.Base.city;
                if (city != null)
                {
                    var elixirs_in_city = city.GetExtend().GetItems().Where(x =>
                            x.HasComponent<Elixir>() && x.Tags.Has<TagElixirRestore>())
                        .ToList();
                    if (elixirs_in_city.Any())
                    {
                        var elixir_to_consume = elixirs_in_city.GetRandom();
                        ae.TryConsumeElixir(elixir_to_consume);
                    }
                }
            }
        }
    }
    private static Tags data_gain_or_change = Tags.Get<TagElixirDataGain, TagElixirDataChange>();
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.updateAge))]
    private static void City_updateAge_postfix(City __instance)
    {
        if (Toolbox.randomChance(XianSetting.CityDistributeElixirProb))
        {
            var units = __instance.units.getSimpleList();
            if (units.Count == 0) return;
            var actor_to_consume = units.GetRandom();
            if (actor_to_consume == null || !actor_to_consume.isAlive()) return;
            var elixirs = __instance.GetExtend().GetItems().Where(x =>
                    x.HasComponent<Elixir>() && x.Tags.HasAny(data_gain_or_change))
                .ToList();
            if (elixirs.Any())
            {
                var elixir_to_consume = elixirs.GetRandom();
                actor_to_consume.GetExtend().TryConsumeElixir(elixir_to_consume);
            }
        }
    }
}