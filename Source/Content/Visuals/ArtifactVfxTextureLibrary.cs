using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>圆域和符阵使用的分层像素纹理。</summary>
internal sealed class ArtifactSurfaceTextureSet
{
    internal Sprite Base;
    internal Sprite[] OuterFrames;
    internal Sprite[] GlyphFrames;
    internal Sprite Node;
}

/// <summary>扇区使用的底纹和流动纹理。</summary>
internal sealed class ArtifactSectorTextureSet
{
    internal Texture2D Base;
    internal Texture2D[] FlowFrames;
}

/// <summary>路径带使用的核心、外晕和端点纹理。</summary>
internal sealed class ArtifactPathTextureSet
{
    internal Texture2D Core;
    internal Texture2D Glow;
    internal Sprite Cap;
}

/// <summary>
/// 根据 Content 样式生成点过滤像素纹理。纹理只生成一次，运行时只做换帧、着色和网格变换。
/// </summary>
internal static class ArtifactVfxTextureLibrary
{
    private const int NodeSize = 16;
    private static readonly Dictionary<string, ArtifactSurfaceTextureSet> SurfaceCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ArtifactSectorTextureSet> SectorCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, ArtifactPathTextureSet> PathCache = new(StringComparer.Ordinal);
    private static readonly List<UnityEngine.Object> OwnedObjects = new();

    internal static void Clear()
    {
        for (int i = 0; i < OwnedObjects.Count; i++)
        {
            if (OwnedObjects[i] != null) UnityEngine.Object.Destroy(OwnedObjects[i]);
        }
        OwnedObjects.Clear();
        SurfaceCache.Clear();
        SectorCache.Clear();
        PathCache.Clear();
    }

    internal static ArtifactSurfaceTextureSet GetSurface(string styleKey, int sides)
    {
        sides = Mathf.Clamp(sides, 3, 32);
        string cacheKey = $"{styleKey}:{sides}";
        if (SurfaceCache.TryGetValue(cacheKey, out ArtifactSurfaceTextureSet cached)) return cached;

        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(styleKey).Surface;
        int size = Mathf.Clamp(style.Canvas, 64, 256);
        int frameCount = Mathf.Clamp(style.Frames, 1, 12);
        int seed = StableHash(cacheKey);
        ArtifactSurfaceTextureSet result = new()
        {
            Base = CreateSprite(BuildSurfaceBase(style, size, seed), $"artifact_surface_{styleKey}_base"),
            OuterFrames = new Sprite[frameCount],
            GlyphFrames = new Sprite[frameCount],
            Node = CreateSprite(BuildNode(style.Pattern, seed), $"artifact_surface_{styleKey}_node", NodeSize),
        };
        for (int frame = 0; frame < frameCount; frame++)
        {
            result.OuterFrames[frame] = CreateSprite(
                BuildSurfaceOuter(style, size, seed, frame, frameCount),
                $"artifact_surface_{styleKey}_outer_{frame}");
            result.GlyphFrames[frame] = CreateSprite(
                BuildSurfaceGlyph(style, size, sides, seed, frame, frameCount),
                $"artifact_surface_{styleKey}_glyph_{frame}");
        }
        SurfaceCache.Add(cacheKey, result);
        return result;
    }

    internal static ArtifactSectorTextureSet GetSector(string styleKey)
    {
        if (SectorCache.TryGetValue(styleKey, out ArtifactSectorTextureSet cached)) return cached;
        ArtifactVfxSurfaceStyleDef style = ArtifactVfxStyleCatalog.Get(styleKey).Surface;
        int frameCount = Mathf.Clamp(style.Frames, 1, 12);
        int seed = StableHash(styleKey);
        ArtifactSectorTextureSet result = new()
        {
            Base = BuildSectorTexture(style, seed, -1, frameCount),
            FlowFrames = new Texture2D[frameCount],
        };
        for (int frame = 0; frame < frameCount; frame++)
        {
            result.FlowFrames[frame] = BuildSectorTexture(style, seed, frame, frameCount);
        }
        SectorCache.Add(styleKey, result);
        return result;
    }

