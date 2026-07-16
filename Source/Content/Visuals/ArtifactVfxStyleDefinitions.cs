using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Newtonsoft.Json;

namespace Cultiway.Content.Visuals;

/// <summary>能力配置引用的法器视觉语义样式。</summary>
public sealed class ArtifactVfxStyleDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("surface")] public ArtifactVfxSurfaceStyleDef Surface = new();
    [JsonProperty("path")] public ArtifactVfxPathStyleDef Path = new();
}

/// <summary>圆域、扇区和符阵的低分辨率纹理生成参数。</summary>
public sealed class ArtifactVfxSurfaceStyleDef
{
    [JsonProperty("pattern")] public string Pattern = ArtifactVfxStyles.Arcane;
    [JsonProperty("canvas")] public int Canvas = 128;
    [JsonProperty("frames")] public int Frames = 6;
    [JsonProperty("frame_rate")] public float FrameRate = 10f;
    [JsonProperty("ring_count")] public int RingCount = 2;
    [JsonProperty("rune_count")] public int RuneCount = 12;
    [JsonProperty("spoke_count")] public int SpokeCount = 6;
    [JsonProperty("node_count")] public int NodeCount = 6;
    [JsonProperty("break_ratio")] public float BreakRatio = 0.18f;
    [JsonProperty("base_density")] public float BaseDensity = 0.18f;
    [JsonProperty("base_alpha")] public float BaseAlpha = 0.2f;
    [JsonProperty("edge_alpha")] public float EdgeAlpha = 0.78f;
    [JsonProperty("glyph_alpha")] public float GlyphAlpha = 0.66f;
    [JsonProperty("outer_rotation")] public float OuterRotation = 9f;
    [JsonProperty("inner_rotation")] public float InnerRotation = -18f;
    [JsonProperty("flow_speed")] public float FlowSpeed = 1f;
    [JsonProperty("irregularity")] public float Irregularity = 0.16f;
}

/// <summary>光束、牵引和历史拖尾的纹理带参数。</summary>
public sealed class ArtifactVfxPathStyleDef
{
    [JsonProperty("pattern")] public string Pattern = ArtifactVfxStyles.Arcane;
    [JsonProperty("texture_length")] public int TextureLength = 48;
    [JsonProperty("texture_width")] public int TextureWidth = 12;
    [JsonProperty("tile_length")] public float TileLength = 0.42f;
    [JsonProperty("flow_speed")] public float FlowSpeed = 1.8f;
    [JsonProperty("strands")] public int Strands = 1;
    [JsonProperty("break_ratio")] public float BreakRatio = 0.12f;
    [JsonProperty("edge_glow")] public float EdgeGlow = 0.42f;
    [JsonProperty("start_width")] public float StartWidth = 0.72f;
    [JsonProperty("middle_width")] public float MiddleWidth = 1f;
    [JsonProperty("end_width")] public float EndWidth = 0.72f;
    [JsonProperty("cap")] public string Cap = "spark";
    [JsonProperty("smooth")] public bool Smooth = true;
}

internal sealed class ArtifactVfxStylesFile
{
    [JsonProperty("styles")] public ArtifactVfxStyleDef[] Styles = [];
}

/// <summary>加载法器视觉样式，并在热重载时清除派生纹理。</summary>
