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
            var elixir_to_consume = ae.GetRandomSpecialItem(x=>x.HasComponent<Elixir>() && x.Tags.Has<TagElixirRestore>());
            if (!elixir_to_consume.self.IsNull)
            {
                ae.TryConsumeElixir(elixir_to_consume.self);
            }
            else
            {
                var city = ae.Base.city;
                if (city != null)
                {
                    elixir_to_consume = city.GetExtend().GetRandomSpecialItem(x=>x.HasComponent<Elixir>() && x.Tags.Has<TagElixirRestore>());
                    if (!elixir_to_consume.self.IsNull)
                    {
                        ae.TryConsumeElixir(elixir_to_consume.self);
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
            var elixir_to_consume = __instance.GetExtend().GetRandomSpecialItem(x=>x.HasComponent<Elixir>() && x.Tags.HasAny(data_gain_or_change));
            if (!elixir_to_consume.self.IsNull)
            {
                actor_to_consume.GetExtend().TryConsumeElixir(elixir_to_consume.self);
            }
        }
    }
}