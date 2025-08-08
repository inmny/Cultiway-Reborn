using ai;
using Cultiway.Utils.Extension;
using HarmonyLib;
using strings;

namespace Cultiway.Patch;

internal static class PatchActorTool
{
    [HarmonyPrefix, HarmonyPatch(typeof(ActorTool), nameof(ActorTool.applyForceToUnit))]
    private static void applyForceToUnit_prefix(AttackData pData, BaseSimObject pTargetToCheck, ref float pMod)
    {
        var reduction = pTargetToCheck.stats[S.knockback_reduction];
        var attacker = pData.initiator;
        var attacker_power_level = attacker.isActor() ? attacker.a.GetExtend().GetPowerLevel() : 0;
        var power_level = pTargetToCheck.isActor() ? pTargetToCheck.a.GetExtend().GetPowerLevel() : 0;
        if (power_level > attacker_power_level)
        {
            reduction += (power_level - attacker_power_level) * 1000f;
        }
        if (reduction >= 0)
        {
            pMod *= (1 / (1 + reduction));
        }
        else
        {
            pMod *= (1 - reduction);
        }

        if (pMod < 0.01f)
        {
            pMod = 0;
        }
    }
}