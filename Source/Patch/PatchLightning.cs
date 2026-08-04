using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>
/// 把内容层主动标记的原版天雷上下文传递到异步受击事件。
/// </summary>
internal static class PatchLightning
{
    private static Action<Vector2Int, int> actionBeforeSkyLightningDamage;

    [ThreadStatic]
    private static long requestedSourceScopeId;

    [ThreadStatic]
    private static Stack<long> damageScopeStack;

    /// <summary>当前调用栈所属的无来源天雷作用域 ID；不在天雷中时为零。</summary>
    public static long CurrentSkyLightningScopeId =>
        damageScopeStack == null || damageScopeStack.Count == 0 ? 0 : damageScopeStack.Peek();

    /// <summary>注册所有无来源原版天雷进入伤害结算前的行为。</summary>
    public static void RegisterActionBeforeSkyLightningDamage(Action<Vector2Int, int> action)
    {
        actionBeforeSkyLightningDamage += action;
    }

    /// <summary>在指定来源上下文中生成原版天雷，使其受击事件能够在稍后恢复该上下文。</summary>
    public static void ExecuteTrackedSkyLightning(long sourceScopeId, Action spawnAction)
    {
        if (sourceScopeId <= 0 || spawnAction == null) return;
        long previousScopeId = requestedSourceScopeId;
        requestedSourceScopeId = sourceScopeId;
        try
        {
            spawnAction();
        }
        finally
        {
            requestedSourceScopeId = previousScopeId;
        }
    }

    /// <summary>仅把主动标记传给无来源且带雷击标记的世界伤害。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(MapAction), nameof(MapAction.damageWorld))]
    private static void damageWorld_prefix(
        WorldTile pTile,
        int pRad,
        TerraformOptions pOptions,
        BaseSimObject pByWho,
        out long __state)
    {
        bool isSkyLightning = pTile != null && pOptions?.lightning_effect == true && pByWho == null;
        __state = isSkyLightning ? requestedSourceScopeId : 0;
        damageScopeStack ??= new Stack<long>();
        damageScopeStack.Push(__state);
        if (isSkyLightning) actionBeforeSkyLightningDamage?.Invoke(pTile.pos, pRad);
    }

    /// <summary>即使原版雷击结算抛出异常，也保证恢复外层伤害作用域。</summary>
    [HarmonyFinalizer, HarmonyPatch(typeof(MapAction), nameof(MapAction.damageWorld))]
    private static Exception damageWorld_finalizer(Exception __exception, long __state)
    {
        if (damageScopeStack?.Count > 0) damageScopeStack.Pop();
        return __exception;
    }
}
