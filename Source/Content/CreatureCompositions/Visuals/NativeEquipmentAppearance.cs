using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.CreatureCompositions.Visuals;

/// <summary>给原版像素身体合入真实穿戴的甲片，同时保留每个可见像素的装备归属。</summary>
internal static class NativeEquipmentAppearance
{
    /// <summary>同一着色帧与同一装备外观共用图片；不为每个人单独创建纹理。</summary>
    private static readonly Dictionary<(int body, string equipment), CreatureAppearanceFrame> frames = new();

    /// <summary>按创建顺序限制缓存，沿用身体合成的容量边界。</summary>
    private static readonly Queue<(int body, string equipment)> order = new();

    /// <summary>制作底稿导出的像素样式，读取后只保留数值。</summary>
    private static readonly Dictionary<string, Pattern> patterns = new(StringComparer.Ordinal);

    /// <summary>先检查实际穿戴，再按原动画位置合成；没有可见甲片时保留原图片。</summary>
    /// <param name="actor">实际穿戴者。</param><param name="body">原版已着色的最终帧。</param>
    /// <param name="native">对应未着色帧。</param>
    internal static CreatureAppearanceFrame ComposeFrame(Actor actor, Sprite body, Sprite native)
    {
        if (body == null || native == null || !actor.asset.need_colored_sprite || !actor.isSapient() || actor.isBaby())
            return null;
        Item helmet = actor.equipment?.helmet?.getItem(), chest = actor.equipment?.armor?.getItem(),
            boots = actor.equipment?.boots?.getItem();
        if (helmet == null && chest == null && boots == null) return null;
        AnimationFrameData frame = actor.getAnimationFrameData();
        Sprite head = frame?.show_head == true ? actor.cached_sprite_head : null;
        string equipment = Style(helmet) + "|" + Style(chest) + "|" + Style(boots) + "|" +
                           (head == null ? 0 : head.GetInstanceID()) + "|" + native.GetInstanceID();
        var key = (body.GetInstanceID(), equipment);
        if (frames.TryGetValue(key, out CreatureAppearanceFrame cached)) return cached;

        // 原版图集先完成本轮上传，再读取同一最终图片；此入口只在主线程调用。
        AssetManager.dynamic_sprites_library.checkDirty();
        Color32[] pixels = CreatureAppearanceService.ReadPixels(body);
        int width = Mathf.RoundToInt(body.rect.width), height = Mathf.RoundToInt(body.rect.height);
        int dx = Mathf.RoundToInt(body.pivot.x - native.pivot.x),
            dy = Mathf.RoundToInt(body.pivot.y - native.pivot.y);
        var owners = new int[pixels.Length];
        Color32[] headPixels = head == null ? null : CreatureAppearanceService.ReadPixels(head);
        int headWidth = head == null ? 0 : Mathf.RoundToInt(head.rect.width),
            headHeight = head == null ? 0 : Mathf.RoundToInt(head.rect.height),
            hx = head == null ? 0 : (int)frame.pos_head_new.x - (int)head.pivot.x + dx,
            hy = head == null ? 0 : (int)frame.pos_head_new.y - (int)head.pivot.y + dy;

        // 成年人形walk资源共用10x10制作位置；游泳、幼体和其他尺寸不套用地面足部。
        if (native.rect.width == 10 && native.rect.height == 10 && native.name is "walk_0" or "walk_3")
        {
            Pattern pattern = GetPattern(native.name == "walk_3" ? "body-wide" : "body");
            Color32[] original = CreatureAppearanceService.ReadPixels(native);
            for (int y = 0; y < pattern.Height; y++)
            for (int x = 0; x < pattern.Width; x++)
            {
                Color32 mark = pattern.Pixels[y * pattern.Width + x];
                if (mark.a == 0 || original[y * pattern.Width + x].a == 0 || InHead(x + dx, y + dy)) continue;
                if (mark.r != 0 && chest != null) Paint(x + dx, y + dy, 2, mark.r, chest);
                else if (mark.b != 0 && boots != null) Paint(x + dx, y + dy, 3, mark.b, boots);
            }
        }

        if (head != null && helmet != null)
        {
            string path = actor.getTextureAsset().texture_head_warrior;
            string race = path?.Split('/').Reverse().Skip(1).FirstOrDefault();
            bool warrior = head.name == "head_warrior" && race is "human" or "elf" or "dwarf" or "orc";
            Pattern pattern = GetPattern(warrior ? "helmet-" + race : "helmet-cap");
            int capBottom = Math.Max(0, headHeight - pattern.Height);
            if (!warrior)
                for (int y = 0; y < headHeight; y++)
                for (int x = 0; x < headWidth; x++)
                {
                    Color32 color = headPixels[y * headWidth + x];
                    if (color.a != 0 && (Toolbox.areColorsEqual(color, Toolbox.color_phenotype_green_0) ||
                                        Toolbox.areColorsEqual(color, Toolbox.color_phenotype_green_1) ||
                                        Toolbox.areColorsEqual(color, Toolbox.color_phenotype_green_2) ||
                                        Toolbox.areColorsEqual(color, Toolbox.color_phenotype_green_3)))
                        capBottom = Math.Max(capBottom, y + 1);
                }
            if (!warrior || pattern.Width == headWidth && pattern.Height == headHeight)
                for (int y = 0; y < pattern.Height; y++)
                for (int x = 0; x < headWidth; x++)
                {
                    if (warrior && headPixels[y * headWidth + x].a == 0) continue;
                    int px = warrior ? x : Mathf.Min(pattern.Width - 1, x * pattern.Width / headWidth);
                    Color32 mark = pattern.Pixels[y * pattern.Width + px];
                    if (mark.a != 0) Paint(hx + x, hy + (warrior ? y : capBottom + y), 1, mark.r, helmet);
                }
        }

        if (!owners.Any(owner => owner != 0)) return null;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "cultiway_equipment_" + body.name, filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite image = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(body.pivot.x / width, body.pivot.y / height), body.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        image.name = texture.name;
        var result = new CreatureAppearanceFrame(image, Array.ConvertAll(pixels, pixel => pixel.a), owners,
            new[] { new string[] { null }, new[] { "armor.helmet" }, new[] { "armor.chest" }, new[] { "armor.boots" } });
        if (frames.Count >= CreatureAppearanceService.MaximumCachedFrames)
        {
            var oldest = order.Dequeue();
            Destroy(frames[oldest]);
            frames.Remove(oldest);
        }

        frames.Add(key, result);
        order.Enqueue(key);
        return result;

        /// <summary>身体甲片不能盖住原本在前面的脸、头发或头盔。</summary>
        /// <param name="x">最终帧横向像素。</param><param name="y">最终帧竖向像素。</param>
        bool InHead(int x, int y) => head != null && x >= hx && x < hx + headWidth && y >= hy && y < hy + headHeight &&
                                    headPixels[(y - hy) * headWidth + x - hx].a != 0;

        /// <summary>只改实际可见像素，着色和归属在同一次写入中完成。</summary>
        /// <param name="x">实际横向像素。</param><param name="y">实际竖向像素。</param>
        /// <param name="owner">装备归属。</param><param name="shade">制作明暗。</param><param name="item">穿戴物。</param>
        void Paint(int x, int y, int owner, byte shade, Item item)
        {
            if (x < 0 || x >= width || y < 0 || y >= height || owner != 1 && pixels[y * width + x].a == 0) return;
            Color32 color = Metal(item);
            float light = shade / 192f * (item.isBroken() ? .5f : 1f);
            int index = y * width + x;
            pixels[index] = new Color32((byte)Mathf.Min(255, color.r * light),
                (byte)Mathf.Min(255, color.g * light), (byte)Mathf.Min(255, color.b * light),
                pixels[index].a == 0 ? (byte)255 : pixels[index].a);
            owners[index] = owner;
        }
    }

