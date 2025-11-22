using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;
using strings;
using UnityEngine;

namespace Cultiway.Patch
{
    internal static class PatchAboutActorTextureSubAssetExtend
    {
        [
            HarmonyPrefix,
            HarmonyPatch(
                typeof(ActorTextureSubAsset),
                nameof(ActorTextureSubAsset.preloadSpritePath)
            )
        ]
        private static bool preloadSpritePath_prefix(
            ActorTextureSubAsset __instance,
            bool pLoadHeads,
            ref bool __result
        )
        {
            if (!pLoadHeads) return true;
            if (
                __instance
                    .GetAnyExtend<ActorTextureSubAsset, ActorTextureSubAssetExtend>()
                    .disable_heads
            )
            {
                __result = false;
                return false;
            }
            return true;
        }

        [
            HarmonyPrefix,
            HarmonyPatch(
                typeof(DynamicActorSpriteCreatorUI),
                nameof(DynamicActorSpriteCreatorUI.getSpriteHeadForUI)
            )
        ]
        private static bool getSpriteHeadForUI_prefix(ActorAsset pAsset, ref Sprite __result)
        {
            if (
                pAsset.texture_asset
                    .GetAnyExtend<ActorTextureSubAsset, ActorTextureSubAssetExtend>()
                    .disable_heads
            )
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
