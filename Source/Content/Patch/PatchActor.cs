using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using Cultiway.Abstract;
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
    [Hotfixable, HarmonyPostfix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.nextJobActor))]
    private static void nextJobActor_postfix(ref string __result, ActorBase pActor)
    {
        if (pActor.asset == Actors.ConstraintSpirit)
        {
            pActor.data.get(ContentActorDataKeys.ConstraintSpiritJob_string, out __result);
            return;
        }
        if (pActor.asset.unit && pActor.city != null && !pActor.isProfession(UnitProfession.Warrior))
        {
            var chance = 0.2f;
            if (pActor.hasTrait(ActorTraits.Cultivator.id)) chance = 0.8f;

            var ae = (pActor as Actor).GetExtend();

            if (Toolbox.randomChance(1 - chance))
            {
                if (Toolbox.randomChance(0.6f))
                {
                    using var pool = new ListPool<string>();
                    if (ae.HasComponent<Jindan>()) pool.Add(ActorJobs.ElixirCrafter.id);
                    if (ae.HasComponent<XianBase>()) pool.Add(ActorJobs.TalismanCrafter.id);
                    if (pool.Any())
                    {
                        __result = pool.GetRandom();
                    }
                }

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
        var tile_pos = __instance.currentTile.pos;
        DirtyWakanMap.I.map[tile_pos.x, tile_pos.y] += 100;
        
        if (!dead_ae.HasCultisys<Xian>()) return;
        if (!dead_ae.HasComponent<XianBase>()) return;

        IHasInventory receiver = null;
        if (__instance.attackedBy != null)
        {
            if (__instance.attackedBy.isActor())
            {
                receiver = __instance.attackedBy.a.GetExtend();
            }
            else if (__instance.attackedBy.city != null)
            {
                receiver = __instance.attackedBy.city.GetExtend();
            }
        }
        else if (__instance.city != null)
        {
            receiver = __instance.city.GetExtend();
        }
        if (receiver == null) return;

        SpecialItemUtils.Builder item_builder =
            SpecialItemUtils.StartBuild(ItemShapes.Ball.id, __instance.data.created_time, __instance.getName(),
                Mathf.Pow(10, Mathf.Min(dead_ae.GetPowerLevel(), 6)));
        if (dead_ae.HasComponent<Jindan>()) item_builder.AddComponent(dead_ae.GetComponent<Jindan>());
        if (dead_ae.HasComponent<XianBase>()) item_builder.AddComponent(dead_ae.GetComponent<XianBase>());
        if (dead_ae.HasComponent<ElementRoot>()) item_builder.AddComponent(dead_ae.GetComponent<ElementRoot>());

        receiver.AddSpecialItem(item_builder.Build());
    }

}