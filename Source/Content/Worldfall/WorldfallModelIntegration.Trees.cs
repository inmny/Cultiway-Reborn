#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Core;
using HarmonyLib;

namespace Cultiway.Content.Worldfall;

public sealed partial class WorldfallModelIntegration
{
    /// <summary>树木显示记录中当前选定的模型族。</summary>
    private static FieldInfo TreeMeshFamily;
    /// <summary>树木显示记录是否已选过模型。</summary>
    private static FieldInfo TreeFamilyChecked;
    /// <summary>模型选择对应的实际建筑资产。</summary>
    private static FieldInfo TreeFamilyAsset;
    /// <summary>模型选择对应的原生资源更新次数。</summary>
    private static FieldInfo TreeFamilyGeneration;
    /// <summary>树木显示记录所用的绘制入口。</summary>
    private static FieldInfo TreeModelRoute;
    /// <summary>原生树木绘制入口的枚举值。</summary>
    private static object PlainTreeRoute;
    /// <summary>Worldfall 当前资源更新次数。</summary>
    private static PropertyInfo TreePacksGeneration;
    /// <summary>成熟黑森林树的放射根下沉量，模型高度为2，此值约占树高6%。</summary>
    private const float MatureDarkTreeRootSink = 0.12f;
    /// <summary>已实际加载的群系树形及残骸模型。</summary>
    private static Dictionary<string, Dictionary<string, string>> TreeVariants;

    /// <summary>群系树木的正式模型已加载后，安装外观与残骸选择。</summary>
    /// <param name="assembly">核对过版本的 Worldfall 程序集。</param>
    /// <param name="collector">负责选取场景物体的原生类型。</param>
    private static void InstallTreeVariants(Assembly assembly, Type collector)
    {
        TreeVariants = BiomeVariantFamilies("cultiway_tree");
        if (TreeVariants.Count == 0) return;
        MethodInfo draw = AccessTools.Method(collector, "DrawAsModel");
        Type record = draw.GetParameters()[0].ParameterType;
        TreeMeshFamily = AccessTools.Field(record, "MeshFamily");
        TreeFamilyChecked = AccessTools.Field(record, "FamilyChecked");
        TreeFamilyAsset = AccessTools.Field(record, "FamilyAsset");
        TreeFamilyGeneration = AccessTools.Field(record, "FamilyGen");
        TreeModelRoute = AccessTools.Field(record, "Route");
        PlainTreeRoute = Enum.ToObject(TreeModelRoute.FieldType, 0);
        TreePacksGeneration = AccessTools.Property(assembly.GetType("FirstPerson.Towns.TownModels"), "Generation");
        ModelHarmony.Patch(draw, prefix: Postfix(nameof(SelectTreeVariant)));
        ModelHarmony.Patch(AccessTools.Method(collector, "WindTree"), prefix: Postfix(nameof(AllowTreeWind)));
        ModelHarmony.Patch(AccessTools.Method(typeof(MeshInstance), nameof(MeshInstance.Place)),
            postfix: Postfix(nameof(LowerMatureDarkTreeRoots)));
    }

    /// <summary>按原版外观编号和枯树状态选择模型，尺寸由原生计算后统一调整。</summary>
    /// <param name="nb">Worldfall 缓存的树木显示记录。</param>
    /// <param name="b">本次准备绘制的树木。</param>
    private static void SelectTreeVariant(object nb, Building b)
    {
        if (!TreeVariants.TryGetValue(b.asset.id, out Dictionary<string, string> variants)) return;
        string frame = (b.isRuin() ? "ruin_" : "main_") + b.animData_index;
        if (!variants.TryGetValue(frame, out string family)) return;
        int generation = (int)TreePacksGeneration.GetValue(null);
        if ((string)TreeMeshFamily.GetValue(nb) == family &&
            (int)TreeFamilyGeneration.GetValue(nb) == generation) return;
        TreeMeshFamily.SetValue(nb, family);
        TreeFamilyChecked.SetValue(nb, true);
        TreeFamilyAsset.SetValue(nb, b.asset);
        TreeFamilyGeneration.SetValue(nb, generation);
        TreeModelRoute.SetValue(nb, PlainTreeRoute);
    }

    /// <summary>残骸和原注册中静止的群系物件不产生树叶或树冠风动。</summary>
    /// <param name="model">本次树木绘制使用的模型。</param>
    /// <returns>是否沿用原生的树冠风动。</returns>
    private static bool AllowTreeWind(MeshModel model) =>
        model?.Info("cultiway_tree") != "1" ||
        (model.Info("cultiway_wind") != "0" &&
         model.Info("cultiway_frame")?.StartsWith("ruin_", StringComparison.Ordinal) != true);

    /// <summary>原生放置重设坐标后，按实际树高埋入成熟树的放射根，避免偏移逐帧累积。</summary>
    /// <param name="__instance">本次放置的模型实例。</param>
    /// <param name="sz">原生入口本次使用的纵向比例。</param>
    private static void LowerMatureDarkTreeRoots(MeshInstance __instance, float sz)
    {
        if (__instance.Model?.Name is not ("building_cultiway_Dark_tree" or
            "building_cultiway_Dark_tree_m" or "building_cultiway_Dark_tree_f") ||
            __instance.Model.Info("cultiway_tree") != "1") return;
        __instance.Position.Z -= MatureDarkTreeRootSink * sz;
    }
}
#endif
