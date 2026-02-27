using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using HarmonyLib;
using UnityEngine;
using Cultiway.Core.Pathfinding;

namespace Cultiway.Patch;

internal static class PatchMapBox
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MapBox), nameof(MapBox.applyAttack))]
    private static IEnumerable<CodeInstruction> applyAttack_transpiler(IEnumerable<CodeInstruction> codes, ILGenerator il)
    {
        var list = new List<CodeInstruction>(codes);

        var add_exp_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Callvirt && (x.operand as MemberInfo)?.Name == nameof(Actor.addExperience));

        CodeInstruction start_call_gethit = list[add_exp_idx + 1];
        var float_local = il.DeclareLocal(typeof(float));

        list[add_exp_idx + 2].operand = float_local.LocalIndex;
        list[add_exp_idx + 3].opcode = OpCodes.Nop;

        list.InsertRange(add_exp_idx + 1, new CodeInstruction[]
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AttackData), nameof(AttackData.initiator))),
            new(OpCodes.Ldloc_S, 5),
            new(OpCodes.Conv_R4),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AttackData), nameof(AttackData.attack_type))),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchMapBox), nameof(recalc_damage))),
            new(OpCodes.Stloc_S, float_local.LocalIndex)
        });

        start_call_gethit.MoveLabelsTo(list[add_exp_idx + 1]);

        return list;
    }
    private static float recalc_damage(BaseSimObject attacker, float damage, AttackType attack_type)
    {
        if (!attacker.isActor()) return damage;

        ElementComposition damage_composition = EnumUtils.DamageCompositionFromDamageType(attack_type);
        for (var i = 0; i < 8; i++)
            damage += (0.125f + damage_composition[i]) * attacker.stats[WorldboxGame.BaseStats.MasterStats[i]];

        return damage;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.finishMakingWorld))]
    private static void finishMakingWorld_postfix()
    {
        ModClass.I.TileExtendManager.FitNewWorld();
        EventSystemHub.Publish(new WorldGeneratedEvent
        {
            WorldSeedId = MapBox.current_world_seed_id,
            Width = MapBox.width,
            Height = MapBox.height
        });
    }
    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
    private static void clearWorld_postfix()
    {
        foreach (var library in AssetManager._instance._list)
        {
            if (library is IDynamicAssetLibrary dynamic_asset_library)
            {
                dynamic_asset_library.ClearDynamicAssets();
            }
        }
        PathFinder.Instance.Clear();
        ModClass.I.ActorExtendManager.Clear();
        ModClass.I.BookExtendManager.Clear();
    }
}
