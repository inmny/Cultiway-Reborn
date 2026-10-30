#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using FirstPerson.Core;

namespace Cultiway.Content.Worldfall;

public sealed partial class WorldfallModelIntegration
{
    /// <summary>稻田按植被地面绘制，避免 Rice 中的 ice 被原生名称匹配识别为永久雪地。</summary>
    /// <param name="id">采样器当前检查的准确地块编号。</param>
    /// <param name="__result">原生地表材质编号；1 为植被地面，5 为雪地。</param>
    private static void UseRiceGroundMaterial(string id, ref byte __result)
    {
        // Worldfall 1.0.0 用 Contains("ice") 判断雪地；上游改为准确匹配后可删除这项修正。
        if (id is "Rice_low" or "Rice_high") __result = 1;
    }

    /// <summary>将模组叶簇图交给已经初始化的原生贴图缓存。</summary>
    /// <param name="path">叶簇图在模组资源中的路径。</param>
    /// <returns>保留透明孔洞的原生图片。</returns>
    private static SpriteImage ReadLeafCardSprite(string path) =>
        (SpriteImage)ReadOriginalPersonSprite.Invoke(OriginalPersonSprites.GetValue(FirstPerson.WorldBoxMod.Instance),
            [SpriteTextureLoader.getSprite(path)]);

    /// <summary>从已加载的正式模型收集群系外观编号，不按名称猜物体种类。</summary>
    /// <param name="marker">树木或花草模型明确记录的标记。</param>
    /// <returns>资产编号、原图帧名和基础模型族的对应表。</returns>
    private static Dictionary<string, Dictionary<string, string>> BiomeVariantFamilies(string marker)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in BuildingFamilies)
        {
            Dictionary<string, string> variants = new(StringComparer.Ordinal);
            foreach (MeshModel model in CustomModels.Values)
            {
                if (model.Info(marker) != "1" || model.Name.EndsWith("_m", StringComparison.Ordinal) ||
                    model.Name.EndsWith("_f", StringComparison.Ordinal)) continue;
                string name = model.Name.StartsWith("ruin_", StringComparison.Ordinal) ? model.Name.Substring(5) : model.Name;
                if (name != pair.Value && !name.StartsWith(pair.Value + "_v", StringComparison.Ordinal)) continue;
                string frame = model.Info("cultiway_frame");
                if (frame != null) variants.Add(frame, model.Name);
            }
            if (variants.Count > 0) result.Add(pair.Key, variants);
        }
        return result;
    }

}
#endif
