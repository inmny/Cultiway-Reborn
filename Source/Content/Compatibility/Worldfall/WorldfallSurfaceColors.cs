using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>采集原模型的逐面配色，平面镜头直接绘制同一真实表面。</summary>
internal static class WorldfallSurfaceColors
{
    /// <summary>原模型、面色编号和实例自己的配色表，安装后不再重复查字段。</summary>
    private static FieldInfo model, palette, slots, colors;

    /// <summary>沿已核对的外部资料格式绑定读取入口。</summary>
    /// <param name="assembly">实际加载的外部模组。</param>
    internal static void Install(Assembly assembly)
    {
        Type instance = assembly.GetType("FirstPerson.Core.MeshInstance", true);
        model = AccessTools.Field(instance, "Model");
        palette = AccessTools.Field(instance, "Palette");
        slots = AccessTools.Field(assembly.GetType("FirstPerson.Core.MeshPart", true), "Slot");
        colors = AccessTools.Field(model.FieldType, "SlotColor");
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
}
