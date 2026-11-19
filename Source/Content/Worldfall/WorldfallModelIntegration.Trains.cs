#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Cultiway.Content.Railways;
using FirstPerson.Core;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>列车每节车体共享二维轨迹，不让三维车头另走一份位置。</summary>
public sealed partial class WorldfallModelIntegration
{
    /// <summary>每个实际车头只复用一组车节实例，销毁角色后可随之释放。</summary>
    private static readonly ConditionalWeakTable<Actor, List<MeshInstance>> trainMeshes = new();
    /// <summary>车头、客车和货车各有近中远三档，所有列车共用已加载的网格。</summary>
    private static readonly MeshModel[,] trainModels = new MeshModel[3, 3];
    /// <summary>读取渲染器正在使用的原有地形，不建立第二份地形数据。</summary>
    private static Func<FirstPerson.WorldBoxMod, TerrainCache> trainTerrain;
    /// <summary>保留原版车头受击区域登记。</summary>
    private static Action<Actor, MeshInstance, bool> recordTrainHitbox;

    /// <summary>核对过当前版本接口后替换列车世界显示，普通生物保持原处理。</summary>
    /// <param name="addCreature">现有生物显示入口。</param>
    private static void InstallTrainConsist(MethodInfo addCreature)
    {
        string[] families = ["cultiway_train_head", "cultiway_train_passenger", "cultiway_train_cargo"];
        string[] suffixes = ["", "_m", "_f"];
        for (int kind = 0; kind < families.Length; kind++)
            for (int level = 0; level < suffixes.Length; level++)
                trainModels[kind, level] = CustomModels[families[kind] + suffixes[level]];
        ActorFamilies[Actors.Train.id] = families[0];
        trainTerrain = (Func<FirstPerson.WorldBoxMod, TerrainCache>)AccessTools.PropertyGetter(typeof(FirstPerson.WorldBoxMod), "SceneTerrain")
            .CreateDelegate(typeof(Func<FirstPerson.WorldBoxMod, TerrainCache>));
        recordTrainHitbox = (Action<Actor, MeshInstance, bool>)AccessTools.Method(addCreature.DeclaringType.Assembly.GetType("FirstPerson.Hitboxes"), "Record")
            .CreateDelegate(typeof(Action<Actor, MeshInstance, bool>));
        ModelHarmony.Patch(addCreature, prefix: new HarmonyMethod(typeof(WorldfallModelIntegration), nameof(DrawTrainConsist))
            { priority = Priority.First });
    }

    /// <summary>按实际编组复用实例，再按相同轨迹逐节定位；站内整列隐藏。</summary>
    /// <param name="meshes">本帧显示列表。</param>
    /// <param name="a">实际单位。</param>
    /// <param name="x">渲染器车头横坐标。</param>
    /// <param name="y">渲染器车头纵坐标。</param>
    /// <param name="z">渲染器车头地面高度。</param>
    /// <param name="distance">原有渲染器到列车的距离。</param>
    /// <param name="tag">整列共用的选择标记。</param>
    /// <param name="flash">受击闪光。</param>
    /// <param name="multiply">原有叠色。</param>
    /// <param name="__result">是否已由列车入口处理；站内隐藏也必须阻止像素后备绘制。</param>
    /// <returns>是否继续普通生物显示。</returns>
    private static bool DrawTrainConsist(List<MeshInstance> meshes, Actor a, float x, float y, float z, float distance,
        int tag, uint flash, uint multiply, ref bool __result)
    {
        if (a.asset != Actors.Train || drawingCreatureFigure) return true;
        // 返回未处理会让收集器把车头贴图再画成方块，即使整列已经按站内规则隐藏。
        __result = true;
        bool recordedHitbox = false;
        var body = a.getActorComponent<ActorComponents.Train>();
        var instances = trainMeshes.GetOrCreateValue(a);
        while (instances.Count <= body.Cars.Count) instances.Add(new MeshInstance());
        body.GetSectionPose(0, out var head, out _, out _);
        var terrain = trainTerrain(FirstPerson.WorldBoxMod.Instance);
        float offset = z - terrain.SampleHeight(x, y);
        int level = distance < 30f ? 0 : distance < 80f ? 1 : 2;
        for (int i = 0; i <= body.Cars.Count; i++)
        {
            body.GetSectionPose(i, out var position, out float angle, out bool shown);
            if (!shown) continue;
            int kind = i == 0 ? 0 : body.Cars[i - 1] == TrainCarKind.Passenger ? 1 : 2;
            var mesh = instances[i];
            mesh.Model = trainModels[kind, level];
            mesh.EnsurePose(); mesh.ResetPose();
            PoseTrainWheels(mesh, body.Distance - ActorComponents.Train.SectionOffset(i));
            float px = x + position.x - head.x, py = y + position.y - head.y;
            mesh.Position = new Vec3(px, py, terrain.SampleHeight(px, py) + offset + WorldfallRailTracks.RailTop);
            mesh.Basis = Mat3.RotZ((angle + 90) * Mathf.Deg2Rad);
            mesh.Tag = tag; mesh.Flash = flash; mesh.Multiply = multiply;
            mesh.NoShadow = false; mesh.ShadowOnly = false; mesh.Hidden = 0;
            mesh.SortDepth = mesh.Position.Z; mesh.KeepSmall = true;
            if (!recordedHitbox)
            {
                recordTrainHitbox(a, mesh, false);
                recordedHitbox = true;
            }
            meshes.Add(mesh);
        }
        return false;
    }

    /// <summary>由真实行驶距离驱动车轮和侧连杆，停车及暂停时不会继续空转。</summary>
    /// <param name="mesh">当前车节和距离档。</param>
    /// <param name="travel">这一车节在实际线路上的进度。</param>
    internal static void PoseTrainWheels(MeshInstance mesh, float travel)
    {
        float driver = mesh.Model.InfoFloat("driver_radius", .4f);
        float phase = travel / driver;
        float crankY = mesh.Model.InfoFloat("crank_y", 0f);
        float crankZ = mesh.Model.InfoFloat("crank_z", 0f);
        for (int i = 0; i < mesh.Model.Parts.Length; i++)
        {
            var part = mesh.Model.Parts[i];
            if (part.Name.StartsWith("wheel_", StringComparison.Ordinal))
                mesh.PartRotation[i] = Mat3.RotX(travel / part.Pivot.Z);
            else if (part.Name.StartsWith("rod_", StringComparison.Ordinal))
                mesh.PartOffset[i] = new Vec3(0f, crankY * Mathf.Cos(phase) - crankZ * Mathf.Sin(phase) - crankY,
                    crankY * Mathf.Sin(phase) + crankZ * Mathf.Cos(phase) - crankZ);
        }
    }
}
#endif
