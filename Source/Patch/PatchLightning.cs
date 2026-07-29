using Cultiway.Content;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>
/// 雷击检测：拦截 <see cref="MapAction.checkLightningAction"/>（所有 MapBox.spawnLightning* 的唯一咽喉）。
/// 仅天雷（pActor==null：天气雷暴 + 闪电神力）时，通知 KnightForge 给命中范围内的候选始祖骑士
/// 打"近期被雷击"标记，供死亡钩子据此判定"死于雷击"。
/// 范围枚举镜像 checkLightningAction 原逻辑（getSimpleList + 平方距离）。
/// 排除：特性/plot/角色触发的闪电（pActor!=null）与模组自己的闪电技能（走技能系统，不经此方法）。
/// </summary>
internal static class PatchLightning
{
    [HarmonyPrefix, HarmonyPatch(typeof(MapAction), nameof(MapAction.checkLightningAction))]
    private static void checkLightningAction_prefix(Vector2Int pPos, int pRad, Actor pActor)
    {
        if (pActor != null) return; // 仅天雷（scope A）
        KnightForge.OnSkyLightning(pPos, pRad);
    }
}
