using Cultiway.Content.Combat;
using Cultiway.Content.Skills;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Patch;

/// <summary>原版弹丸先撞真实屏障，再进入原有伤害及落点回调。</summary>
internal static class PatchConstructedProjectileBarrier
{
    /// <summary>原版正在使用的三维位置。</summary>
    private static readonly AccessTools.FieldRef<Projectile, Vector3> Position =
        AccessTools.FieldRefAccess<Projectile, Vector3>("_current_position_3d");

    /// <summary>原版本步速度。</summary>
    private static readonly AccessTools.FieldRef<Projectile, Vector3> Velocity =
        AccessTools.FieldRefAccess<Projectile, Vector3>("_velocity");

    /// <summary>原版每次出生、反弹和反射都重新确定来源。</summary>
    /// <param name="__instance">实际弹丸。</param><param name="pInitiator">本次确定的原版来源。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(Projectile), "start")]
    private static void Start(Projectile __instance, BaseSimObject pInitiator)
    {
        ConstructedAbilityService.RememberProjectile(__instance, pInitiator);
        NativeProjectileFlights.Started(__instance);
    }

    /// <summary>采用原版重力积分预测本步，命中时先写回真实撞点再执行原碰撞。</summary>
    /// <param name="__instance">真实弹丸。</param><param name="pElapsed">本步世界秒数。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Projectile), "updateVelocity")]
    private static bool Move(Projectile __instance, float pElapsed)
    {
        Vector3 from = Position(__instance), speed = Velocity(__instance);
        speed.z -= SimGlobals.m.gravity * pElapsed;
        Vector3 to = from + speed * pElapsed;
        to.z = Mathf.Max(0f, to.z);
        if (!ConstructedAbilityService.InterceptProjectile(__instance, from, to, out Vector3 hit)) return true;
        Position(__instance) = hit;
        Velocity(__instance) = speed;
        __instance.getCollided(hit);
        return false;
    }
}
