using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>将真实装备的像素轮廓挤出为薄实体，保留材质色与透明缺口。</summary>
internal static class SpatialSpriteMesh
{
    /// <summary>按真实精灵缓存局部模型。</summary>
    private static readonly Dictionary<Sprite, SpatialVisualMesh> models = new();

    /// <summary>清理当前世界的装备模型引用。</summary>
    internal static void Clear() => models.Clear();

    /// <summary>读取当前精灵，而非按武器类型凭空替换外形。</summary>
    /// <param name="sprite">当前真实装备精灵。</param>
    internal static SpatialVisualMesh Get(Sprite sprite)
    {
        if (models.TryGetValue(sprite, out SpatialVisualMesh model)) return model;
        Rect rect = sprite.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        Color32[] pixels = ReadPixels(sprite, width, height);
        Vector2 offset = sprite.textureRectOffset - sprite.pivot;
        float unit = 1f / sprite.pixelsPerUnit;
        float depth = unit * 0.45f;
        SpatialVisualMeshBuilder builder = new();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color32 color = pixels[y * width + x];
            if (color.a < 128) continue;
            color.a = 255;
            float left = (x + offset.x) * unit;
            float bottom = (y + offset.y) * unit;
            Vector3 a = new(left, bottom, -depth);
            Vector3 b = new(left + unit, bottom, -depth);
            Vector3 c = new(left + unit, bottom + unit, -depth);
            Vector3 d = new(left, bottom + unit, -depth);
            Vector3 lift = new(0f, 0f, depth * 2f);
            builder.AddFace(new[] { d, c, b, a }, color);
            builder.AddFace(new[] { a + lift, b + lift, c + lift, d + lift }, color);
            if (x == 0 || pixels[y * width + x - 1].a < 128)
                builder.AddFace(new[] { a, d, d + lift, a + lift }, color);
            if (x == width - 1 || pixels[y * width + x + 1].a < 128)
                builder.AddFace(new[] { c, b, b + lift, c + lift }, color);
            if (y == 0 || pixels[(y - 1) * width + x].a < 128)
                builder.AddFace(new[] { b, a, a + lift, b + lift }, color);
            if (y == height - 1 || pixels[(y + 1) * width + x].a < 128)
                builder.AddFace(new[] { d, c, c + lift, d + lift }, color);
        }
        model = builder.Build(sprite.name);
        models.Add(sprite, model);
        return model;
    }

    /// <summary>只复制精灵所在的小区域；不可读图集通过显卡复制一次后读取。</summary>
    /// <param name="sprite">图集中需要读取的精灵。</param>
    /// <param name="width">该区域宽度。</param>
    /// <param name="height">该区域高度。</param>
    private static Color32[] ReadPixels(Sprite sprite, int width, int height)
    {
        Texture2D texture = sprite.texture;
        Rect rect = sprite.textureRect;
        if (texture.isReadable)
            return System.Array.ConvertAll(texture.GetPixels((int)rect.x, (int)rect.y, width, height), color => (Color32)color);

        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Texture2D copy = new(width, height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, temporary,
                new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = temporary;
            copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            copy.Apply();
            return copy.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            Object.Destroy(copy);
        }
    }
}
