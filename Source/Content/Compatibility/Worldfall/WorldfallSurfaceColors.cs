using System;
using System.Reflection;
using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>采集原模型的逐面配色，平面镜头直接绘制同一真实表面。</summary>
internal static class WorldfallSurfaceColors
{
    /// <summary>原模型、面色编号和实例自己的配色表，安装后不再重复查字段。</summary>
    private static FieldInfo model, palette, slots, colors, triangles;

    /// <summary>原动物用同一身体图片提取个体配色。</summary>
    private static MethodInfo creaturePalette;

    /// <summary>沿已核对的外部资料格式绑定读取入口。</summary>
    /// <param name="assembly">实际加载的外部模组。</param>
    internal static void Install(Assembly assembly)
    {
        Type instance = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        model = AccessTools.Field(instance, "Model");
        palette = AccessTools.Field(instance, "Palette");
        slots = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Slot");
        colors = AccessTools.Field(model.FieldType, "SlotColor");
        triangles = AccessTools.Field(slots.DeclaringType, "Tri");
        creaturePalette = AccessTools.Method(assembly.GetType("FirstPerson.Creatures", true), "SidePalette");
    }

    /// <summary>实例覆盖色优先，零值继续原模型颜色；颜色数组不持有外部可变对象。</summary>
    /// <param name="instance">实际身体、衣甲或持物实例。</param><param name="part">原模型部件。</param>
    internal static Color32[] Read(object instance, object part)
    {
        var assigned = (byte[])slots.GetValue(part);
        var basis = (uint[])colors.GetValue(model.GetValue(instance));
        var custom = (uint[])palette.GetValue(instance);
        var result = new Color32[assigned.Length];
        for (int face = 0; face < result.Length; face++)
        {
            int slot = assigned[face];
            uint color = custom != null && slot < custom.Length && custom[slot] != 0 ? custom[slot] : basis[slot];
            result[face] = new Color32((byte)color, (byte)(color >> 8), (byte)(color >> 16), (byte)(color >> 24));
        }
        return result;
    }

    /// <summary>纹样将原面分组后仍按原三角形取色，不按分组顺序错读色号。</summary>
    /// <param name="instance">实际模型实例。</param><param name="part">原部件。</param><param name="selected">当前可见组的三角形。</param>
    internal static Color32[] Read(object instance, object part, int[] selected)
    {
        Color32[] all = Read(instance, part);
        var original = (int[])triangles.GetValue(part);
        if (ReferenceEquals(selected, original)) return all;
        var faces = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < original.Length; i += 3)
            faces[(original[i], original[i + 1], original[i + 2])] = i / 3;
        var result = new Color32[selected.Length / 3];
        for (int i = 0; i < selected.Length; i += 3)
            result[i / 3] = all[faces[(selected[i], selected[i + 1], selected[i + 2])]];
        return result;
    }

    /// <summary>未曾被镜头看见的动物也从真实图片取得原有配色。</summary>
    /// <param name="body">原模型。</param><param name="image">已经完成的真实图片。</param>
    internal static uint[] CreaturePalette(object body, object image) =>
        (uint[])creaturePalette.Invoke(null, new[] { body, image });

    /// <summary>独立器官保留原顶点色及材质底色，不把渐变压成单面颜色。</summary>
    /// <param name="resource">已经加载的正式形体。</param><param name="part">实际部件。</param>
    internal static Color32[] Read(FormResource resource, int part)
    {
        Mesh mesh = resource.Meshes[part];
        Color32[] source = mesh.colors32;
        Color tint = resource.Surface(resource.Description.Parts[part].Surface, true).GetColor("_Color");
        var result = new Color32[mesh.vertexCount];
        for (int i = 0; i < result.Length; i++) result[i] = (source.Length == 0 ? Color.white : (Color)source[i]) * tint;
        return result;
    }

}
