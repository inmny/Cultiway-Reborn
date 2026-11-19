#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>按地面中位高度放置建筑，沿实际底面补齐地基，并在入口铺设石阶。</summary>
internal static partial class WorldfallBuildingFoundations
{
    /// <summary>建筑最低处与地面中位高度之间保留的实际距离。</summary>
    internal const float GroundClearance = 0.06f;
    /// <summary>原生建筑放置时会减去的固定距离。</summary>
    private const float NativeGroundInset = 0.05f;
    /// <summary>地基实例跟随原生建筑绘制记录回收，不保留已移除的建筑。</summary>
    private static readonly ConditionalWeakTable<object, Foundation> Foundations = new();
    /// <summary>本帧原生地表。</summary>
    private static FieldInfo SceneTerrain;
    /// <summary>本帧实际绘制的网格列表。</summary>
    private static FieldInfo SceneMeshes;
    /// <summary>已经选好距离档和颜色的建筑主体。</summary>
    private static FieldInfo BuildingMesh;
    /// <summary>与原生主体使用同一纵向中心偏移。</summary>
    private static FieldInfo BuildingCenterOffset;

    /// <summary>每座建筑复用绘制实例和采样数组，只在底形或所需深度变化时重建网格。</summary>
    private sealed class Foundation
    {
        /// <summary>贴合底部的地基。</summary>
        internal readonly MeshInstance Mesh = new();
        /// <summary>入口的石阶。</summary>
        internal readonly List<MeshInstance> Stairs = new();
        /// <summary>地面高度采样，排序时复用。</summary>
        internal readonly List<float> Samples = new();
        /// <summary>沿用底座主要材质的颜色。</summary>
        internal readonly uint[] Palette = new uint[1];
        /// <summary>当前网格使用的底形。</summary>
        internal FootingShape Shape;
        /// <summary>地基网格底部的模型坐标。</summary>
        internal float Bottom;
        /// <summary>本次放置准备的底形。</summary>
        internal FootingShape Prepared;
        /// <summary>本次地面最低点。</summary>
        internal float Low;
        /// <summary>主体与附件共用的点击编号。</summary>
        internal int Tag;
    }

    /// <summary>在原生高度用于主体、碰撞和地板之前修正同一个高度变量。</summary>
    /// <param name="collector">核对过版本的场景收集器类型。</param>
    /// <param name="harmony">现有的模型补丁组。</param>
    internal static void Install(Type collector, Harmony harmony)
    {
        MethodInfo draw = AccessTools.Method(collector, "DrawBuildingModel");
        SceneTerrain = AccessTools.Field(collector, "_terrain");
        SceneMeshes = AccessTools.Field(collector, "Meshes");
        BuildingMesh = AccessTools.Field(draw.GetParameters()[0].ParameterType, "Mesh");
        BuildingCenterOffset = AccessTools.Field(draw.GetParameters()[0].ParameterType, "CenterOffset");
        HarmonyMethod fix = new(AccessTools.Method(typeof(WorldfallBuildingFoundations), nameof(UseRaisedGround)));
        harmony.Patch(draw, transpiler: fix);
        harmony.Patch(AccessTools.Method(collector, "DrawSite"), transpiler: fix);
    }

