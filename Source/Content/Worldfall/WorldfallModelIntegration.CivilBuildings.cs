#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Content.Const;
using FirstPerson.Core;
using HarmonyLib;

namespace Cultiway.Content.Worldfall;

public sealed partial class WorldfallModelIntegration
{
    /// <summary>按建筑、文化风格和原图编号记录已加载的完整建筑模型。</summary>
    private static readonly Dictionary<string, Dictionary<string, Dictionary<int, string>>> CivilBuildingVariants = new(StringComparer.Ordinal);
    /// <summary>原生建筑记录当前选择的模型族。</summary>
    private static FieldInfo CivilMeshFamily;
    /// <summary>原生建筑记录是否已经选择过模型。</summary>
    private static FieldInfo CivilFamilyChecked;
    /// <summary>原生记录对应的建筑资产。</summary>
    private static FieldInfo CivilFamilyAsset;
    /// <summary>模型选择对应的原生资源更新次数。</summary>
    private static FieldInfo CivilFamilyGeneration;
    /// <summary>原生记录使用的绘制方式。</summary>
    private static FieldInfo CivilModelRoute;
    /// <summary>文明建筑应使用的原生建筑绘制方式。</summary>
    private static object CivilBuildingRoute;
    /// <summary>原生城镇资源的更新次数。</summary>
    private static PropertyInfo CivilPacksGeneration;

    /// <summary>正式模型载入后，按原图编号与文化风格接入原生建筑入口。</summary>
    /// <param name="assembly">已核对版本的 Worldfall 程序集。</param>
    /// <param name="collector">原生场景收集类型。</param>
    private static void InstallCivilBuildingVariants(Assembly assembly, Type collector)
    {
        CivilBuildingVariants.Clear();
        foreach (MeshModel model in CustomModels.Values)
        {
            string frame = model.Info("cultiway_frame");
            string style = model.Info("cultiway_style");
            if (style == null || frame?.StartsWith("main_", StringComparison.Ordinal) != true ||
                model.Name.EndsWith("_m", StringComparison.Ordinal) || model.Name.EndsWith("_f", StringComparison.Ordinal)) continue;
            string asset = model.Info("cultiway_asset");
            if (!CivilBuildingVariants.TryGetValue(asset, out Dictionary<string, Dictionary<int, string>> styles))
                CivilBuildingVariants[asset] = styles = new(StringComparer.Ordinal);
            if (!styles.TryGetValue(style, out Dictionary<int, string> variants)) styles[style] = variants = new();
            variants.Add(int.Parse(frame.Substring(5)), model.Name);
        }
        if (CivilBuildingVariants.Count == 0) return;
        MethodInfo draw = AccessTools.Method(collector, "DrawAsModel");
        Type record = draw.GetParameters()[0].ParameterType;
        CivilMeshFamily = AccessTools.Field(record, "MeshFamily");
        CivilFamilyChecked = AccessTools.Field(record, "FamilyChecked");
        CivilFamilyAsset = AccessTools.Field(record, "FamilyAsset");
        CivilFamilyGeneration = AccessTools.Field(record, "FamilyGen");
        CivilModelRoute = AccessTools.Field(record, "Route");
        CivilBuildingRoute = Enum.Parse(CivilModelRoute.FieldType, "Building");
        CivilPacksGeneration = AccessTools.Property(assembly.GetType("FirstPerson.Towns.TownModels"), "Generation");
        ModelHarmony.Patch(draw, prefix: Postfix(nameof(SelectCivilBuildingVariant)));
    }

    /// <summary>文化或外观改变后刷新模型族，保留原生缩放、状态、交互和国家色处理。</summary>
    /// <param name="nb">原生建筑显示记录。</param>
    /// <param name="b">本次绘制的建筑。</param>
    private static void SelectCivilBuildingVariant(object nb, Building b)
    {
        if (!CivilBuildingVariants.ContainsKey(b.asset.id)) return;
        string style = "base";
        Culture culture = b.city?.culture;
        if (culture != null)
        {
            culture.data.get(ContentCultureDataKeys.SkinID_int, out int skin, -1);
            if (EasternHumanSkinStyles.TryGetKey(skin, out string selected)) style = selected;
        }
        string family = CivilBuildingFamily(b.asset.id, style, b.animData_index);
        CivilMeshFamily.SetValue(nb, family);
        CivilFamilyChecked.SetValue(nb, true);
        CivilFamilyAsset.SetValue(nb, b.asset);
        CivilFamilyGeneration.SetValue(nb, CivilPacksGeneration.GetValue(null));
        CivilModelRoute.SetValue(nb, CivilBuildingRoute);
    }

    /// <summary>遵循二维皮肤的整套风格选择，缺少某变体时使用该风格的首个外观。</summary>
    /// <param name="asset">实际建筑编号。</param>
    /// <param name="style">文化当前风格。</param>
    /// <param name="index">原图外观编号。</param>
    /// <returns>已实际加载的模型族名称。</returns>
    private static string CivilBuildingFamily(string asset, string style, int index)
    {
        Dictionary<string, Dictionary<int, string>> styles = CivilBuildingVariants[asset];
        if (!styles.TryGetValue(style, out Dictionary<int, string> variants)) variants = styles["base"];
        return variants.TryGetValue(index, out string family) ? family : variants.OrderBy(pair => pair.Key).First().Value;
    }
}
#endif
