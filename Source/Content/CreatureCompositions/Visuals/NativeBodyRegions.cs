using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Cultiway.Core.Skills;
using Object = UnityEngine.Object;

namespace Cultiway.Content.CreatureCompositions.Visuals;

/// <summary>原版自然帧的实际部位映射，缺失掌部仅在制作资料声明的位置补入实际肤色。</summary>
internal static class NativeBodyRegions
{
    /// <summary>已检查的制作位置及逐帧归属。</summary>
    private static Definition definition;

    /// <summary>相同帧直接复用展开后的名称，不逐人物解析文字行。</summary>
    private static readonly Dictionary<string, string[]> frames = new(StringComparer.Ordinal);

    /// <summary>缺失掌部和臂链接管共用有限图片缓存，相同来源和用途只生成一次。</summary>
    private static readonly Dictionary<(int sprite, BodyOccupancy arms), CreatureAppearanceFrame> cutouts = new();

    /// <summary>有限图片缓存的创建顺序。</summary>
    private static readonly Queue<(int sprite, BodyOccupancy arms)> cutoutOrder = new();

    /// <summary>只有制作尺寸和帧名都匹配才采用本映射，其他身体继续自己的接入。</summary>
    /// <param name="actor">实际身体。</param><param name="native">原动画帧。</param>
    internal static string[] Get(Actor actor, Sprite native)
    {
        if (!actor.asset.is_humanoid) return NativeCreatureRegions.Get(actor, native)?.Regions;
        if (actor.isBaby()) return null;
        if (definition == null) Load();
        string profile = Profile(actor, native);
        if (native.rect.width != definition.Width || native.rect.height != definition.Height ||
            !definition.Frames.TryGetValue(profile, out string[] rows)) return null;
        if (frames.TryGetValue(profile, out string[] cached)) return cached;
        var result = new string[definition.Width * definition.Height];
        for (int y = 0; y < definition.Height; y++)
        for (int x = 0; x < definition.Width; x++)
            result[y * definition.Width + x] = definition.Roles[rows[definition.Height - 1 - y][x].ToString()];
        frames.Add(profile, result);
        return result;
    }

    /// <summary>少数衣装确实采用另一种抬手姿态，按原族与原纹理选择已制作的表。</summary>
    /// <param name="actor">实际人物。</param><param name="native">原帧。</param>
    private static string Profile(Actor actor, Sprite native) =>
        definition.Variants.TryGetValue(actor.asset.id + "/" + native.texture.name, out var variants) &&
        variants.TryGetValue(native.name, out string selected) ? selected : native.name;

    /// <summary>掌足继续属于原前臂和小腿，选择规则与三维真实模型一致。</summary>
    /// <param name="region">实际像素部位。</param>
    internal static string Parent(string region) => region switch
    {
        "hand.left" => "arm.left.lower", "hand.right" => "arm.right.lower",
        "foot.left" => "leg.left.lower", "foot.right" => "leg.right.lower", _ => null
    };

    /// <summary>部分衣装省略了抬起的掌部；只在声明位置确实空白时补入实际肤色像素。</summary>
    /// <param name="actor">真实人物。</param><param name="body">已着色的原图。</param><param name="native">原动画帧。</param>
    internal static CreatureAppearanceFrame CompleteHands(Actor actor, Sprite body, Sprite native)
    {
        if (!actor.asset.is_humanoid) return null;
        if (body == null || native == null || !actor.asset.need_colored_sprite || Get(actor, native) == null ||
            !definition.MissingHandPixels.TryGetValue(Profile(actor, native), out int[][] locations)) return null;
        var key = (body.GetInstanceID(), BodyOccupancy.None);
        if (cutouts.TryGetValue(key, out CreatureAppearanceFrame cached)) return cached;
        AssetManager.dynamic_sprites_library.checkDirty();
        Color32[] pixels = CreatureAppearanceService.ReadPixels(body);
        int width = Mathf.RoundToInt(body.rect.width), height = Mathf.RoundToInt(body.rect.height),
            dx = Mathf.RoundToInt(body.pivot.x - native.pivot.x), dy = Mathf.RoundToInt(body.pivot.y - native.pivot.y);
        var missing = locations.Select(point => (x: point[0] + dx, y: point[1] + dy))
            .Where(point => point.x >= 0 && point.x < width && point.y >= 0 && point.y < height &&
                            pixels[point.y * width + point.x].a == 0).ToArray();
        Sprite head = actor.cached_sprite_head;
        if (missing.Length == 0 || head == null) return Remember(key, null);
        Color32[] source = CreatureAppearanceService.ReadPixels(head);
        AnimationFrameData frame = actor.getAnimationFrameData();
        int hx = (int)frame.pos_head_new.x - (int)head.pivot.x + dx,
            hy = (int)frame.pos_head_new.y - (int)head.pivot.y + dy,
            headWidth = Mathf.RoundToInt(head.rect.width);
        int skin = Array.FindIndex(source, color => color.a != 0 &&
            Toolbox.areColorsEqual(color, Toolbox.color_phenotype_green_0));
        if (skin < 0) return Remember(key, null);
        int sx = skin % headWidth + hx, sy = skin / headWidth + hy;
        if (sx < 0 || sx >= width || sy < 0 || sy >= height) return Remember(key, null);
        Color32 color = pixels[sy * width + sx];
        foreach (var point in missing) pixels[point.y * width + point.x] = color;
        return Remember(key, Image(body, pixels, width, height, "cultiway_body_hands_", null));
    }