    /// <summary>在第一次放置主体前改写原高度变量，后续地板、碰撞和特效自然共用新高度。</summary>
    /// <param name="instructions">实际渲染器方法的原始指令。</param>
    /// <returns>只增加一次地基处理调用的指令。</returns>
    private static IEnumerable<CodeInstruction> UseRaisedGround(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        int place = code.FindIndex(i => i.operand is MethodInfo method &&
            method.DeclaringType == typeof(MeshInstance) && method.Name == nameof(MeshInstance.Place));
        int subtract = place - 1;
        while (subtract > 1 && !(code[subtract].opcode == OpCodes.Sub &&
            code[subtract - 1].LoadsConstant(NativeGroundInset))) subtract--;
        if (place < 0 || subtract <= 1 || !code[subtract - 2].IsLdloc() || !code[subtract + 1].IsLdloc())
            throw new InvalidOperationException("Worldfall 1.0 建筑放置指令与地基接入约定不符。");
        int local = LocalIndex(code[subtract - 2]);
        CodeInstruction address = local < 256
            ? new CodeInstruction(OpCodes.Ldloca_S, (byte)local)
            : new CodeInstruction(OpCodes.Ldloca, (short)local);
        address.MoveLabelsFrom(code[subtract - 2]);
        // 先修正主体高度，再在真实放置完成后沿用主体的朝向、镜像和位置。
        code.InsertRange(place + 1, [new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorldfallBuildingFoundations), nameof(FinishFoundation)))]);
        code.InsertRange(subtract - 2, [address,
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Ldarg_3),
            new CodeInstruction(OpCodes.Ldarg_S, (byte)5), new CodeInstruction(code[subtract + 1]),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WorldfallBuildingFoundations), nameof(RaiseGround)))]);
        return code;
    }

    /// <summary>读取短指令或普通指令中登记的局部变量编号。</summary>
    /// <param name="instruction">已确认的局部变量读取指令。</param>
    /// <returns>该变量在原方法中的编号。</returns>
    private static int LocalIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldloc_0) return 0;
        if (instruction.opcode == OpCodes.Ldloc_1) return 1;
        if (instruction.opcode == OpCodes.Ldloc_2) return 2;
        if (instruction.opcode == OpCodes.Ldloc_3) return 3;
        return instruction.operand is LocalBuilder local ? local.LocalIndex : Convert.ToInt32(instruction.operand);
    }

    /// <summary>普通自有建筑使用地基；植被、码头和具有真实地下坑腔的模型保留原来的落地方式。</summary>
    /// <param name="asset">实际建筑资产。</param>
    /// <param name="model">本次绘制的距离档或状态。</param>
    /// <returns>是否应用普通建筑地基。</returns>
    internal static bool AppliesTo(BuildingAsset asset, MeshModel model) =>
        model.Info("cultiway_building") == "1" && model.Info("cultiway_ground_pit") != "1" &&
        asset.type is not ("type_tree" or "type_vegetation") &&
        model.Info("kind") != "docks" && !asset.id.StartsWith("docks_", StringComparison.Ordinal) &&
        !asset.id.StartsWith("fishing_docks_", StringComparison.Ordinal);

    /// <summary>先取得实际底形，再用覆盖地面的中位高度修正主体，并保存本次附件放置数据。</summary>
    /// <param name="ground">原生方法中随后用于所有放置的高度。</param>
    /// <param name="collector">当前场景收集器。</param>
    /// <param name="nb">当前建筑的原生绘制记录。</param>
    /// <param name="building">实际建筑。</param>
    /// <param name="position">已经对齐占地中心横坐标的原生位置。</param>
    /// <param name="tag">主体与地基共用的点击编号。</param>
    /// <param name="heading">本次实际绘制的朝向。</param>
    private static void RaiseGround(ref float ground, object collector, object nb, Building building,
        Vector2 position, int tag, float heading)
    {
        MeshInstance body = (MeshInstance)BuildingMesh.GetValue(nb);
        Foundation foundation = Foundations.GetValue(nb, static _ => new Foundation());
        foundation.Prepared = null;
        if (!AppliesTo(building.asset, body.Model)) return;
        FootingShape shape = ShapeOf(body.Model);
        if (shape.Top.Length == 0) return;
        BuildingFundament footprint = building.asset.fundament;
        float width = footprint.width;
        float x = position.x, y = position.y + (float)BuildingCenterOffset.GetValue(nb);
        float halfX = Math.Max(Math.Abs(shape.Min.X), Math.Abs(shape.Max.X)) * width;
        float halfY = Math.Max(Math.Abs(shape.Min.Y), Math.Abs(shape.Max.Y)) * width;
        // 原生放置还会额外旋转九十度，地形范围和地基都必须沿用同一实际朝向。
        float angle = heading + (float)Math.PI * 0.5f;
        float cosine = Math.Abs((float)Math.Cos(angle)), sine = Math.Abs((float)Math.Sin(angle));
        float reachX = halfX * cosine + halfY * sine;
        float reachY = halfX * sine + halfY * cosine;
        TerrainCache terrain = (TerrainCache)SceneTerrain.GetValue(collector);
        TerrainRange(terrain, x - reachX, y - reachY, x + reachX, y + reachY,
            foundation.Samples, out float low, out float median);
        ground = median + GroundClearance - body.Model.Min.Z * width + NativeGroundInset;
        foundation.Prepared = shape;
        foundation.Low = low;
        foundation.Tag = tag;
    }

    /// <summary>主体放置后生成附件，直接复用其变换，确保圆弧、缺口、偏心和镜像全部贴合。</summary>
    /// <param name="collector">当前场景收集器。</param>
    /// <param name="nb">刚刚完成放置的建筑记录。</param>
    /// <param name="building">实际建筑。</param>
    private static void FinishFoundation(object collector, object nb, Building building)
    {
        if (!Foundations.TryGetValue(nb, out Foundation foundation) || foundation.Prepared == null) return;
        MeshInstance body = (MeshInstance)BuildingMesh.GetValue(nb);
        FootingShape shape = foundation.Prepared;
        foundation.Prepared = null;
        float width = building.asset.fundament.width;
        float bottom = (float)Math.Floor((foundation.Low - 0.1f - body.Position.Z) * 4f) / (4f * width);
        if (foundation.Shape != shape || bottom < foundation.Bottom)
        {
            foundation.Mesh.Model = CreateFoundation(shape, bottom);
            foundation.Shape = shape;
            foundation.Bottom = bottom;
        }
        MeshInstance mesh = foundation.Mesh;
        mesh.EnsurePose();
        foundation.Palette[0] = body.Palette[shape.Slot];
        mesh.Palette = foundation.Palette;
        mesh.Position = body.Position;
        mesh.Basis = body.Basis;
        mesh.Static = true;
        mesh.KeepSmall = true;
        mesh.ShadowOnly = body.ShadowOnly;
        mesh.Tag = foundation.Tag;
        List<MeshInstance> meshes = (List<MeshInstance>)SceneMeshes.GetValue(collector);
        meshes.Add(mesh);
        AddStairs(foundation, body, width, (TerrainCache)SceneTerrain.GetValue(collector), meshes);
    }

    /// <summary>读取整个底座范围并取中位数，少量高点不会把整座建筑抬到最高点。</summary>
    /// <param name="terrain">实际用于三维绘制和人物落地的地表。</param>
    /// <param name="left">建筑最左边。</param>
    /// <param name="front">建筑最前边。</param>
    /// <param name="right">建筑最右边。</param>
    /// <param name="back">建筑最后边。</param>
    /// <param name="samples">当前建筑复用的采样数组。</param>
    /// <param name="low">范围内的最低地面。</param>
    /// <param name="median">奇数取中间一项，偶数取中间两项平均。</param>
    internal static void TerrainRange(TerrainCache terrain, float left, float front, float right, float back,
        List<float> samples, out float low, out float median)
    {
        int x0 = Math.Max(0, (int)Math.Floor(left - 0.5f));
        int y0 = Math.Max(0, (int)Math.Floor(front - 0.5f));
        int x1 = Math.Min(terrain.Width - 1, (int)Math.Ceiling(right - 0.5f));
        int y1 = Math.Min(terrain.Height - 1, (int)Math.Ceiling(back - 0.5f));
        samples.Clear();
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float height = terrain.SampleHeight(x + 0.5f, y + 0.5f);
            samples.Add(height);
        }
        samples.Sort();
        low = samples[0];
        int middle = samples.Count / 2;
        median = samples.Count % 2 == 0 ? (samples[middle - 1] + samples[middle]) * 0.5f : samples[middle];
    }
}
#endif
