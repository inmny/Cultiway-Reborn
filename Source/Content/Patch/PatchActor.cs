using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using Cultiway.Abstract;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using strings;
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
            var cultivate_method = ae.GetMainCultibook()?.GetCultivateMethod() ?? CultivateMethods.Standard;
            var can_cultivate = cultivate_method.CanCultivate?.Invoke(ae) ?? true;
            
            if (can_cultivate && pActor.hasTrait(ActorTraits.Cultivator.id)) chance = 0.8f;

            if (!can_cultivate)
            {
                chance = 0f;
            }
            if (Randy.randomChance(1 - chance))
            {
                if (Randy.randomChance(0.6f))
                {
                    using var pool = new ListPool<string>();
                    if (Randy.randomChance(pActor.hasTrait(ActorTraits.OpenSource) ? 0.5f : 0.1f))
                    {
                        pool.Add(ActorJobs.BookWriter.id);
                    }
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
                    
                    // ========== 师徒系统工作添加到pool ==========
                    // 1. 师傅工作：元婴期及以上才会主动收徒和教导弟子
                    if (xian.CurrLevel >= XianLevels.Yuanying)
                    {
                        var apprentices = ae.GetApprentices();
                        // 如果有弟子，或者可以收徒，添加到pool
                        if (apprentices.Count > 0 || ae.CanRecruit())
                        {
                            // 有弟子的师傅更倾向于执行师傅工作
                            float masterJobChance = apprentices.Count > 0 ? 0.8f : 0.5f;
                            if (Randy.randomChance(masterJobChance))
                            {
                                pool.Add(ActorJobs.MasterDuty.id);
                            }
                        }
                    }
                    
                    // 2. 弟子工作：有师傅的角色
                    // 弟子有一定概率执行弟子工作（跟随师傅、寻师等）
                    if (Randy.randomChance(0.3f)) // 30%概率添加到pool
                    {
                        pool.Add(ActorJobs.ApprenticeDuty.id);
                    }
                    
                    if (pool.Any())
                    {
                        __result = pool.GetRandom();
                    }
                }

                return;
            }

            __result = cultivate_method.GetBehaviourJobId?.Invoke(ae);
            if (string.IsNullOrEmpty(__result))
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

        List<string> param = new();
        var type = "修士";
        if (LM.Has(dead_ae.Base.asset.name_locale))
        {
            type = LM.Get(dead_ae.Base.asset.name_locale);
        }
        param.Add(type);
        ElementRoot? element_root_component = null;
        ItemIconData? icon_data_component = null;
        if (dead_ae.TryGetComponent(out ElementRoot er))
        {
            element_root_component = er;

            var color = ColorUtils.FromElement(er.Iron, er.Wood, er.Water, er.Fire, er.Earth, er.Neg, er.Pos, er.Entropy);
            icon_data_component = new ItemIconData()
            {
                ColorHex1 = Toolbox.colorToHex(color)
            };
            param.Add(er.Type.GetName());
        }
        XianBase? xian_base_component = null;
        if (dead_ae.TryGetComponent(out XianBase xian_base))
        {
            xian_base_component = xian_base;
        }
        Jindan? jindan_component = null;
        if (dead_ae.TryGetComponent(out Jindan jindan))
        {
            jindan_component = jindan;
            param.Add(jindan.Type.GetName());
        }
        var shape_key = IngredientShapeGenerator.Instance.GenerateName(param.ToArray());
        var shape_asset = ModClass.L.ItemShapeLibrary.GetOrDefault(shape_key, ItemShapes.Ball);
        var shape_name = LM.Has(shape_asset.id) ? LM.Get(shape_asset.id) : shape_asset.id;
        param.Add(shape_name);

        SpecialItemUtils.Builder item_builder =
            SpecialItemUtils.StartBuild(shape_asset.id, __instance.data.created_time, __instance.getName(),
                Mathf.Pow(10, Mathf.Min(dead_ae.GetPowerLevel(), 6)));

        item_builder.AddTag<TagIngredient>();
        if (element_root_component.HasValue)
        {
            item_builder.AddComponent(element_root_component.Value);
        }
        if (icon_data_component.HasValue)
        {
            item_builder.AddComponent(icon_data_component.Value);
        }
        if (xian_base_component.HasValue)
        {
            item_builder.AddComponent(xian_base_component.Value);
        }
        if (jindan_component.HasValue)
        {
            item_builder.AddComponent(jindan_component.Value);
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
