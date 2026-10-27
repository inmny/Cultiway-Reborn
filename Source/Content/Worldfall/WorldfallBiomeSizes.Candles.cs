#if S3COND2_WORLDFALL
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

internal static partial class WorldfallBiomeSizes
{
    /// <summary>固定三维蜡烛的尺寸来源，避免火焰动画差一个像素时整根蜡烛跟着伸缩。</summary>
    /// <param name="current">原生入口本帧选择的二维贴图。</param>
    /// <param name="building">当前群系物件。</param>
    /// <returns>完整蜡烛使用可见高度最大的第2帧，残骸及其他物件沿用原图。</returns>
    private static Sprite StableCandleSprite(Sprite current, Building building) =>
        building.asset.id == "Candle_tree" && LoadedTrees.Contains(building.asset.id) && !building.isRuin()
            ? building.animData.main[1]
            : current;

    /// <summary>远景也从同一张蜡烛图计算尺寸，保留原生缓存与显示流程。</summary>
    /// <param name="instructions">原生远景取图方法的指令。</param>
    /// <returns>只在缓存读图前固定完整蜡烛动画帧的指令。</returns>
    private static IEnumerable<CodeInstruction> StableFarCandleSprite(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(NativeSpriteGet))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return CodeInstruction.Call(typeof(WorldfallBiomeSizes), nameof(StableCandleSprite));
            }
            yield return instruction;
        }
    }
}
#endif
