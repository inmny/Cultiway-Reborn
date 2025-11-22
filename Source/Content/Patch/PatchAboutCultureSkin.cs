using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Cultiway.Content.Const;
using HarmonyLib;

namespace Cultiway.Content.Patch
{
    internal static class PatchAboutCultureSkin
    {
        [HarmonyTranspiler, HarmonyPatch(typeof(ActorTextureSubAsset), nameof(ActorTextureSubAsset.getUnitTexturePath))]
        private static IEnumerable<CodeInstruction> getUnitTexturePath_transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            
            var start_idx = list.FindIndex(x=>x.opcode == OpCodes.Ldfld && (x.operand as FieldInfo)?.Name == nameof(ActorTextureSubAsset.texture_path_warrior));

            var idx = list.FindIndex(start_idx, x => x.opcode == OpCodes.Callvirt && (x.operand as MethodInfo)?.Name == "get_" + nameof(Subspecies.has_mutation_reskin));
            list[idx-1].opcode = OpCodes.Ldarg_1;
            list.InsertRange(idx, [
                new (OpCodes.Ldloc_3),
                new (OpCodes.Call, AccessTools.Method(typeof(PatchAboutCultureSkin), nameof(tryLoadCultureWarriorSkin))),
                new (OpCodes.Stloc_3),
                new (OpCodes.Ldloc_0)
            ]);

            return list;
        }
        private static string tryLoadCultureWarriorSkin(Actor pActor, string defaultSkin)
        {
            var skinID = GetCultureSkinID(pActor);
            if (skinID == -1) return defaultSkin;
            
            return pActor.asset.skin_warrior[skinID % pActor.asset.skin_warrior.Length];
        }
        [HarmonyPrefix, HarmonyPatch(typeof(ActorTextureSubAsset), nameof(ActorTextureSubAsset.getTextureSkinBasedOnSex))]
        private static bool getTextureSkinBasedOnSex_prefix(ActorTextureSubAsset __instance, Actor pActor, ref string __result)
        {
            var skinID = GetCultureSkinID(pActor);
            if (skinID == -1) return true;

            if (pActor.isSexFemale())
            {
                __result = __instance.texture_path_base + pActor.asset.skin_citizen_female[skinID % pActor.asset.skin_citizen_female.Length];
            }
            else
            {
                __result = __instance.texture_path_base + pActor.asset.skin_citizen_male[skinID % pActor.asset.skin_citizen_male.Length];
            }
            return false;
        }
        private static int GetCultureSkinID(Actor pActor)
        {
            if (!pActor.hasCultureTrait(CultureTraits.CultureSkin.id)) return -1;
            var culture = pActor.culture;
            culture.data.get(ContentCultureDataKeys.SkinID_int, out int skinID, -1);
            if (skinID == -1)
            {
                skinID = Randy.randomInt(0, pActor.asset.skin_citizen_male.Length);
                culture.data.set(ContentCultureDataKeys.SkinID_int, skinID);
            }
            return skinID;
        }
    }
}