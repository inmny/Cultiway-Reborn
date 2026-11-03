using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Content.CreatureCompositions.Libraries;
using Cultiway.Content.CreatureCompositions.Models;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.CreatureCompositions.Visuals;

/// <summary>从完整身体挑选合理的外观，并把部件合进原版当前帧；不改变器官、亚种或能力。</summary>
public static class CreatureAppearanceService
{
    /// <summary>只保留最近使用的有限张合成帧，避免不同身体组合无限占用图片内存。</summary>
    public const int MaximumCachedFrames = 2048;

    /// <summary>已选出的外观方案，键与当前世界的身体整理编号一致。</summary>
    private static readonly Dictionary<int, AppearancePlan> plans = new();

    /// <summary>相同主体帧与相同可见部件共用一张图片。</summary>
    private static readonly Dictionary<(int sprite, string appearance), Sprite> frames = new();

    /// <summary>按生成顺序淘汰图片，不在每帧重排缓存。</summary>
    private static readonly Queue<(int sprite, string appearance)> frameOrder = new();

    /// <summary>部件原图由加载器拥有，本服务只保留引用。</summary>
    private static readonly Dictionary<string, Sprite> parts = new(StringComparer.Ordinal);

    /// <summary>当前世界已经生成的合成帧数量，供实际运行检查。</summary>
    public static int CachedFrameCount => frames.Count;

    /// <summary>读取当前身体实际采用的外观编号；观察和调试也使用相同规则。</summary>
    public static string[] GetVisibleLayerIds(CompiledCreaturePhenotype compiled)
        => GetPlan(compiled).Layers.Select(layer => layer.id).ToArray();

    /// <summary>将实际采用的器官帧挂点转换为肉身世界位置；不依赖镜头或当前绘制方式。</summary>
    /// <param name="compiled">当前实际身体。</param>
    /// <param name="actor">肉身所属单位。</param>
    /// <param name="layerId">声明出口的外观层。</param>
    /// <param name="pointId">制作时命名的点，例如mount或cast。</param>
    /// <param name="position">共同地图坐标；高度相对脚下地表。</param>
    public static bool TryGetAnchor(CompiledCreaturePhenotype compiled, Actor actor, string layerId,
        string pointId, out Vector3 position)
    {
        position = default;
        Sprite native = actor.calculateMainSprite();
        if (native == null) return false;
        foreach (CreatureVisualLayerAsset layer in GetPlan(compiled).Layers)
        {
            if (layer.id != layerId || !layer.FramesByRig[compiled.Morph.VisualRigId]
                    .TryGetValue(native.name, out CreatureLayerFrame frame) ||
                frame.BodyWidth != native.rect.width || frame.BodyHeight != native.rect.height ||
                !frame.Points.TryGetValue(pointId, out CreatureFramePoint point)) continue;
            Vector2 offset = new((point.X - native.pivot.x) / native.pixelsPerUnit *
                                 actor.current_scale.x * (actor.flip ? -1f : 1f),
                (point.Y - native.pivot.y) / native.pixelsPerUnit * Mathf.Abs(actor.current_scale.y));
            // 原版倾斜在身体图片平面内旋转；地表和显示抖动仍由各显示端统一处理。
            Vector3 rotated = Quaternion.Euler(0f, 0f, actor.current_rotation.z) * offset;
            position = new Vector3(actor.current_position.x + rotated.x, actor.current_position.y,
                actor.position_height + rotated.y);
            return true;
        }

        return false;
    }

