using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Patch;

internal static class PatchActor
{
    [HarmonyPostfix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.nextJobActor))]
    private static void nextJobActor_postfix(ref string __result, ActorBase pActor)
    {
        if (pActor.asset.unit && pActor.city != null && !pActor.isProfession(UnitProfession.Warrior))
        {
            var chance = 0.2f;
            if (pActor.hasTrait(ActorTraits.Cultivator.id)) chance = 0.8f;

            var ae = (pActor as Actor).GetExtend();

            if (Toolbox.randomChance(1 - chance))
            {
                if (ae.HasComponent<Jindan>() && Toolbox.randomChance(0.4f)) __result = ActorJobs.ElixirCrafter.id;

                return;
            }

            if (ae.HasCultisys<Xian>())
            {
                __result = ActorJobs.XianCultivator.id;
            }
        }
    }

    [Hotfixable]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.killHimself))]
    private static void killHimself_postfix(Actor __instance)
    {
        if (!__instance.isAlive()) return;
        ActorExtend dead_ae = __instance.GetExtend();
        if (!dead_ae.HasCultisys<Xian>()) return;
        if (!dead_ae.HasComponent<XianBase>()) return;
        BaseSimObject receiver = __instance.attackedBy ?? __instance;
        if (receiver.city == null) return;
        CityExtend ce = receiver.city.GetExtend();

        SpecialItemUtils.Builder item_builder =
            SpecialItemUtils.StartBuild(ItemShapes.Ball.id, __instance.data.created_time, __instance.getName());
        if (dead_ae.HasComponent<Jindan>()) item_builder.AddComponent(dead_ae.GetComponent<Jindan>());
        if (dead_ae.HasComponent<XianBase>()) item_builder.AddComponent(dead_ae.GetComponent<XianBase>());
        if (dead_ae.HasComponent<ElementRoot>()) item_builder.AddComponent(dead_ae.GetComponent<ElementRoot>());

        ce.AddSpecialItem(item_builder.Build());
    }

}