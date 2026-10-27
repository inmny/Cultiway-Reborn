#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FirstPerson.Core;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

internal static partial class WorldfallBiomeSizes
{
    /// <summary>本次实际加载了正式树模型的资产编号。</summary>
    private static HashSet<string> LoadedTrees;
    /// <summary>原生近景记录对应的建筑。</summary>
    private static FieldInfo SizedBiomeBuilding;
    /// <summary>原生树木的高度放大入口。</summary>
    private static MethodInfo NativeTreeGrow;
    /// <summary>原生树木的宽度放大入口。</summary>
    private static MethodInfo NativeTreeWiden;
    /// <summary>原生贴图缓存读入当前动画图的入口。</summary>
    private static MethodInfo NativeSpriteGet;

    /// <summary>调整原生尺寸计算，让三档模型、远景、遮挡与倒树使用同一比例。</summary>
    /// <param name="collector">核对过版本的原生场景类型。</param>
    /// <param name="harmony">模型接入已有的补丁组。</param>
    /// <param name="trees">实际加载了正式树模型的编号。</param>
    /// <param name="plants">是否加载了正式花草模型。</param>
    internal static void Install(Type collector, Harmony harmony, IEnumerable<string> trees, bool plants)
    {
        LoadedTrees = new HashSet<string>(trees, StringComparer.Ordinal);
        if (LoadedTrees.Count > 0)
        {
            MethodInfo update = AccessTools.Method(collector, "UpdateBuildingSprite");
            SizedBiomeBuilding = AccessTools.Field(update.GetParameters()[0].ParameterType, "Building");
            NativeTreeGrow = AccessTools.Method(collector, "TreeGrow");
            NativeTreeWiden = AccessTools.Method(collector, "TreeWiden");
            NativeSpriteGet = AccessTools.Method(update.GetParameters()[1].ParameterType, "Get", [typeof(Sprite)]);
            harmony.Patch(update, transpiler: Patch(nameof(ScaleNearbyBiomeTrees)));
            harmony.Patch(AccessTools.Method(collector, "FarSpriteOf"),
                transpiler: Patch(nameof(ScaleDistantBiomeTrees)));
            harmony.Patch(AccessTools.Method(collector, "FarImage"),
                transpiler: Patch(nameof(StableFarCandleSprite)));
            harmony.Patch(AccessTools.Method(typeof(MeshInstance), nameof(MeshInstance.Place)),
                postfix: Patch(nameof(LowerBiomeModels)));
        }
        if (plants)
            harmony.Patch(AccessTools.Method(typeof(MeshInstance), nameof(MeshInstance.Place)),
                prefix: Patch(nameof(ScaleBiomePlants)));
    }

    /// <summary>取得本类的尺寸调整补丁。</summary>
    /// <param name="name">补丁方法名称。</param>
    /// <returns>交给原有补丁组安装的方法。</returns>
    private static HarmonyMethod Patch(string name) => new(AccessTools.Method(typeof(WorldfallBiomeSizes), name));

    /// <summary>在原生近景生长计算后等比缩放，尺寸由原图每次重算，不会逐帧累积。</summary>
    /// <param name="instructions">原生近景尺寸方法的指令。</param>
    /// <returns>保留原流程并加入群系比例的指令。</returns>
    private static IEnumerable<CodeInstruction> ScaleNearbyBiomeTrees(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(NativeSpriteGet))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Ldfld, SizedBiomeBuilding);
                yield return CodeInstruction.Call(typeof(WorldfallBiomeSizes), nameof(StableCandleSprite));
            }
            yield return instruction;
            if (!instruction.Calls(NativeTreeGrow) && !instruction.Calls(NativeTreeWiden)) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldfld, SizedBiomeBuilding);
            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Building), nameof(Building.asset)));
            yield return CodeInstruction.Call(typeof(WorldfallBiomeSizes), nameof(BiomeTreeDisplayScale));
            yield return new CodeInstruction(OpCodes.Mul);
        }
    }

    /// <summary>同步原生远景尺寸缓存，避免离开近景后树木重新变大。</summary>
    /// <param name="instructions">原生远景尺寸方法的指令。</param>
    /// <returns>保留远景颜色与缓存，仅加入同一树木比例的指令。</returns>
    private static IEnumerable<CodeInstruction> ScaleDistantBiomeTrees(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(NativeTreeGrow) && !instruction.Calls(NativeTreeWiden)) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return CodeInstruction.Call(typeof(WorldfallBiomeSizes), nameof(BiomeTreeDisplayScale));
            yield return new CodeInstruction(OpCodes.Mul);
        }
    }

    /// <summary>只调整已载入正式树模型的修仙群系。</summary>
    /// <param name="asset">本次计算尺寸的建筑资产。</param>
    /// <returns>对应群系的等比调整量。</returns>
    private static float BiomeTreeDisplayScale(BuildingAsset asset) =>
        asset != null && LoadedTrees.Contains(asset.id) ? TreeScale(asset.id) : 1f;

    /// <summary>花草的原生放置重建缩放，因此只在放置前调整一次，保留落地和随机差异。</summary>
    /// <param name="__instance">本次放置的花草模型实例。</param>
    /// <param name="sx">原生左右方向比例。</param>
    /// <param name="sy">原生前后方向比例。</param>
    /// <param name="sz">原生高度比例。</param>
    private static void ScaleBiomePlants(MeshInstance __instance, ref float sx, ref float sy, ref float sz)
    {
        if (__instance.Model?.Info("cultiway_plant") != "1") return;
        float scale = PlantScaleFor(__instance.Model.Info("cultiway_asset"));
        sx *= scale;
        sy *= scale;
        sz *= scale;
    }

    /// <summary>原生放置重设位置后下移指定群系模型，不逐帧累积。</summary>
    /// <param name="__instance">本次放置的模型实例。</param>
    /// <param name="sz">原生入口使用的实际高度比例。</param>
    private static void LowerBiomeModels(MeshInstance __instance, float sz)
    {
        MeshModel model = __instance.Model;
        if (model == null) return;
        float sink = model.Info("cultiway_tree") == "1" ? model.Info("cultiway_asset") switch
        {
            "Candle_tree" => CandleRootSink,
            "FleshBlood_tree" => FleshBloodRootSink,
            "Titans_tree" => TitansRootSink,
            _ => 0f
        } : model.Info("cultiway_plant") == "1" && model.Info("cultiway_asset") == "Candle" ? CandlePlantSink : 0f;
        __instance.Position.Z -= sink * sz;
    }
}
#endif
