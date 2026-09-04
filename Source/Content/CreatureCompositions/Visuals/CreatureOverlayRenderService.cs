using System;
using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Models;
using UnityEngine;

namespace Cultiway.Content.CreatureCompositions.Visuals;

/// <summary>器官附加图层的静态解析、图片缓存与轮廓遮罩合成；绘制对象由渲染系统自持。</summary>
public static class CreatureOverlayRenderService
{
    private static readonly Dictionary<string, Sprite> spriteCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<(string layerId, int spriteId, Color tint), Sprite> maskedCache =
        new();
    private static readonly HashSet<string> failedMasks = new();

    /// <summary>读取当前身体整理出的全部附加图层定义；身体没有图层时返回假。</summary>
    public static bool TryResolveLayers(
        CompiledCreaturePhenotype compiled, string actorAssetId, List<CreatureVisualLayerAsset> output)
    {
        output.Clear();
        if (compiled.VisualLayers.Count == 0) return false;

        CreatureVisualRigAsset rig = Content.Libraries.Manager.CreatureVisualRigLibrary.get(compiled.Morph.VisualRigId);
        string rigId = rig != null && rig.SupportsActor(actorAssetId) ? rig.id : null;

        foreach (CompiledCreatureVisualLayer channelLayer in compiled.VisualLayers)
        {
            foreach (string layerId in channelLayer.LayerIds)
            {
                CreatureVisualLayerAsset layer =
                    Content.Libraries.Manager.CreatureVisualLayerLibrary.get(layerId);
                if (layer == null) continue;
                if (rigId != null && !layer.SupportsRig(rigId)) continue;
                output.Add(layer);
            }
        }

        return output.Count > 0;
    }

    /// <summary>按主体当前帧名称读取图层帧图片；路径只解析一次并长期缓存。</summary>
    public static bool TryGetFrameSprite(CreatureVisualLayerAsset layer, string baseFrameName, out Sprite sprite)
    {
        if (!layer.TryGetFramePath(baseFrameName, out string path))
        {
            sprite = null;
            return false;
        }

        if (spriteCache.TryGetValue(path, out sprite)) return sprite != null;

        sprite = SpriteTextureLoader.getSprite(path);
        spriteCache[path] = sprite;
        return sprite != null;
    }

    /// <summary>
    ///     生成按主体轮廓遮罩的淡染精灵：图层纹理平铺后与主体精灵的像素轮廓相乘，
    ///     只保留身体范围内的部分。同一图层与同一主体帧的合成结果长期缓存。
    /// </summary>
    public static bool TryGetMaskedSprite(
        CreatureVisualLayerAsset layer, Sprite mainSprite, Color tint, out Sprite masked)
    {
        masked = null;
        if (layer.WildcardSpritePath == null || mainSprite == null) return false;
        if (!spriteCache.TryGetValue(layer.WildcardSpritePath, out Sprite pattern) || pattern == null)
            return false;

        var key = (layer.id, mainSprite.GetInstanceID(), tint);
        if (maskedCache.TryGetValue(key, out masked)) return masked != null;
        if (failedMasks.Contains(layer.id + mainSprite.GetInstanceID())) return false;

        // 淡染纹理必须能读到像素；原版图集不可读时记录失败避免每帧重试刷日志。
        try
        {
            masked = BuildMaskedSprite(layer, mainSprite, pattern, tint);
        }
        catch (Exception e)
        {
            ModClass.LogWarning($"淡染图层 {layer.id} 无法读取主体像素，已跳过: {e.Message}");
            failedMasks.Add(layer.id + mainSprite.GetInstanceID());
            return false;
        }

        maskedCache[key] = masked;
        return true;
    }

    /// <summary>把淡染纹理平铺进主体精灵的矩形，逐像素相乘轮廓透明度后生成同参数精灵。</summary>
    private static Sprite BuildMaskedSprite(
        CreatureVisualLayerAsset layer, Sprite mainSprite, Sprite pattern, Color tint)
    {
        Rect rect = mainSprite.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        Color[] body = mainSprite.texture.GetPixels(
            Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
        Color32[] patternPixels = pattern.texture.GetPixels32();
        int patternWidth = pattern.texture.width;
        int patternHeight = pattern.texture.height;

        var output = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color bodyPixel = body[y * width + x];
                Color32 patternPixel = patternPixels[(y % patternHeight) * patternWidth + (x % patternWidth)];
                byte alpha = (byte)(bodyPixel.a * patternPixel.a * 255f);
                output[y * width + x] = new Color32(
                    (byte)(tint.r * 255f),
                    (byte)(tint.g * 255f),
                    (byte)(tint.b * 255f),
                    alpha);
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };
        texture.SetPixels32(output);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            mainSprite.pivot,
            mainSprite.pixelsPerUnit);
    }

    /// <summary>清空图片缓存；静态图层定义保持不变。</summary>
    public static void ClearWorldState()
    {
        spriteCache.Clear();
        maskedCache.Clear();
        failedMasks.Clear();
    }
}