    /// <summary>动作臂链独立显示时移除原图里的同侧旧手臂，器官随后仍按原顺序合成。</summary>
    /// <param name="actor">实际身体。</param><param name="basis">已有装备归属。</param>
    /// <param name="body">实际基础图片。</param><param name="native">对应原帧。</param>
    /// <param name="occupancy">同轮接管部位。</param>
    internal static CreatureAppearanceFrame RemoveArms(Actor actor, CreatureAppearanceFrame basis, Sprite body,
        Sprite native, BodyOccupancy occupancy)
    {
        BodyOccupancy arms = occupancy & (BodyOccupancy.LeftArm | BodyOccupancy.RightArm);
        if (body == null || native == null || arms == BodyOccupancy.None) return null;
        string[] regions = Get(actor, native);
        if (regions == null) return null;
        var key = (body.GetInstanceID(), arms);
        if (cutouts.TryGetValue(key, out CreatureAppearanceFrame cached)) return cached;
        // 新着色帧先写在原版图集数组中，裁切不能读到尚未上传区域里的旧颜色。
        AssetManager.dynamic_sprites_library.checkDirty();
        Color32[] pixels = CreatureAppearanceService.ReadPixels(body);
        int width = Mathf.RoundToInt(body.rect.width), height = Mathf.RoundToInt(body.rect.height);
        foreach (int pixel in ReplacedPixels(body, native, regions, arms)) pixels[pixel] = default;
        return Remember(key, Image(body, pixels, width, height, "cultiway_body_action_", basis));
    }

