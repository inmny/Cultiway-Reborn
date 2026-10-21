using System;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Core.Visuals.Forms;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Content.Compatibility.Worldfall;

/// <summary>在 Worldfall 的真实三维绘制阶段提交带完整表面资料的形体，不转换为面色槽。</summary>
internal static class FormWorldfallRenderer
{
    /// <summary>该次场景收集已确定的几何、姿态和材质。</summary>
    private static readonly List<(Mesh Mesh, Matrix4x4 Transform, Material Material)> draws = new();
    /// <summary>当前帧真正提交的部件数量。</summary>
    internal static int LastDrawCount { get; private set; }

    /// <summary>当前核对版本的前景绘制在此入口之前已绑定真实地形与人物深度。</summary>
    /// <param name="assembly">实际加载的 Worldfall 程序集。</param>
    internal static void Install(Assembly assembly)
    {
        MethodInfo target = assembly.GetType("FirstPerson.Gpu.GpuRenderer", true)
            .GetMethod("FlareOpen", BindingFlags.Instance | BindingFlags.NonPublic);
        new Harmony("cultiway.forms.worldfall").Patch(target,
            prefix: new HarmonyMethod(typeof(FormWorldfallRenderer), nameof(Draw)));
    }

    /// <summary>从同一发布状态生成本视角的绘制资料，只在这里增加地面承托高度。</summary>
    /// <param name="ground">当前场景使用的地面采样。</param>
    internal static void Prepare(Func<float, float, float> ground)
    {
        draws.Clear();
        float time = (float)World.world.getCurWorldTime();
        foreach (FormVisualInstance instance in FormVisualScene.Instances)
        {
            Vector4 root = instance.Transform.GetColumn(3);
            float height = ground(root.x, root.y);
            for (int part = 0; part < instance.Resource.Meshes.Length; part++)
            {
                Matrix4x4 transform = instance.PartTransform(part);
                transform.m23 += height;
                Material material = FormResources.Surface(instance.Surface ?? instance.Resource.Description.Parts[part].Surface, false);
                material.SetFloat("_WorldTime", time);
                draws.Add((instance.Resource.Meshes[part], transform, material));
            }
        }
    }

    /// <summary>当前目标是 Worldfall 的前景色及共同深度，矩阵与观察者信息由它的同一绘制过程提供。</summary>
    /// <param name="__0">当前帧命令缓冲。</param>
    private static void Draw(CommandBuffer __0)
    {
        LastDrawCount = draws.Count;
        foreach (var draw in draws) __0.DrawMesh(draw.Mesh, draw.Transform, draw.Material, 0, 1);
    }

    /// <summary>清理旧世界的暂存资料。</summary>
    internal static void Clear()
    {
        draws.Clear(); LastDrawCount = 0;
    }
}
