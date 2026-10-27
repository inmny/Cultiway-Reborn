#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FirstPerson.Core;
using HarmonyLib;

namespace Cultiway.Content.Worldfall;

/// <summary>在三维地表副本中露出模型的地下坑腔，不改地图、碰撞或模型落点。</summary>
internal static class WorldfallGroundPits
{
    /// <summary>模型文件明确登记的坑口内外半径和坑底，使用模型自身尺寸。</summary>
    private static readonly Dictionary<MeshModel, (float Inner, float Radius, float Floor)> Shapes = new();
    /// <summary>当前可见坑口，尺寸已经换算成地图坐标。</summary>
    private static List<(float X, float Y, float Inner, float Radius, float Floor)> Pits = new();
    /// <summary>复用下一帧列表，避免每帧为建筑建立新对象。</summary>
    private static List<(float X, float Y, float Inner, float Radius, float Floor)> NextPits = new();
    /// <summary>这些坑口所在的原生地表缓存，只读取其地形数据。</summary>
    private static TerrainCache PitTerrain;
    /// <summary>场景本帧实际绘制的网格。</summary>
    private static FieldInfo SceneMeshes;
    /// <summary>原生采样器持有的地表缓存。</summary>
    private static FieldInfo SamplerCache;
    /// <summary>上传显卡之前的地表副本。</summary>
    private static FieldInfo PackedTerrain;
    /// <summary>通知原生渲染器重新上传指定范围。</summary>
    private static MethodInfo MarkChanged;

