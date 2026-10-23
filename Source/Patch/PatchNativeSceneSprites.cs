using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>登记原版精灵池的新对象，避免战斗中反复搜索场景。</summary>
internal static class PatchNativeSceneSprites
{
    /// <summary>保留原版建立、材质设置及池管理，只登记实际显示器。</summary>
    /// <param name="__result">原版建立的对象。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(QuantumSpriteGroupSystem), "createNew")]
    private static void AfterCreate(QuantumSprite __result) => FormNativeScenePass.Register(__result.GetComponent<SpriteRenderer>());
}
