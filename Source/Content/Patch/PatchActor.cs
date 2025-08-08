using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using Cultiway.Abstract;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Patch;

internal static class PatchActor
{
    [Hotfixable, HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.nextJobActor))]
    private static void nextJobActor_postfix(ref string __result, Actor pActor)
    {/*
        if (pActor.asset == Actors.ConstraintSpirit)
        {
            pActor.data.get(ContentActorDataKeys.ConstraintSpiritJob_string, out __result);
            pActor.data.get(ContentActorDataKeys.ConstraintSpiritCitizenJob_string, out var citizen_job_id, "");
            if (!string.IsNullOrEmpty(citizen_job_id))
            {
                var citizen_job = AssetManager.citizen_job_library.get(citizen_job_id);
                if (citizen_job != null)
                {
                    if (pActor.city != null)
                    {
                        pActor.city.jobs.takeJob(citizen_job);
                    }
                    pActor.citizen_job = citizen_job;
                }
            }
            return;
        }*/
        if (pActor.isSapient() && pActor.city != null && !pActor.isProfession(UnitProfession.Warrior))
        {
            var ae = (pActor as Actor).GetExtend();
            if (!ae.TryGetComponent(out Xian xian)) return;
            
            var chance = 0.2f;
            if (pActor.hasTrait(ActorTraits.Cultivator.id)) chance = 0.8f;

            if (Randy.randomChance(1 - chance))
            {
                if (Randy.randomChance(0.6f))
                {
                    using var pool = new ListPool<string>();
                    if (xian.CurrLevel >= XianLevels.Jindan)
                    {
                        pool.Add(ActorJobs.ElixirCrafter.id);
                        if (Randy.randomChance(0.9f))
                        {
                            pool.Add(ActorJobs.ElixirFinder.id);
                        }
                    }
                    if (xian.CurrLevel >= XianLevels.XianBase) pool.Add(ActorJobs.TalismanCrafter.id);
                    if (xian.CurrLevel >= XianLevels.Yuanying) pool.Add(ActorJobs.CultibookResearcher.id);
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
    [HarmonyPatch(typeof(Actor), nameof(Actor.die))]
    private static void killHimself_prefix(Actor __instance)
    {
        if (!__instance.isAlive()) return;
        ActorExtend dead_ae = __instance.GetExtend();
        var tile_pos = __instance.current_tile.pos;
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
            else if (__instance.attackedBy.hasCity())
            {
                receiver = __instance.attackedBy.getCity().GetExtend();
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

        item_builder.AddTag<TagIngredient>();
        List<string> param = new();
        var type = "修士";
        if (LM.Has(dead_ae.Base.asset.name_locale))
        {
            type = LM.Get(dead_ae.Base.asset.name_locale);
        }
        param.Add(type);
        if (dead_ae.TryGetComponent(out ElementRoot er))
        {
            item_builder.AddComponent(er);
            param.Add(er.Type.GetName());
        }
        if (dead_ae.TryGetComponent(out XianBase xian_base))
        {
            item_builder.AddComponent(xian_base);
        }
        if (dead_ae.TryGetComponent(out Jindan jindan))
        {
            item_builder.AddComponent(jindan);
            param.Add(jindan.Type.GetName());
        }
        if (dead_ae.Base.asset == Actors.Plant)
        {
            item_builder.AddComponent(new EntityName(dead_ae.Base.getName()));
        }
        else
        {

            item_builder.AddComponent(new EntityName(IngredientNameGenerator.Instance.GenerateName(param.ToArray())));
        }

        receiver.AddSpecialItem(item_builder.Build());
    }

}