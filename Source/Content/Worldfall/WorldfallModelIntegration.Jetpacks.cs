#if S3COND2_WORLDFALL
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>让背包单位在水面悬空，尾焰从已建好的实际喷口射出。</summary>
public sealed partial class WorldfallModelIntegration
{
    /// <summary>三种背包各共用一份尾焰几何。</summary>
    private static readonly Dictionary<string, MeshModel> jetpackExhaustModels = new();

    /// <summary>识别只在水面启用背包的单位。</summary>
    /// <param name="asset">单位资产名称。</param>
    /// <returns>是否使用水面喷气背包。</returns>
    private static bool UsesWaterJetpack(string asset) =>
        asset is "Cultiway.Astartes" or "Cultiway.AdeptusCustodes" or "Cultiway.SistersSilence";

    /// <summary>识别移动时连同月亮一起漂浮的玉蟾和月兔。</summary>
    /// <param name="asset">单位资产名称。</param>
    /// <returns>是否采用平稳漂浮。</returns>
    private static bool UsesFloatingMovement(string asset) => asset is "Cultiway.YuChan" or "Cultiway.YueTu";

    /// <summary>读取三种背包自身建模来源里的双喷口坐标，建立有厚度的双色尾焰。</summary>
    /// <param name="asset">背包单位名称。</param>
    /// <returns>与该身体使用相同坐标的尾焰模型。</returns>
    private static MeshModel JetpackExhaustModel(string asset)
    {
        if (jetpackExhaustModels.TryGetValue(asset, out MeshModel model)) return model;
        bool astartes = asset == "Cultiway.Astartes";
        bool sister = asset == "Cultiway.SistersSilence";
        float x = astartes ? 0.29f : 0.145f;
        Vec3 nozzle = astartes ? new Vec3(x, 0.429f, 0.43f) : sister ?
            new Vec3(x, 0.29255f, 0.60795f) : new Vec3(x, 0.314f, 0.445f);
        Vec3 direction = (sister ? new Vec3(0, 0.41f, -0.91f) : astartes ?
            new Vec3(0, 0.022f, -0.172f) : new Vec3(0, 0, -1)).Normalized();
        MeshBuilder builder = new("cultiway_jetpack_exhaust_" + asset);
        int blue = builder.Slot("jet_blue", MeshKind.Plain, TerrainCache.Rgb(70, 167, 255), 1);
        int core = builder.Slot("jet_core", MeshKind.Plain, TerrainCache.Rgb(211, 242, 255), 1);
        for (int side = -1; side <= 1; side += 2)
        {
            Vec3 at = new(side * nozzle.X, nozzle.Y, nozzle.Z);
            builder.Part(side < 0 ? "jet_l" : "jet_r", -1, at);
            builder.Tube(blue, at, at + direction * 0.26f, 0.047f, 0.002f, 6, false);
            builder.Tube(core, at + direction * 0.001f, at + direction * 0.15f, 0.026f, 0.002f, 6, false);
        }
        model = builder.Build();
        jetpackExhaustModels.Add(asset, model);
        return model;
    }

    /// <summary>尾焰跟随身体的朝向、位置和显示比例，附身隐藏自身时也隐藏尾焰。</summary>
    /// <param name="meshes">当前帧绘制列表。</param>
    /// <param name="asset">背包单位名称。</param>
    /// <param name="body">实际身体。</param>
    /// <param name="exhaust">该单位复用的尾焰实例。</param>
    /// <param name="phase">沿用单位连续推进的悬浮周期。</param>
    private static void DrawCreatureJetpack(List<MeshInstance> meshes, string asset, MeshInstance body, MeshInstance exhaust, float phase)
    {
        if (body.Hidden == ulong.MaxValue || body.ShadowOnly) return;
        exhaust.Model ??= JetpackExhaustModel(asset);
        exhaust.EnsurePose();
        exhaust.ResetPose();
        for (int i = 0; i < exhaust.Model.Parts.Length; i++)
            exhaust.PartScale[i] = 0.92f + 0.08f * Mathf.Sin(phase * 19f + i);
        exhaust.Basis = body.Basis;
        exhaust.Position = body.Position;
        exhaust.Tag = body.Tag;
        exhaust.NoShadow = true;
        exhaust.Multiply = body.Multiply;
        exhaust.Fade = body.Fade;
        exhaust.SortDepth = body.SortDepth;
        meshes.Add(exhaust);
    }
}
#endif