    /// <summary>同次像素修改与透明度一起发布，头部和原装备归属保持不变。</summary>
    /// <param name="body">原图。</param><param name="pixels">真正绘制像素。</param><param name="width">宽。</param>
    /// <param name="height">高。</param><param name="prefix">用途名称。</param><param name="basis">已有归属。</param>
    private static CreatureAppearanceFrame Image(Sprite body, Color32[] pixels, int width, int height,
        string prefix, CreatureAppearanceFrame basis)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = prefix + body.name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(body.pivot.x / width, body.pivot.y / height), body.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        return new CreatureAppearanceFrame(sprite, Array.ConvertAll(pixels, pixel => pixel.a),
            basis?.Owners ?? new int[pixels.Length], basis?.Layers ?? new[] { new string[] { null } });
    }

    /// <summary>没有缺失像素也缓存结果，避免每帧重复读取同一来源；全部缓存仍有共同上限。</summary>
    /// <param name="key">原图与实际用途。</param><param name="result">生成图或无需修改。</param>
    private static CreatureAppearanceFrame Remember((int sprite, BodyOccupancy arms) key, CreatureAppearanceFrame result)
    {
        if (cutouts.Count >= CreatureAppearanceService.MaximumCachedFrames)
        {
            var oldest = cutoutOrder.Dequeue();
            Destroy(cutouts[oldest]);
            cutouts.Remove(oldest);
        }
        cutouts.Add(key, result);
        cutoutOrder.Enqueue(key);
        return result;
    }

    /// <summary>实际遮挡沿同一部位裁切，已经移除的旧手臂不能继续挡住持物。</summary>
    /// <param name="actor">实际身体。</param><param name="body">原最终图。</param><param name="native">原动画帧。</param>
    /// <param name="alpha">原透明度。</param><param name="arms">实际接管。</param>
    internal static byte[] CutAlpha(Actor actor, Sprite body, Sprite native, byte[] alpha, BodyOccupancy arms)
    {
        string[] regions = Get(actor, native);
        if (regions == null) return alpha;
        byte[] result = (byte[])alpha.Clone();
        foreach (int pixel in ReplacedPixels(body, native, regions, arms)) result[pixel] = 0;
        return result;
    }

    /// <summary>显示和遮挡使用同一份实际像素位置。</summary>
    /// <param name="body">最终身体画布。</param><param name="native">对应原帧。</param>
    /// <param name="regions">已经展开的制作归属。</param><param name="arms">实际接管。</param>
    private static IEnumerable<int> ReplacedPixels(Sprite body, Sprite native, string[] regions, BodyOccupancy arms)
    {
        int width = Mathf.RoundToInt(body.rect.width), height = Mathf.RoundToInt(body.rect.height),
            nativeWidth = Mathf.RoundToInt(native.rect.width), nativeHeight = Mathf.RoundToInt(native.rect.height),
            dx = Mathf.RoundToInt(body.pivot.x - native.pivot.x), dy = Mathf.RoundToInt(body.pivot.y - native.pivot.y);
        for (int y = 0; y < nativeHeight; y++)
        for (int x = 0; x < nativeWidth; x++)
        {
            int px = x + dx, py = y + dy;
            if (px >= 0 && px < width && py >= 0 && py < height &&
                BodySurfaceComposition.Replaces(regions[y * nativeWidth + x], arms)) yield return py * width + px;
        }
    }

    /// <summary>资源入口一次检查尺寸和字符；实际采样不重复做相同校验。</summary>
    private static void Load()
    {
        string path = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "BodyVisuals", "humanoid-regions.json");
        Definition data = JsonConvert.DeserializeObject<Definition>(File.ReadAllText(path));
        if (data.Width <= 0 || data.Height <= 0 || data.Frames.Any(frame => frame.Value.Length != data.Height ||
                frame.Value.Any(row => row.Length != data.Width || row.Any(c => !data.Roles.ContainsKey(c.ToString())))))
            throw new InvalidDataException("人物部位映射的尺寸或字符不正确：" + path);
        if (data.MissingHandPixels.Any(frame => !data.Frames.ContainsKey(frame.Key) || frame.Value.Any(p =>
                p.Length != 2 || p[0] < 0 || p[0] >= data.Width || p[1] < 0 || p[1] >= data.Height ||
                !data.Roles[data.Frames[frame.Key][data.Height - 1 - p[1]][p[0]].ToString()].StartsWith("hand.", StringComparison.Ordinal))))
            throw new InvalidDataException("补充掌部像素必须位于已声明的手掌内：" + path);
        if (data.Variants.Values.Any(variant => variant.Values.Any(frame => !data.Frames.ContainsKey(frame))))
            throw new InvalidDataException("衣装姿态必须指向已有的部位制作表：" + path);
        definition = data;
    }

    /// <summary>世界清理和资源重载后重新读取制作资料。</summary>
    internal static void Clear()
    {
        foreach (CreatureAppearanceFrame frame in cutouts.Values) Destroy(frame);
        cutouts.Clear();
        cutoutOrder.Clear();
        definition = null;
        frames.Clear();
        NativeCreatureRegions.Clear();
    }

    /// <summary>只释放本服务创建的裁切图片。</summary>
    /// <param name="frame">实际裁切帧。</param>
    private static void Destroy(CreatureAppearanceFrame frame)
    {
        if (frame == null) return;
        Object.Destroy(frame.Sprite.texture);
        Object.Destroy(frame.Sprite);
    }

    /// <summary>原帧尺寸、名称和文字行，文字行从图的上方向下排列。</summary>
    private sealed class Definition
    {
        /// <summary>原帧宽。</summary>
        public int Width { get; set; }
        /// <summary>原帧高。</summary>
        public int Height { get; set; }
        /// <summary>每个字符对应的真实部位名。</summary>
        public Dictionary<string, string> Roles { get; set; }
        /// <summary>按原帧名称读取的部位行。</summary>
        public Dictionary<string, string[]> Frames { get; set; }

        /// <summary>族与纹理名称对应的特定姿态，不按人物创建重复资料。</summary>
        public Dictionary<string, Dictionary<string, string>> Variants { get; set; }

        /// <summary>原衣装确实省略时才补画的掌部位置。</summary>
        public Dictionary<string, int[][]> MissingHandPixels { get; set; }
    }
}
