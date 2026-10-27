#if S3COND2_WORLDFALL
using System;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

/// <summary>为群系模型选用原生树皮和叶片纹理，保留逐面原色与特殊物件分区。</summary>
internal static class WorldfallNatureMaterials
{
    /// <summary>补齐实际资产编号并处理三档和残骸材质，保留网格、槽顺序、原色与发光标记。</summary>
    /// <param name="assetId">正式模型文件中的资产编号。</param>
    /// <param name="model">已读取部件和原色的模型。</param>
    internal static void Apply(string assetId, MeshModel model)
    {
        bool tree = model.Info("cultiway_tree") == "1";
        if (!tree && model.Info("cultiway_plant") != "1") return;
        WorldfallMeshes.SetInfo(model, "cultiway_asset=" + assetId);
        string frame = model.Info("cultiway_frame");
        for (int i = 0; i < model.SlotNames.Length; i++)
            model.SlotKind[i] = tree ? TreeKind(assetId, frame, model.SlotNames[i]) :
                PlantKind(assetId, model.SlotNames[i]);
    }

    /// <summary>按物体真实部件选树皮或叶片，器物与器官保留原有逐面配色。</summary>
    /// <param name="asset">当前树木或群系大物件。</param>
    /// <param name="frame">原贴图外观及残骸编号。</param>
    /// <param name="slot">材质槽名称。</param>
    /// <returns>原生表面类别。</returns>
    private static byte TreeKind(string asset, string frame, string slot)
    {
        switch (asset)
        {
            case "Dark_tree":
                if (slot.StartsWith("uv_wood_", StringComparison.Ordinal)) return MeshKind.Bark;
                if (slot.StartsWith("uv_leaf_", StringComparison.Ordinal)) return MeshKind.Leaves;
                return MeshKind.Plain;
            case "Bamboo_tree":
                if (slot.StartsWith("leaf", StringComparison.Ordinal)) return MeshKind.Leaves;
                if (slot.StartsWith("stem", StringComparison.Ordinal) || slot.StartsWith("root", StringComparison.Ordinal)) return MeshKind.Bark;
                return MeshKind.Plain;
            case "Fern_tree":
                if (slot.Contains("leaf")) return MeshKind.Leaves;
                return slot.StartsWith("bark", StringComparison.Ordinal) ? MeshKind.Bark : MeshKind.Plain;
            case "Oak_tree":
                if (slot.StartsWith("leaf", StringComparison.Ordinal)) return MeshKind.Leaves;
                return slot.StartsWith("bark", StringComparison.Ordinal) ? MeshKind.Bark : MeshKind.Plain;
            case "Rice_tree":
                return slot.StartsWith("leaf", StringComparison.Ordinal) ? MeshKind.Leaves : MeshKind.Plain;
            case "Cemetery_tree":
                if (slot == "moss") return MeshKind.Leaves;
                return slot.StartsWith("branch", StringComparison.Ordinal) ? MeshKind.Bark : MeshKind.Plain;
            case "Nurgle_tree":
                return NurgleTreeKind(frame, slot);
            case "Tzeentch_tree":
                return TzeentchTreeKind(frame, slot);
            case "Khorne_tree":
                return frame is "main_0" or "ruin_0" ? MeshKind.Bark : MeshKind.Plain;
            case "Slaanesh_tree":
                return SlaaneshTreeKind(frame, slot);
            default:
                return MeshKind.Plain;
        }
    }

    /// <summary>按各外观的实际用槽区分纳垢木质、垂冠和器官，共用槽不强加纹理。</summary>
    /// <param name="frame">完整或残骸外观。</param>
    /// <param name="slot">该外观的原图色槽。</param>
    /// <returns>已独立核对的表面类别。</returns>
    private static byte NurgleTreeKind(string frame, string slot) => frame switch
    {
        "main_0" when slot is "pixel_3" or "pixel_5" => MeshKind.Leaves,
        "main_0" when slot == "pixel_6" => MeshKind.Bark,
        "main_2" when slot == "pixel_5" => MeshKind.Bark,
        "main_3" or "main_9" or "ruin_9" when slot == "pixel_4" => MeshKind.Bark,
        "main_4" when slot == "pixel_1" => MeshKind.Bark,
        "main_5" or "ruin_0" or "ruin_4" or "ruin_5" => MeshKind.Bark,
        _ => MeshKind.Plain
    };

