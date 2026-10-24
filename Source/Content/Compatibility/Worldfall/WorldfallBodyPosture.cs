using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>为贴地长身模型调整静止姿态，抬起前部并保留截面厚度，不将整个身体放大成长杆。</summary>
internal static class WorldfallBodyPosture
{
    /// <summary>外部模型与部件结构，只在安装边界查找。</summary>
    private static FieldInfo parts, position, normal, pivot, minimum, maximum, version;
    /// <summary>外部向量三个分量。</summary>
    private static FieldInfo x, y, z;
    /// <summary>外部模型复制及说明写入入口。</summary>
    private static MethodInfo derive, parseInfo;
    /// <summary>外部实际部件与向量类型。</summary>
    private static Type partType, vectorType;
    /// <summary>相同原模型与同一静止比例共用一份几何；不随单位或世界重复创建。</summary>
    private static readonly Dictionary<(object Model, float Crown), object> models = new();
    /// <summary>模型切换或重新选择细节时仍能找回原形，避免重复抬高。</summary>
    private static readonly Dictionary<object, object> originals = new();

    /// <summary>绑定当前支持的外部模型资料。</summary>
    /// <param name="assembly">实际外部程序集。</param>
    internal static void Install(Assembly assembly)
    {
        Type model = assembly.GetType("FirstPerson.Core.MeshModel", true);
        partType = assembly.GetType("FirstPerson.Core.MeshPart", true);
        vectorType = assembly.GetType("FirstPerson.Core.Vec3", true);
        parts = AccessTools.Field(model, "Parts"); minimum = AccessTools.Field(model, "Min"); maximum = AccessTools.Field(model, "Max");
        position = AccessTools.Field(partType, "Pos"); normal = AccessTools.Field(partType, "Nrm");
        pivot = AccessTools.Field(partType, "Pivot"); version = AccessTools.Field(partType, "Version");
        x = AccessTools.Field(vectorType, "X"); y = AccessTools.Field(vectorType, "Y"); z = AccessTools.Field(vectorType, "Z");
        derive = AccessTools.Method(model, "Derive"); parseInfo = AccessTools.Method(model, "ParseInfo");
    }

    /// <summary>读取原形，所有单位共用的原模型保持不变。</summary>
    /// <param name="model">可能已经调整姿态的模型。</param>
    internal static object Original(object model) => originals.TryGetValue(model, out object original) ? original : model;

    /// <summary>取得原身体的实际前后长度。</summary>
    /// <param name="model">实际模型。</param>
    internal static float Length(object model) => Read(maximum.GetValue(model)).y - Read(minimum.GetValue(model)).y;

    /// <summary>按前后位置平滑抬起头颈，后部保持地面；顶点、法线、关节和身体边界一起更新。</summary>
    /// <param name="model">未经调整的外部模型。</param>
    /// <param name="crown">原来安装点。</param>
    /// <param name="height">与原版身体长高比例对应的颅顶高度。</param>
    internal static object Fit(object model, Vector3 crown, float height)
    {
        var key = (model, height);
        if (models.TryGetValue(key, out object cached)) return cached;
        float lift = Mathf.Max(0f, height - crown.z);
        float span = Length(model) * .65f;
        var source = (Array)parts.GetValue(model); var result = Array.CreateInstance(partType, source.Length);
        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
        for (int part = 0; part < source.Length; part++)
        {
            object before = source.GetValue(part), after = Activator.CreateInstance(partType);
            foreach (FieldInfo field in partType.GetFields(BindingFlags.Instance | BindingFlags.Public))
                field.SetValue(after, field.GetValue(before));
            var vertices = (float[])((float[])position.GetValue(before)).Clone();
            var normals = (float[])((float[])normal.GetValue(before)).Clone();
            for (int i = 0; i < vertices.Length; i += 3)
            {
                float t = Mathf.Clamp01((vertices[i + 1] - crown.y) / span);
                float raised = lift * (1f - t * t * (3f - 2f * t));
                float slope = -lift * 6f * t * (1f - t) / span;
                vertices[i + 2] += raised;
                Vector3 n = new Vector3(normals[i], normals[i + 1] - slope * normals[i + 2], normals[i + 2]).normalized;
                normals[i] = n.x; normals[i + 1] = n.y; normals[i + 2] = n.z;
                Vector3 p = new(vertices[i], vertices[i + 1], vertices[i + 2]); min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            Vector3 centre = Read(pivot.GetValue(before)); float along = Mathf.Clamp01((centre.y - crown.y) / span);
            centre.z += lift * (1f - along * along * (3f - 2f * along));
            position.SetValue(after, vertices); normal.SetValue(after, normals); pivot.SetValue(after, Vector(centre));
            version.SetValue(after, (int)version.GetValue(before) + 1); result.SetValue(after, part);
        }
        object fitted = derive.Invoke(model, new[] { AccessTools.Field(model.GetType(), "Name").GetValue(model), result });
        minimum.SetValue(fitted, Vector(min)); maximum.SetValue(fitted, Vector(max));
        parseInfo.Invoke(fitted, new object[] { "height=" + (max.z - Mathf.Max(0f, min.z)).ToString("R", CultureInfo.InvariantCulture) });
        models.Add(key, fitted); originals.Add(fitted, model);
        return fitted;
    }

    /// <summary>安装边界已核对的向量转换。</summary>
    /// <param name="value">外部向量。</param>
    private static Vector3 Read(object value) => new((float)x.GetValue(value), (float)y.GetValue(value), (float)z.GetValue(value));
    /// <summary>只在建立共享静止模型时构造外部值类型。</summary>
    /// <param name="value">共同坐标中的向量。</param>
    private static object Vector(Vector3 value) => Activator.CreateInstance(vectorType, value.x, value.y, value.z);
}
