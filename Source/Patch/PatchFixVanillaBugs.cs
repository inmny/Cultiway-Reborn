using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchFixVanillaBugs
{
    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(ItemRendering),
        nameof(ItemRendering.getItemMainSpriteFrame))]
    private static bool GuardMissingHandRendererSprites(
        IHandRenderer pHandRendererAsset,
        ref Sprite __result)
    {
        if (pHandRendererAsset == null)
        {
            return true;
        }

        Sprite[] sprites = pHandRendererAsset.getSprites();
        if (sprites is { Length: > 0 })
        {
            return true;
        }

        // 原版会直接访问 sprites.Length / sprites[0]。
        // 部分任务工具只有逻辑资源而没有手持贴图，此时应视为“不绘制”。
        __result = null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(DynamicSprites),
        nameof(DynamicSprites.getItemSpriteID),
        typeof(Sprite),
        typeof(int))]
    private static bool GuardMissingItemSpriteId(
        Sprite pSprite,
        ref long __result)
    {
        if (pSprite != null)
        {
            return true;
        }

        // checkHasRenderedItem 只检查逻辑装备；工具资源没有贴图时，
        // 原版仍会在这里对 null 调用 GetHashCode。
        __result = 0L;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(DynamicSprites),
        nameof(DynamicSprites.getCachedAtlasItemSprite),
        typeof(long),
        typeof(Sprite))]
    private static bool GuardMissingCachedItemSprite(
        Sprite pSpriteSource,
        ref Sprite __result)
    {
        if (pSpriteSource != null)
        {
            return true;
        }

        __result = null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(DynamicSprites),
        nameof(DynamicSprites.getCachedAtlasItemSprite),
        typeof(long),
        typeof(Sprite),
        typeof(ColorAsset))]
    private static bool GuardMissingColoredCachedItemSprite(
        Sprite pSpriteSource,
        ref Sprite __result)
    {
        if (pSpriteSource != null)
        {
            return true;
        }

        __result = null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ResourceAsset), nameof(ResourceAsset.getGameplaySprite))]
    private static bool GuardMissingResourceGameplaySprite(
        ResourceAsset __instance,
        ref Sprite __result)
    {
        if (__instance?.gameplay_sprites is { Length: > 0 } sprites &&
            sprites[0] != null)
        {
            return true;
        }

        // 资源仍可参与库存与经济逻辑；缺少表现贴图时只跳过绘制。
        __result = null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuantumSpriteLibrary), "drawResourceIconOnStockpile")]
    private static bool SkipMissingStockpileResourceSprite(Sprite pSprite)
    {
        // getGameplaySprite 的空值保护只能避免资源自身解引用；
        // 原版绘制入口仍会对空 Sprite 调用 GetHashCode，因此必须在这里跳过。
        return pSprite != null;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(EffectsCamera), "LateUpdate")]
    private static bool EffectsCamera_LateUpdate()
    {
        // 原版会把屏幕尺寸除以 3 后直接创建 RenderTexture；
        // 加载早期尺寸可能暂时为 0，此时等待下一帧尺寸就绪即可。
        return Screen.width >= 3 && Screen.height >= 3;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateRotations))]
    private static IEnumerable<CodeInstruction> Actor_updateRotations(IEnumerable<CodeInstruction> codes)
    {
        foreach (var code in codes)
        {
            if (code.opcode == OpCodes.Call &&
                ((code.operand as MethodInfo)?.Name.Contains(nameof(Actor.is_unconscious)) ?? false))
            {
                code.operand = AccessTools.Method(typeof(Actor), nameof(Actor.isLying));
            }
            yield return code;
        }
    }
}
