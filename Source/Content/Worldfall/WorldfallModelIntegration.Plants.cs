#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Cultiway.Content.Worldfall;

public sealed partial class WorldfallModelIntegration
{
    /// <summary>花草显示记录中当前选定的模型族。</summary>
    private static FieldInfo PlantMeshFamily;
    /// <summary>花草显示记录是否已选择模型。</summary>
    private static FieldInfo PlantFamilyChecked;
    /// <summary>模型选择对应的实际花草资产。</summary>
    private static FieldInfo PlantFamilyAsset;
    /// <summary>已实际加载的群系花草外观模型。</summary>
    private static Dictionary<string, Dictionary<string, string>> PlantVariants;

    /// <summary>群系花草的正式模型加载后，接入原生花草绘制入口。</summary>
    /// <param name="collector">负责选取场景物体的原生类型。</param>
    private static void InstallPlantVariants(Type collector)
    {
        PlantVariants = BiomeVariantFamilies("cultiway_plant");
        if (PlantVariants.Count == 0) return;
        MethodInfo draw = AccessTools.Method(collector, "DrawPlantModel");
        Type record = draw.GetParameters()[0].ParameterType;
        PlantMeshFamily = AccessTools.Field(record, "MeshFamily");
        PlantFamilyChecked = AccessTools.Field(record, "FamilyChecked");
        PlantFamilyAsset = AccessTools.Field(record, "FamilyAsset");
        ModelHarmony.Patch(draw, prefix: Postfix(nameof(SelectPlantVariant)));
    }

    /// <summary>按实际原图外观编号选取花草，继续由原生入口处理落地、朝向、距离和选择。</summary>
    /// <param name="nb">Worldfall 缓存的花草显示记录。</param>
    /// <param name="b">本次准备绘制的花草。</param>
    private static void SelectPlantVariant(object nb, Building b)
    {
        if (!PlantVariants.TryGetValue(b.asset.id, out Dictionary<string, string> variants) ||
            !variants.TryGetValue("main_" + b.animData_index, out string family)) return;
        if ((string)PlantMeshFamily.GetValue(nb) == family &&
            PlantFamilyAsset.GetValue(nb) == b.asset) return;
        PlantMeshFamily.SetValue(nb, family);
        PlantFamilyChecked.SetValue(nb, true);
        PlantFamilyAsset.SetValue(nb, b.asset);
    }
}
#endif
