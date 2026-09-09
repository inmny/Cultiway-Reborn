using System;
using Cultiway.Content.CreatureCompositions.Libraries;

namespace Cultiway.Content.UI;

/// <summary>器官展示图标的唯一解析入口：专用小图标优先，其次世界视觉图层贴图。</summary>
public static class YaoOrganIcons
{
    /// <summary>没有世界视觉图层的器官存放专用小图标的目录。</summary>
    private const string IconFolder = "cultiway/icons/organs/";

    /// <summary>解析器官的展示贴图路径；器官无效或两种来源都缺失时返回 null。</summary>
    public static string ResolvePath(CreatureOrganAsset organ)
    {
        if (organ == null) return null;
        string iconPath = IconFolder + organ.id.Replace('.', '_');
        if (SpriteTextureLoader.getSprite(iconPath) != null) return iconPath;

        foreach (string rankId in organ.RankIds ?? Array.Empty<string>())
        {
            CreatureOrganRankAsset rank = Content.Libraries.Manager.CreatureOrganRankLibrary.get(rankId);
            foreach (string layerId in rank?.VisualLayerIds ?? Array.Empty<string>())
            {
                CreatureVisualLayerAsset layer =
                    Content.Libraries.Manager.CreatureVisualLayerLibrary.get(layerId);
                if (layer == null) continue;
                if (!string.IsNullOrEmpty(layer.WildcardSpritePath)) return layer.WildcardSpritePath;
                foreach (CreatureLayerFrame frame in layer.FramesByBaseFrame)
                {
                    if (!string.IsNullOrEmpty(frame.SpritePath)) return frame.SpritePath;
                }
            }
        }

        return null;
    }

    /// <summary>按器官编号解析展示贴图路径；编号不存在时返回 null。</summary>
    public static string ResolvePath(string organId)
    {
        return string.IsNullOrEmpty(organId)
            ? null
            : ResolvePath(Content.Libraries.Manager.CreatureOrganLibrary.get(organId));
    }
}