    /// <summary>合成原版已经完成着色的身体；幼体或未制作的动作保持原版显示。</summary>
    /// <param name="compiled">完整身体及全部候选器官。</param>
    /// <param name="body">原版已处理肤色和装备的最终主体帧。</param>
    /// <param name="nativeFrame">未着色的原版动画帧，用来识别动作和成体尺寸。</param>
    /// <param name="excludedLayer">由同轮动作独立显示的实际器官层。</param>
    public static Sprite Compose(CompiledCreaturePhenotype compiled, Sprite body, Sprite nativeFrame,
        string excludedLayer = null)
    {
        AppearancePlan plan = GetPlan(compiled);
        if (plan.Layers.Length == 0 || body == null || nativeFrame == null) return body;
        var key = (body.GetInstanceID(), plan.Key + "|" + excludedLayer);
        if (frames.TryGetValue(key, out Sprite cached)) return cached;

        // 原版新着色帧先写进图集的像素数组，稍后才上传；首次合成前须完成这次上传。
        AssetManager.dynamic_sprites_library.checkDirty();
        Sprite composed = BuildFrame(compiled.Morph.VisualRigId, plan, body, nativeFrame, excludedLayer);
        if (composed == null) return body;
        if (frames.Count >= MaximumCachedFrames)
        {
            var oldest = frameOrder.Dequeue();
            DestroyFrame(frames[oldest]);
            frames.Remove(oldest);
        }

        frames.Add(key, composed);
        frameOrder.Enqueue(key);
        return composed;
    }

    /// <summary>先按位置与优先顺序排除拥挤，再限制显示数量；全部器官仍保留在实际身体中。</summary>
    private static AppearancePlan GetPlan(CompiledCreaturePhenotype compiled)
    {
        if (plans.TryGetValue(compiled.CompiledIndex, out AppearancePlan cached)) return cached;
        string rigId = compiled.Morph.VisualRigId;
        CreatureVisualRigAsset rig = string.IsNullOrEmpty(rigId)
            ? null
            : Content.Libraries.Manager.CreatureVisualRigLibrary.get(rigId);
        var candidates = new List<(CreatureVisualLayerAsset layer, int rank)>();
        if (rig != null && rig.SupportsActor(compiled.Morph.ActorAssetId))
        {
            foreach (CompiledCreatureVisualLayer source in compiled.VisualLayers)
            foreach (string layerId in source.LayerIds)
            {
                CreatureVisualLayerAsset layer = Content.Libraries.Manager.CreatureVisualLayerLibrary.get(layerId);
                if (layer != null && layer.FramesByRig.ContainsKey(rigId)) candidates.Add((layer, source.Rank));
            }
        }

        var occupied = new HashSet<string>(StringComparer.Ordinal);
        var selected = new List<CreatureVisualLayerAsset>();
        foreach (var candidate in candidates.OrderByDescending(item => item.layer.Priority)
                     .ThenByDescending(item => item.rank).ThenBy(item => item.layer.id, StringComparer.Ordinal))
        {
            if (occupied.Contains(candidate.layer.Channel)) continue;
            selected.Add(candidate.layer);
            occupied.Add(candidate.layer.Channel);
            occupied.UnionWith(candidate.layer.OccludesChannels);
            if (selected.Count >= compiled.BodyPlan.MaximumOverlayLayers) break;
        }

        if (compiled.BodyPlan.MaximumOverlayLayers == 0) selected.Clear();

        var result = new AppearancePlan(rigId, selected.OrderBy(layer => layer.Depth)
            .ThenBy(layer => layer.id, StringComparer.Ordinal).ToArray());
        plans.Add(compiled.CompiledIndex, result);
        return result;
    }