    /// <summary>缓存只区分确实影响外观的资源、材料与破损，不使用人物或物品编号。</summary>
    /// <param name="item">实际穿戴物。</param>
    private static string Style(Item item) => item == null ? "" : item.getAsset().id + ":" + item.data.material + ":" + item.isBroken();

    /// <summary>装备使用自身材料色，制作明暗独立保留，不借用王国衣服颜色冒充甲片。</summary>
    /// <param name="item">实际穿戴物。</param>
    private static Color32 Metal(Item item)
    {
        string material = string.IsNullOrEmpty(item.data.material) ? item.getAsset().id : item.data.material;
        if (material.Contains("adamantine")) return new Color32(123, 173, 194, 255);
        if (material.Contains("bronze")) return new Color32(187, 139, 82, 255);
        if (material.Contains("copper")) return new Color32(188, 112, 83, 255);
        if (material.Contains("gold")) return new Color32(214, 178, 85, 255);
        if (material.Contains("silver")) return new Color32(192, 203, 214, 255);
        return new Color32(157, 170, 183, 255);
    }

    /// <summary>从像素文字底稿导出的PNG读取样式；读取纹理立即释放。</summary>
    /// <param name="name">发布样式名称。</param>
    private static Pattern GetPattern(string name)
    {
        if (patterns.TryGetValue(name, out Pattern pattern)) return pattern;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "BodyEquipment", name + ".png");
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
                throw new InvalidDataException("装备像素样式无法读取：" + path);
            pattern = new Pattern(texture.GetPixels32(), texture.width, texture.height);
            patterns.Add(name, pattern);
            return pattern;
        }
        finally { Object.Destroy(texture); }
    }

    /// <summary>世界清理和资源重载沿现有身体生命周期释放图片。</summary>
    internal static void ClearWorldState()
    {
        foreach (CreatureAppearanceFrame frame in frames.Values) Destroy(frame);
        frames.Clear();
        order.Clear();
        patterns.Clear();
    }

    /// <summary>仅释放本服务拥有的图片。</summary>
    /// <param name="frame">生成帧。</param>
    private static void Destroy(CreatureAppearanceFrame frame)
    {
        Object.Destroy(frame.Sprite.texture);
        Object.Destroy(frame.Sprite);
    }

    /// <summary>制作样式的只读像素与尺寸。</summary>
    private sealed class Pattern
    {
        /// <summary>原图像素。</summary>
        internal readonly Color32[] Pixels;
        /// <summary>制作宽高。</summary>
        internal readonly int Width, Height;
        /// <summary>保存数值，不保留加载纹理。</summary>
        /// <param name="pixels">像素。</param><param name="width">宽。</param><param name="height">高。</param>
        internal Pattern(Color32[] pixels, int width, int height) { Pixels = pixels; Width = width; Height = height; }
    }
}