    /// <summary>只有已登记深坑模型时，才安装地表显示补丁。</summary>
    /// <param name="collector">已核对版本的场景收集类型。</param>
    /// <param name="harmony">现有的模型补丁组。</param>
    /// <param name="models">已经读入的自有模型。</param>
    internal static void Install(Type collector, Harmony harmony, Dictionary<string, MeshModel> models)
    {
        foreach (MeshModel model in models.Values)
        {
            if (model.Info("cultiway_ground_pit") != "1") continue;
            Shapes.Add(model, (
                float.Parse(model.Info("pit_cut_inner"), CultureInfo.InvariantCulture),
                float.Parse(model.Info("pit_cut_radius"), CultureInfo.InvariantCulture),
                float.Parse(model.Info("pit_floor"), CultureInfo.InvariantCulture)));
        }
        if (Shapes.Count == 0) return;
        SceneMeshes = AccessTools.Field(collector, "Meshes");
        SamplerCache = AccessTools.Field(collector.Assembly.GetType("FirstPerson.WorldSampler"), "Cache");
        Type gpuTerrain = collector.Assembly.GetType("FirstPerson.Gpu.GpuTerrain", true);
        PackedTerrain = AccessTools.Field(gpuTerrain, "_packed");
        MarkChanged = AccessTools.Method(typeof(TerrainCache), "MarkChanged");
        harmony.Patch(AccessTools.Method(collector, "Collect"),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(WorldfallGroundPits), nameof(CollectPits))));
        harmony.Patch(AccessTools.Method(gpuTerrain, "Pack"),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(WorldfallGroundPits), nameof(ExposePits))));
    }

    /// <summary>从本帧真正显示的建筑取坑口，移除、换世界和切换距离档都会同步。</summary>
    /// <param name="__instance">原生场景收集器。</param>
    /// <param name="sampler">当前世界的采样器。</param>
    private static void CollectPits(object __instance, object sampler) =>
        UpdatePits((TerrainCache)SamplerCache.GetValue(sampler), (List<MeshInstance>)SceneMeshes.GetValue(__instance));

    /// <summary>先收集坑口，再比较前后位置，只有发生变化才要求重新上传地表。</summary>
    /// <param name="land">本帧的原生地表。</param>
    /// <param name="meshes">本帧实际显示的模型。</param>
    internal static void UpdatePits(TerrainCache land, List<MeshInstance> meshes)
    {
        NextPits.Clear();
        foreach (MeshInstance mesh in meshes)
        {
            if (!Shapes.TryGetValue(mesh.Model, out var shape)) continue;
            float scale = Math.Abs(mesh.Basis.C2.Z);
            NextPits.Add((mesh.Position.X, mesh.Position.Y, shape.Inner * scale,
                shape.Radius * scale, mesh.Position.Z + shape.Floor * scale - 0.1f));
        }
        if (ReferenceEquals(PitTerrain, land) && Pits.SequenceEqual(NextPits)) return;
        if (PitTerrain != null) DirtyPits(PitTerrain, Pits);
        DirtyPits(land, NextPits);
        PitTerrain = land;
        (Pits, NextPits) = (NextPits, Pits);
    }

    /// <summary>只标记坑缘附近的上传范围，建筑消失后原生打包会恢复原表面。</summary>
    /// <param name="land">需要刷新显示的地表。</param>
    /// <param name="pits">变化之前或之后的坑口。</param>
    private static void DirtyPits(TerrainCache land, List<(float X, float Y, float Inner, float Radius, float Floor)> pits)
    {
        foreach (var pit in pits)
        {
            int x0 = Math.Max(0, (int)Math.Floor(pit.X - pit.Radius) - 1);
            int y0 = Math.Max(0, (int)Math.Floor(pit.Y - pit.Radius) - 1);
            int x1 = Math.Min(land.Width, (int)Math.Ceiling(pit.X + pit.Radius) + 1);
            int y1 = Math.Min(land.Height, (int)Math.Ceiling(pit.Y + pit.Radius) + 1);
            MarkChanged.Invoke(land, new object[] { x0, y0, x1, y1 });
        }
    }

    /// <summary>在原生打包完成、显卡上传之前压低坑口内的地表副本。</summary>
    /// <param name="__instance">持有上传数组的原生显卡地表。</param>
    /// <param name="land">原生地表数据。</param>
    /// <param name="x0">本次上传范围的左边界。</param>
    /// <param name="y0">本次上传范围的前边界。</param>
    /// <param name="x1">本次上传范围的右边界，不含此列。</param>
    /// <param name="y1">本次上传范围的后边界，不含此行。</param>
    private static void ExposePits(object __instance, TerrainCache land, int x0, int y0, int x1, int y1)
    {
        if (!ReferenceEquals(PitTerrain, land) || Pits.Count == 0) return;
        ApplyPits((uint[])PackedTerrain.GetValue(__instance), land.Width, x0, y0, x1, y1);
    }

    /// <summary>只改两项显示高度；颜色、材质、水域标记及原始高度数组全部沿用。</summary>
    /// <param name="packed">原生打包出的显卡上传副本。</param>
    /// <param name="width">地表每行的格数。</param>
    /// <param name="x0">上传左边界。</param>
    /// <param name="y0">上传前边界。</param>
    /// <param name="x1">上传右边界，不含此列。</param>
    /// <param name="y1">上传后边界，不含此行。</param>
    internal static unsafe void ApplyPits(uint[] packed, int width, int x0, int y0, int x1, int y1)
    {
        foreach (var pit in Pits)
        {
            int left = Math.Max(x0, (int)Math.Floor(pit.X - pit.Radius));
            int front = Math.Max(y0, (int)Math.Floor(pit.Y - pit.Radius));
            int right = Math.Min(x1, (int)Math.Ceiling(pit.X + pit.Radius));
            int back = Math.Min(y1, (int)Math.Ceiling(pit.Y + pit.Radius));
            for (int y = front; y < back; y++)
            for (int x = left; x < right; x++)
            {
                float dx = x + 0.5f - pit.X, dy = y + 0.5f - pit.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance >= pit.Radius) continue;
                float amount = Math.Max(0f, (distance - pit.Inner) / (pit.Radius - pit.Inner));
                amount = 1f - amount * amount * (3f - 2f * amount);
                int index = (y * width + x) * 4;
                for (int field = 0; field < 2; field++)
                {
                    uint bits = packed[index + field];
                    float height = *(float*)&bits;
                    height += (Math.Min(height, pit.Floor) - height) * amount;
                    packed[index + field] = *(uint*)&height;
                }
            }
        }
    }
}
#endif