    /// <summary>先铺背后部件，再铺身体与正面部件；全程使用整数像素并保持原版枢轴。</summary>
    private static Sprite BuildFrame(string rigId, AppearancePlan plan, Sprite bodySprite, Sprite nativeFrame,
        string excludedLayer)
    {
        int width = Mathf.RoundToInt(bodySprite.rect.width);
        int height = Mathf.RoundToInt(bodySprite.rect.height);
        var layers = new List<(CreatureVisualLayerAsset layer, CreatureLayerFrame frame, Sprite sprite)>();
        int minX = 0, minY = 0, maxX = width, maxY = height;
        foreach (CreatureVisualLayerAsset layer in plan.Layers)
        {
            if (layer.id == excludedLayer) continue;
            if (!layer.FramesByRig[rigId].TryGetValue(nativeFrame.name, out CreatureLayerFrame frame)) continue;
            // 成体、幼体、特殊换装有各自的动画尺寸，不能拿成体部件硬套到另一种身体上。
            if (frame.BodyWidth != nativeFrame.rect.width || frame.BodyHeight != nativeFrame.rect.height ||
                width != frame.BodyWidth || height != frame.BodyHeight) continue;
            if (!parts.TryGetValue(frame.SpritePath, out Sprite sprite))
            {
                sprite = SpriteTextureLoader.getSprite(frame.SpritePath);
                if (sprite == null) throw new InvalidOperationException($"器官动画图片不存在：{frame.SpritePath}");
                parts.Add(frame.SpritePath, sprite);
            }

            layers.Add((layer, frame, sprite));
            minX = Math.Min(minX, frame.X);
            minY = Math.Min(minY, frame.Y);
            maxX = Math.Max(maxX, frame.X + Mathf.RoundToInt(sprite.rect.width));
            maxY = Math.Max(maxY, frame.Y + Mathf.RoundToInt(sprite.rect.height));
        }

        if (layers.Count == 0) return null;

        Color32[] body = ReadPixels(bodySprite);
        int outWidth = maxX - minX, outHeight = maxY - minY;
        var output = new Color32[outWidth * outHeight];
        foreach (var item in layers)
            if (item.layer.Depth == CreatureLayerDepth.BehindBody)
                Paint(item.sprite, item.frame, false);
        Blit(output, outWidth, body, width, height, -minX, -minY);
        foreach (var item in layers)
            if (item.layer.Depth != CreatureLayerDepth.BehindBody)
                Paint(item.sprite, item.frame, item.layer.ClipToBody);

        var texture = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false)
        {
            name = "cultiway_body_" + bodySprite.name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(output);
        texture.Apply(false, true);
        var pivot = new Vector2((bodySprite.pivot.x - minX) / outWidth,
            (bodySprite.pivot.y - minY) / outHeight);
        Sprite result = Sprite.Create(texture, new Rect(0, 0, outWidth, outHeight), pivot,
            bodySprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        result.name = "cultiway_" + bodySprite.name;
        return result;

        // 图层位置来自同一份制作目录；只在真实身体像素内叠加体表纹样。
        void Paint(Sprite sprite, CreatureLayerFrame frame, bool clip)
        {
            Color32[] pixels = ReadPixels(sprite);
            int partWidth = Mathf.RoundToInt(sprite.rect.width);
            int partHeight = Mathf.RoundToInt(sprite.rect.height);
            for (int y = 0; y < partHeight; y++)
            for (int x = 0; x < partWidth; x++)
            {
                Color32 color = pixels[y * partWidth + x];
                int bx = frame.X + x, by = frame.Y + y;
                if (clip && (bx < 0 || bx >= width || by < 0 || by >= height || body[by * width + bx].a == 0))
                    continue;
                int target = (by - minY) * outWidth + bx - minX;
                output[target] = Over(output[target], color);
            }
        }
    }

    /// <summary>把完整身体铺到合成画布中，保留已有的半透明像素。</summary>
    private static void Blit(Color32[] target, int targetWidth, Color32[] source, int width, int height, int dx, int dy)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int index = (y + dy) * targetWidth + x + dx;
            target[index] = Over(target[index], source[y * width + x]);
        }
    }

    /// <summary>标准透明叠加，透明背景上的颜色保持原值。</summary>
    private static Color32 Over(Color32 bottom, Color32 top)
    {
        if (top.a == 0) return bottom;
        if (top.a == 255 || bottom.a == 0) return top;
        int alpha = top.a * 255 + bottom.a * (255 - top.a);
        return new Color32(
            (byte)((top.r * top.a * 255 + bottom.r * bottom.a * (255 - top.a)) / alpha),
            (byte)((top.g * top.a * 255 + bottom.g * bottom.a * (255 - top.a)) / alpha),
            (byte)((top.b * top.a * 255 + bottom.b * bottom.a * (255 - top.a)) / alpha),
            (byte)(alpha / 255));
    }

    /// <summary>读取精灵自己的矩形；原版不可读图集通过临时显存画布读取一次。</summary>
    internal static Color32[] ReadPixels(Sprite sprite)
    {
        // 未打包精灵的 textureRect 也可能被紧密网格缩成小数边界，必须读取原图 rect。
        Rect rect = sprite.packed ? sprite.textureRect : sprite.rect;
        int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
        if (sprite.texture.isReadable)
        {
            Color[] colors =
                sprite.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
            var pixels = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++) pixels[i] = colors[i];
            return RestoreTrimmedFrame(sprite, pixels, width, height);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0);
        var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(sprite.texture, temporary);
            RenderTexture.active = temporary;
            readable.ReadPixels(rect, 0, 0);
            readable.Apply();
            return RestoreTrimmedFrame(sprite, readable.GetPixels32(), width, height);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            Object.Destroy(readable);
        }
    }

    /// <summary>图集会裁去透明边；按裁剪偏移还原原图画布，保持制作时的位置与宽高。</summary>
    private static Color32[] RestoreTrimmedFrame(Sprite sprite, Color32[] pixels, int width, int height)
    {
        int fullWidth = Mathf.RoundToInt(sprite.rect.width), fullHeight = Mathf.RoundToInt(sprite.rect.height);
        if (width == fullWidth && height == fullHeight) return pixels;
        var result = new Color32[fullWidth * fullHeight];
        Vector2 offset = sprite.textureRectOffset;
        int dx = Mathf.RoundToInt(offset.x), dy = Mathf.RoundToInt(offset.y);
        for (int y = 0; y < height; y++)
            Array.Copy(pixels, y * width, result, (y + dy) * fullWidth + dx, width);
        return result;
    }

    /// <summary>释放本服务创建的图片；加载器拥有的原图不由本服务销毁。</summary>
    private static void DestroyFrame(Sprite sprite)
    {
        Object.Destroy(sprite.texture);
        Object.Destroy(sprite);
    }

    /// <summary>资源重载时移除原版加载器保留的旧部件，再释放本服务的合成帧。</summary>
    public static void InvalidateResources()
    {
        var originalCache = (Dictionary<string, Sprite>)typeof(SpriteTextureLoader)
            .GetField("_cached_sprites", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        foreach (CreatureVisualLayerAsset layer in Content.Libraries.Manager.CreatureVisualLayerLibrary.list)
        foreach (Dictionary<string, CreatureLayerFrame> animation in layer.FramesByRig.Values)
        foreach (CreatureLayerFrame frame in animation.Values)
            originalCache.Remove(frame.SpritePath);
        ClearWorldState();
    }

    /// <summary>清理世界或重载资源时释放所有合成图片。</summary>
    public static void ClearWorldState()
    {
        foreach (Sprite sprite in frames.Values) DestroyFrame(sprite);
        frames.Clear();
        frameOrder.Clear();
        plans.Clear();
        parts.Clear();
    }

    /// <summary>只保存实际可见的部件；隐藏的器官不进入图片缓存键。</summary>
    private sealed class AppearancePlan
    {
        /// <summary>按绘制顺序排列的外观。</summary>
        internal readonly CreatureVisualLayerAsset[] Layers;

        /// <summary>相同可见外观共用图片的编号。</summary>
        internal readonly string Key;

        /// <summary>建立只读的外观取舍结果。</summary>
        internal AppearancePlan(string rigId, CreatureVisualLayerAsset[] layers)
        {
            Layers = layers;
            Key = rigId + "|" + string.Join("|", layers.Select(layer => layer.id));
        }
    }
}