    internal static ArtifactPathTextureSet GetPath(string styleKey)
    {
        if (PathCache.TryGetValue(styleKey, out ArtifactPathTextureSet cached)) return cached;
        ArtifactVfxPathStyleDef style = ArtifactVfxStyleCatalog.Get(styleKey).Path;
        int width = Mathf.Clamp(style.TextureLength, 16, 128);
        int height = Mathf.Clamp(style.TextureWidth, 6, 32);
        int seed = StableHash(styleKey);
        byte[] coreMask = BuildPathMask(style, width, height, seed);
        byte[] glowMask = Dilate(coreMask, width, height, 2, 0.52f);
        ArtifactPathTextureSet result = new()
        {
            Core = CreateTexture(coreMask, width, height, $"artifact_path_{styleKey}_core", TextureWrapMode.Repeat),
            Glow = CreateTexture(glowMask, width, height, $"artifact_path_{styleKey}_glow", TextureWrapMode.Repeat),
            Cap = string.Equals(style.Cap, "none", StringComparison.Ordinal)
                ? null
                : CreateSprite(BuildCap(style.Cap, seed), $"artifact_path_{styleKey}_cap", NodeSize),
        };
        PathCache.Add(styleKey, result);
        return result;
    }

    private static Texture2D BuildSurfaceBase(ArtifactVfxSurfaceStyleDef style, int size, int seed)
    {
        byte[] mask = new byte[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float ny = (y - half) / half;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                if (radius >= 0.96f) continue;
                float angle = Mathf.Atan2(ny, nx) / (Mathf.PI * 2f) + 0.5f;
                float grain = Hash01(x, y, seed);
                float density = style.BaseDensity * Mathf.Lerp(1.15f, 0.55f, radius);
                float pattern = SurfaceFlow(style.Pattern, radius, angle, 0f);
                float alpha = grain < density ? 0.2f + pattern * 0.34f : pattern * 0.08f;
                if (Mathf.Abs(radius - 0.48f) < 0.012f && grain > 0.24f) alpha = Mathf.Max(alpha, 0.24f);
                mask[y * size + x] = ToByte(alpha);
            }
        }
        return CreateTexture(mask, size, size, "artifact_surface_base", TextureWrapMode.Clamp);
    }

    private static Texture2D BuildSurfaceOuter(
        ArtifactVfxSurfaceStyleDef style,
        int size,
        int seed,
        int frame,
        int frameCount)
    {
        byte[] mask = new byte[size * size];
        float half = (size - 1) * 0.5f;
        float pixel = 1f / half;
        float phase = frame / (float)frameCount * style.FlowSpeed;
        int rings = Mathf.Clamp(style.RingCount, 1, 4);
        int runes = Mathf.Clamp(style.RuneCount, 4, 32);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float ny = (y - half) / half;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                if (radius > 1f) continue;
                float angle = Repeat01(Mathf.Atan2(ny, nx) / (Mathf.PI * 2f) + 0.5f);
                float alpha = 0f;
                for (int ring = 0; ring < rings; ring++)
                {
                    float ringRadius = 0.91f - ring * 0.105f;
                    float distance = Mathf.Abs(radius - ringRadius);
                    if (distance > pixel * (ring == 0 ? 2f : 1.35f)) continue;
                    float section = angle * runes + ring * 0.37f;
                    int sectionIndex = Mathf.FloorToInt(section);
                    float local = Repeat01(section);
                    float broken = Hash01(sectionIndex, ring, seed + 19);
                    if (broken < style.BreakRatio && local > 0.14f && local < 0.86f) continue;
                    float moving = Repeat01(section - phase);
                    float highlight = moving < 0.14f ? 1f - moving / 0.14f : 0f;
                    alpha = Mathf.Max(alpha, 0.48f + highlight * 0.52f);
                }

                float runeSection = angle * runes;
                float runeLocal = Mathf.Abs(Repeat01(runeSection) - 0.5f);
                if (runeLocal < 0.075f && radius > 0.68f && radius < 0.98f)
                {
                    int index = Mathf.FloorToInt(runeSection);
                    float runeBreak = Hash01(index, seed, 73);
                    if (runeBreak >= style.BreakRatio * 0.65f)
                    {
                        float radialPattern = Repeat01((radius - 0.68f) * 18f + index * 0.31f);
                        if (radialPattern < 0.32f) alpha = Mathf.Max(alpha, 0.78f);
                    }
                }
                mask[y * size + x] = ToByte(alpha);
            }
        }
        return CreateTexture(mask, size, size, "artifact_surface_outer", TextureWrapMode.Clamp);
    }

    private static Texture2D BuildSurfaceGlyph(
        ArtifactVfxSurfaceStyleDef style,
        int size,
        int sides,
        int seed,
        int frame,
        int frameCount)
    {
        byte[] mask = new byte[size * size];
        float half = (size - 1) * 0.5f;
        float pixel = 1f / half;
        float phase = frame / (float)frameCount;
        int spokes = Mathf.Clamp(style.SpokeCount, 3, 24);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float ny = (y - half) / half;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                if (radius > 0.78f) continue;
                float radians = Mathf.Atan2(ny, nx);
                float angle = Repeat01(radians / (Mathf.PI * 2f) + 0.5f);
                float alpha = 0f;

                float sector = Mathf.PI * 2f / sides;
                float localAngle = Mathf.Repeat(radians + Mathf.PI + sector * 0.5f, sector) - sector * 0.5f;
                float polygonRadius = 0.61f * Mathf.Cos(Mathf.PI / sides) /
                                      Mathf.Max(0.15f, Mathf.Cos(localAngle));
                if (Mathf.Abs(radius - polygonRadius) < pixel * 1.7f) alpha = 0.84f;

                float spokeDistance = Mathf.Abs(Repeat01(angle * spokes) - 0.5f) / spokes * Mathf.PI * 2f * radius;
                if (spokeDistance < pixel * 1.25f && radius > 0.18f && radius < 0.62f)
                {
                    float segment = Repeat01(radius * 7f + Mathf.Floor(angle * spokes) * 0.23f);
                    if (segment > style.BreakRatio * 0.55f) alpha = Mathf.Max(alpha, 0.62f);
                }
                if (Mathf.Abs(radius - 0.31f) < pixel * 1.5f) alpha = Mathf.Max(alpha, 0.64f);
                alpha = Mathf.Max(alpha, SurfaceMotif(style.Pattern, nx, ny, radius, angle, phase, pixel, seed));
                mask[y * size + x] = ToByte(alpha);
            }
        }
        return CreateTexture(mask, size, size, "artifact_surface_glyph", TextureWrapMode.Clamp);
    }

    private static float SurfaceMotif(
        string pattern,
        float x,
        float y,
        float radius,
        float angle,
        float phase,
        float pixel,
        int seed)
    {
        switch (pattern)
        {
            case ArtifactVfxStyles.Suppression:
            case ArtifactVfxStyles.Prison:
            {
                float chevron = Mathf.Abs(Repeat01(angle * 8f + radius * 2.4f) - 0.5f);
                return chevron < 0.055f && radius > 0.24f ? 0.9f : 0f;
            }
            case ArtifactVfxStyles.Healing:
            case ArtifactVfxStyles.Purification:
            {
                float petals = 0.27f + Mathf.Abs(Mathf.Cos(angle * Mathf.PI * 5f)) * 0.15f;
                float petalLine = Mathf.Abs(radius - petals);
                return petalLine < pixel * 1.8f ? 0.9f : 0f;
            }
            case ArtifactVfxStyles.Devouring:
            case ArtifactVfxStyles.Soul:
            {
                float spiral = Mathf.Abs(Repeat01(angle * 2f + radius * 3.2f + phase) - 0.5f);
                float eye = Mathf.Abs(Mathf.Sqrt(x * x + y * y * 4f) - 0.18f);
                return spiral < 0.04f && radius > 0.16f || eye < pixel * 1.5f ? 0.88f : 0f;
            }
            case ArtifactVfxStyles.Fire:
            {
                float tongue = Repeat01(angle * 5f + phase);
                float target = 0.2f + (1f - Mathf.Abs(tongue - 0.5f) * 2f) * 0.3f;
                return Mathf.Abs(radius - target) < pixel * 2f ? 0.92f : 0f;
            }
            case ArtifactVfxStyles.Wind:
            case ArtifactVfxStyles.Cloth:
            case ArtifactVfxStyles.Vehicle:
            {
                float spiral = Mathf.Abs(Repeat01(angle * 3f - radius * 2.1f - phase) - 0.5f);
                return spiral < 0.045f && radius > 0.16f ? 0.84f : 0f;
            }
            case ArtifactVfxStyles.Earth:
            {
                float square = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                float crack = Mathf.Abs(x * 0.72f + y * 0.36f + (Hash01(seed, 4, 8) - 0.5f) * 0.12f);
                return Mathf.Abs(square - 0.42f) < pixel * 1.8f || crack < pixel && radius < 0.52f ? 0.86f : 0f;
            }
            case ArtifactVfxStyles.Metal:
            case ArtifactVfxStyles.Reflection:
            {
                float blade = Mathf.Abs(x) < pixel * 1.4f && y > -0.42f && y < 0.48f ? 0.9f : 0f;
                float guard = Mathf.Abs(y + 0.16f) < pixel * 1.5f && Mathf.Abs(x) < 0.28f ? 0.82f : 0f;
                return Mathf.Max(blade, guard);
            }
            case ArtifactVfxStyles.Command:
            {
                float chevron = Mathf.Abs(Mathf.Abs(x) + y * 0.65f - 0.3f);
                return chevron < pixel * 1.8f && y > -0.4f ? 0.9f : 0f;
            }
            case ArtifactVfxStyles.Pearl:
            {
                float beads = Mathf.Abs(Repeat01(angle * 5f + phase) - 0.5f);
                return beads < 0.08f && Mathf.Abs(radius - 0.45f) < 0.06f ? 0.94f : 0f;
            }
            default:
            {
                float diamond = Mathf.Abs(Mathf.Abs(x) + Mathf.Abs(y) - 0.38f);
                return diamond < pixel * 1.6f ? 0.76f : 0f;
            }
        }
    }

    private static Texture2D BuildSectorTexture(
        ArtifactVfxSurfaceStyleDef style,
        int seed,
        int frame,
        int frameCount)
    {
        const int width = 64;
        const int height = 64;
        byte[] mask = new byte[width * height];
        bool flowLayer = frame >= 0;
        float phase = flowLayer ? frame / (float)frameCount * style.FlowSpeed : 0f;
        for (int y = 0; y < height; y++)
        {
            float radial = y / (height - 1f);
            for (int x = 0; x < width; x++)
            {
                float lateral = x / (width - 1f);
                float alpha;
                if (!flowLayer)
                {
                    float edge = Mathf.Min(lateral, 1f - lateral);
                    float boundary = edge < 0.025f || radial > 0.94f ? 0.72f : 0f;
                    float grain = Hash01(x, y, seed);
                    float interior = grain < style.BaseDensity * 0.42f ? 0.16f * (0.25f + radial) : 0f;
                    alpha = Mathf.Max(boundary, interior);
                }
                else
                {
                    int lane = Mathf.FloorToInt(lateral * 11f);
                    float laneNoise = Hash01(lane, seed, 91);
                    float travel = Repeat01(radial * (2.5f + laneNoise * 2f) - phase + laneNoise);
                    float laneCenter = (lane + 0.5f) / 11f;
                    float laneWidth = 0.025f + laneNoise * 0.035f;
                    float streak = Mathf.Abs(lateral - laneCenter) < laneWidth && travel < 0.18f
                        ? 1f - travel / 0.18f
                        : 0f;
                    float front = radial > 0.82f
                        ? Mathf.Clamp01((radial - 0.82f) / 0.18f) * (0.4f + Hash01(x, frame, seed) * 0.5f)
                        : 0f;
                    alpha = Mathf.Max(streak, front);
                    if (style.Pattern == ArtifactVfxStyles.Fire)
                    {
                        float flame = Repeat01(radial * 4f - phase + Mathf.Sin(lateral * Mathf.PI * 5f) * 0.22f);
                        alpha = Mathf.Max(alpha, flame < 0.14f ? 0.8f : 0f);
                    }
                    else if (style.Pattern == ArtifactVfxStyles.Wind || style.Pattern == ArtifactVfxStyles.Purification)
                    {
                        float arc = Mathf.Abs(Repeat01(radial * 3f + lateral * 0.85f - phase) - 0.5f);
                        if (arc < 0.045f) alpha = Mathf.Max(alpha, 0.76f);
                    }
                }
                mask[y * width + x] = ToByte(alpha);
            }
        }
        return CreateTexture(mask, width, height, "artifact_sector", TextureWrapMode.Clamp);
    }

    private static byte[] BuildPathMask(ArtifactVfxPathStyleDef style, int width, int height, int seed)
    {
        byte[] mask = new byte[width * height];
        int strands = Mathf.Clamp(style.Strands, 1, 4);
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)width;
            bool broken = Hash01(x / 4, seed, 31) < style.BreakRatio;
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float alpha = 0f;
                if (style.Pattern == ArtifactVfxStyles.Cloth)
                {
                    float edge = Mathf.Min(v, 1f - v);
                    float weave = Repeat01(u * 8f + v * 3f);
                    alpha = edge < 0.12f ? 0.92f : weave < 0.42f ? 0.46f : 0.28f;
                }
                else if (style.Pattern == "chain" || style.Pattern == "binding")
                {
                    float link = Repeat01(u * 6f);
                    float oval = Mathf.Abs(Mathf.Sqrt(Mathf.Pow((link - 0.5f) * 2f, 2f) +
                                                       Mathf.Pow((v - 0.5f) * 1.55f, 2f)) - 0.7f);
                    alpha = oval < 0.16f ? 0.94f : 0f;
                }
                else if (style.Pattern == ArtifactVfxStyles.Pearl)
                {
                    float bead = Repeat01(u * 7f);
                    float distance = Mathf.Sqrt(Mathf.Pow((bead - 0.5f) * 2f, 2f) + Mathf.Pow((v - 0.5f) * 2f, 2f));
                    alpha = distance < 0.5f ? 0.94f : 0f;
                }
                else if (style.Pattern == ArtifactVfxStyles.Ward)
                {
                    float gate = Repeat01(u * 4f);
                    bool rail = Mathf.Abs(v - 0.27f) < 0.075f || Mathf.Abs(v - 0.73f) < 0.075f;
                    bool brace = (gate < 0.12f || gate > 0.88f) && Mathf.Abs(v - 0.5f) < 0.31f;
                    alpha = rail || brace ? 0.9f : 0f;
                }
                else if (style.Pattern == ArtifactVfxStyles.Command)
                {
                    float chevron = Repeat01(u * 5f);
                    float arm = Mathf.Abs(Mathf.Abs(v - 0.5f) - Mathf.Abs(chevron - 0.5f) * 0.72f);
                    alpha = arm < 0.075f && chevron > 0.12f && chevron < 0.88f ? 0.94f : 0f;
                }
                else if (style.Pattern == ArtifactVfxStyles.Purification)
                {
                    float pulse = Repeat01(u * 6f);
                    bool strand = Mathf.Abs(v - 0.5f) < 0.065f;
                    bool cross = Mathf.Abs(pulse - 0.5f) < 0.075f && Mathf.Abs(v - 0.5f) < 0.34f;
                    alpha = strand || cross ? 0.92f : 0f;
                }
                else if (style.Pattern == ArtifactVfxStyles.Reflection)
                {
                    float shard = Repeat01(u * 7f);
                    float mirrored = Mathf.Abs(v - 0.5f) - Mathf.Abs(shard - 0.5f) * 0.32f;
                    alpha = Mathf.Abs(mirrored) < 0.065f && shard > 0.14f && shard < 0.86f ? 0.96f : 0f;
                }
                else if (style.Pattern == "crack")
                {
                    int section = Mathf.FloorToInt(u * 10f);
                    float center = 0.5f + (Hash01(section, seed, 113) - 0.5f) * 0.42f;
                    alpha = Mathf.Abs(v - center) < 0.085f ? 0.9f : 0f;
                }
                else
                {
                    for (int strand = 0; strand < strands; strand++)
                    {
                        float center = (strand + 1f) / (strands + 1f);
                        float wave = Mathf.Sin((u * (2f + strand * 0.45f) + strand * 0.31f) * Mathf.PI * 2f) *
                                     (style.Pattern == "reflection" || style.Pattern == "blade" ? 0.015f : 0.08f);
                        float thickness = style.Pattern == "blade" ? 0.14f : 0.075f;
                        float distance = Mathf.Abs(v - center - wave);
                        if (distance < thickness) alpha = Mathf.Max(alpha, 1f - distance / thickness * 0.35f);
                    }
                    if (style.Pattern == ArtifactVfxStyles.Fire)
                    {
                        float flame = Repeat01(u * 5f + v * 1.6f);
                        if (flame < 0.18f && v > 0.22f && v < 0.78f) alpha = Mathf.Max(alpha, 0.82f);
                    }
                    else if (style.Pattern == ArtifactVfxStyles.Wind)
                    {
                        float slash = Repeat01(u * 4f + v * 0.7f);
                        if (slash < 0.12f) alpha = Mathf.Max(alpha, 0.72f);
                    }
                    else if (style.Pattern == ArtifactVfxStyles.Soul)
                    {
                        float bead = Repeat01(u * 5f);
                        if (Mathf.Abs(bead - 0.5f) < 0.1f && Mathf.Abs(v - 0.5f) < 0.2f) alpha = 1f;
                    }
                }
                if (broken && style.Pattern != ArtifactVfxStyles.Cloth && style.Pattern != "chain") alpha *= 0.18f;
                mask[y * width + x] = ToByte(alpha);
            }
        }
        return mask;
    }

    private static byte[] Dilate(byte[] source, int width, int height, int radius, float alphaScale)
    {
        byte[] result = new byte[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float strongest = 0f;
                for (int oy = -radius; oy <= radius; oy++)
                {
                    int sy = y + oy;
                    if (sy < 0 || sy >= height) continue;
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        int sx = x + ox;
                        if (sx < 0 || sx >= width) continue;
                        float distance = Mathf.Sqrt(ox * ox + oy * oy);
                        if (distance > radius) continue;
                        strongest = Mathf.Max(strongest,
                            source[sy * width + sx] / 255f * (1f - distance / (radius + 0.01f)));
                    }
                }
                result[y * width + x] = ToByte(strongest * alphaScale);
            }
        }
        return result;
    }

    private static Texture2D BuildNode(string pattern, int seed)
    {
        byte[] mask = new byte[NodeSize * NodeSize];
        float center = (NodeSize - 1) * 0.5f;
        for (int y = 0; y < NodeSize; y++)
        {
            for (int x = 0; x < NodeSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 0f;
                if (Mathf.Abs(dx) < 0.75f || Mathf.Abs(dy) < 0.75f) alpha = Mathf.Clamp01(1f - radius / 6f);
                if (pattern == ArtifactVfxStyles.Pearl && radius < 3.2f) alpha = 0.95f;
                if ((pattern == ArtifactVfxStyles.Seal || pattern == ArtifactVfxStyles.Prison) &&
                    Mathf.Abs(radius - 4f) < 1f) alpha = 0.88f;
                if (Hash01(x, y, seed) > 0.92f && radius < 5f) alpha = Mathf.Max(alpha, 0.42f);
                mask[y * NodeSize + x] = ToByte(alpha);
            }
        }
        return CreateTexture(mask, NodeSize, NodeSize, "artifact_surface_node", TextureWrapMode.Clamp);
    }

    private static Texture2D BuildCap(string cap, int seed)
    {
        byte[] mask = new byte[NodeSize * NodeSize];
        float center = (NodeSize - 1) * 0.5f;
        for (int y = 0; y < NodeSize; y++)
        {
            for (int x = 0; x < NodeSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 0f;
                switch (cap)
                {
                    case "blade":
                        if (dx > -4f && dx < 5f && Mathf.Abs(dy) < (5f - dx) * 0.36f) alpha = 0.96f;
                        break;
                    case "shield":
                    case "seal":
                        if (Mathf.Abs(radius - 4.5f) < 1f || Mathf.Abs(dx) < 0.7f || Mathf.Abs(dy) < 0.7f)
                            alpha = 0.9f;
                        break;
                    case "bloom":
                        float petal = 3.2f + Mathf.Abs(Mathf.Cos(Mathf.Atan2(dy, dx) * 4f)) * 2f;
                        if (Mathf.Abs(radius - petal) < 1f) alpha = 0.9f;
                        break;
                    case "flame":
                        float flameWidth = (5.5f - dx) * 0.34f + Mathf.Sin((dx + 5f) * 1.7f) * 0.35f;
                        if (dx > -5f && dx < 5.5f && Mathf.Abs(dy) < flameWidth) alpha = 0.9f;
                        break;
                    case "maw":
                    case "eye":
                        float eye = Mathf.Sqrt(dx * dx + dy * dy * 3f);
                        if (Mathf.Abs(eye - 4.2f) < 1f || radius < 1.4f) alpha = 0.92f;
                        break;
                    case "impact":
                        if (Mathf.Abs(dx) < 0.8f || Mathf.Abs(dy) < 0.8f || Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 0.8f)
                            alpha = Mathf.Clamp01(1f - radius / 7f);
                        break;
                    case "orb":
                        if (radius < 4.2f) alpha = radius < 2f ? 1f : 0.76f;
                        break;
                    case "gust":
                        float gust = Mathf.Abs(dy - Mathf.Sin((dx + 5f) * 0.42f) * 1.8f);
                        float lowerGust = Mathf.Abs(dy + 2.1f - Mathf.Sin((dx + 4f) * 0.36f) * 1.1f);
                        if (dx > -5.5f && dx < 5.5f && (gust < 0.72f || lowerGust < 0.62f)) alpha = 0.88f;
                        break;
                    case "banner":
                        float chevron = Mathf.Abs(Mathf.Abs(dy) - (2.6f - dx * 0.42f));
                        if (dx > -4.5f && dx < 5f && chevron < 0.75f) alpha = 0.92f;
                        break;
                    case "mirror":
                        float diamond = Mathf.Abs(Mathf.Abs(dx) * 0.68f + Mathf.Abs(dy) - 4.3f);
                        if (diamond < 0.8f || radius < 1.2f) alpha = 0.92f;
                        break;
                    default:
                        if (Mathf.Abs(dx) < 0.7f || Mathf.Abs(dy) < 0.7f || Mathf.Abs(radius - 3.5f) < 0.8f)
                            alpha = Mathf.Clamp01(1f - radius / 8f);
                        break;
                }
                if (Hash01(x, y, seed) > 0.97f && radius < 6f) alpha = Mathf.Max(alpha, 0.35f);
                mask[y * NodeSize + x] = ToByte(alpha);
            }
        }
        return CreateTexture(mask, NodeSize, NodeSize, "artifact_path_cap", TextureWrapMode.Clamp);
    }

    private static float SurfaceFlow(string pattern, float radius, float angle, float phase)
    {
        if (pattern == ArtifactVfxStyles.Devouring || pattern == ArtifactVfxStyles.Soul)
        {
            return 1f - Mathf.Abs(Repeat01(angle * 2f + radius * 3f + phase) - 0.5f) * 2f;
        }
        if (pattern == ArtifactVfxStyles.Wind || pattern == ArtifactVfxStyles.Fire)
        {
            return 1f - Mathf.Abs(Repeat01(angle * 4f - radius * 2f - phase) - 0.5f) * 2f;
        }
        return 1f - Mathf.Abs(Repeat01(radius * 5f + angle * 2f + phase) - 0.5f) * 2f;
    }

    private static Sprite CreateSprite(Texture2D texture, string name, float pixelsPerUnit = -1f)
    {
        float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : texture.width * 0.5f;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            ppu);
        sprite.name = name;
        OwnedObjects.Add(sprite);
        return sprite;
    }

    private static Texture2D CreateTexture(
        byte[] alpha,
        int width,
        int height,
        string name,
        TextureWrapMode wrapMode)
    {
        Color32[] pixels = new Color32[alpha.Length];
        for (int i = 0; i < alpha.Length; i++) pixels[i] = new Color32(255, 255, 255, alpha[i]);
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = wrapMode,
            hideFlags = HideFlags.DontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        OwnedObjects.Add(texture);
        return texture;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00ffffff) / 16777215f;
        }
    }

    private static float Repeat01(float value)
    {
        return value - Mathf.Floor(value);
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }
}