    /// <summary>奸奇的青核、圆端和符文与枝条颜色槽分开；只有明确的紫枝和根加树皮。</summary>
    /// <param name="frame">完整或残骸外观。</param>
    /// <param name="slot">已经核对过的精确色槽名。</param>
    /// <returns>树皮或保留逐面细节的普通表面。</returns>
    private static byte TzeentchTreeKind(string frame, string slot) => frame switch
    {
        "main_0" when slot is "rgb_743587" or "rgb_9444ac" or "rgb_542763" => MeshKind.Bark,
        "main_1" when slot is "rgb_8944b5" or "rgb_a351d8" or "rgb_3a1d4d" => MeshKind.Bark,
        "ruin_0" when slot is "rgb_593669" or "rgb_77498d" or "rgb_3a2445" => MeshKind.Bark,
        "ruin_1" when slot is "rgb_443756" or "rgb_5f4e79" or "rgb_282133" => MeshKind.Bark,
        _ => MeshKind.Plain
    };

    /// <summary>色孽冠簇与浅粉树干使用各自明确的色槽，柱、立像与带状物保持原色。</summary>
    /// <param name="frame">完整或残骸外观。</param>
    /// <param name="slot">原图及生成脚本中核对过的槽名。</param>
    /// <returns>树皮、叶片或保留逐面细节的普通表面。</returns>
    private static byte SlaaneshTreeKind(string frame, string slot) => frame switch
    {
        "main_0" when slot is "c_d7458d" or "c_ff52a7" or "c_9b3265" => MeshKind.Leaves,
        "main_0" when slot is "c_c990a9" or "c_dea3b9" or "c_a15e84" => MeshKind.Bark,
        "main_1" when slot is "c_ea4f74" or "c_c44261" or "c_eea4a8" or "c_d89297" or "c_9a6570" or "c_76273a" => MeshKind.Bark,
        "main_2" when slot is "c_bd4e76" or "c_d085a7" or "c_792f62" => MeshKind.Bark,
        "ruin_0" when slot is "c_885e64" or "c_51383b" or "c_bf848c" => MeshKind.Bark,
        "ruin_1" when slot is "c_9f6c76" or "c_c0828f" or "c_3c282c" => MeshKind.Bark,
        "ruin_2" when slot is "c_996d7c" or "c_5f434d" or "c_d496ab" => MeshKind.Bark,
        _ => MeshKind.Plain
    };

    /// <summary>仅给确有叶片或叶鞘的小植被加叶纹，花瓣、菌盖、骨片和蜡质保留原色。</summary>
    /// <param name="asset">花草资产编号。</param>
    /// <param name="slot">原图对应的颜色槽。</param>
    /// <returns>原生表面类别。</returns>
    private static byte PlantKind(string asset, string slot) => asset switch
    {
        "Bamboo" or "Fern" or "Oak" or "Titans" => MeshKind.Leaves,
        "Rice" => slot is "green_shoot" or "dark_stem" or "lime_leaf" ? MeshKind.Leaves : MeshKind.Plain,
        "Coral" => slot.StartsWith("leaf", StringComparison.Ordinal) ? MeshKind.Leaves : MeshKind.Plain,
        "Dark" => slot is "sprout_green" or "stem_muted" ? MeshKind.Leaves : MeshKind.Plain,
        "Knowledge" => slot is "root" or "root_dark" ? MeshKind.Plain : MeshKind.Leaves,
        "Nurgle" => slot.StartsWith("leaf", StringComparison.Ordinal) || slot is "stem" or "stem_dark" ? MeshKind.Leaves : MeshKind.Plain,
        "Tzeentch" => slot is "stem" or "stem_dark" ? MeshKind.Leaves : MeshKind.Plain,
        _ => MeshKind.Plain
    };
}
#endif
