using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts;

public sealed class ArtifactAppearanceModuleDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("variants")] public ArtifactAppearanceVariantDef[] Variants = [];

    public ArtifactAppearanceVariantDef GetVariant(string key)
    {
        for (int i = 0; i < Variants.Length; i++)
        {
            if (Variants[i].Key == key) return Variants[i];
        }
        return null;
    }
}

public sealed class ArtifactAppearanceVariantDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("anchors")] public ArtifactAppearanceAnchorDef[] Anchors = [];
    [JsonProperty("parts")] public JObject[] Parts = [];

    public ArtifactAppearanceAnchorDef GetAnchor(string key)
    {
        for (int i = 0; i < Anchors.Length; i++)
        {
            if (Anchors[i].Key == key) return Anchors[i];
        }
        return null;
    }
}

public sealed class ArtifactAppearanceAnchorDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("position")] public float[] Position = [0f, 0f, 0f];
}

public sealed class ArtifactAppearanceTemplateDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("shape")] public string Shape;
    [JsonProperty("artifact")] public string Artifact;
    [JsonProperty("camera")] public JObject Camera = new();
    [JsonProperty("light")] public JObject Light = new();
    [JsonProperty("placements")] public ArtifactAppearancePlacementDef[] Placements = [];
}

public sealed class ArtifactAppearancePlacementDef
{
    [JsonProperty("slot")] public string Slot;
    [JsonProperty("module")] public string Module;
    [JsonProperty("anchor")] public string Anchor = "origin";
    [JsonProperty("position")] public float[] Position = [0f, 0f, 0f];
    [JsonProperty("rotation")] public float[] Rotation = [0f, 0f, 0f];
    [JsonProperty("scale")] public float[] Scale = [1f, 1f, 1f];
    [JsonProperty("z")] public int Z;
}

public sealed class ArtifactAppearanceColorSchemeDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("colors")] public Dictionary<string, string> Colors = new();
    [JsonProperty("visual_theme")] public ArtifactAppearanceVisualThemeDef VisualTheme;
}

/// <summary>
/// 配色方案提供给光效、粒子等外部表现使用的语义颜色，不依赖模型内部的材质通道命名。
/// </summary>
public sealed class ArtifactAppearanceVisualThemeDef
{
    [JsonProperty("primary")] public string Primary;
    [JsonProperty("secondary")] public string Secondary;
    [JsonProperty("glow")] public string Glow;
}

/// <summary>独立于颜色通道名的表面着色与像素纹理参数。</summary>
public sealed class ArtifactAppearanceSurfaceStyleDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("diffuse")] public float Diffuse = 0.56f;
    [JsonProperty("side_shadow")] public float SideShadow = 1f;
    [JsonProperty("brightness")] public float Brightness;
    [JsonProperty("emission")] public float Emission;
    [JsonProperty("texture_dark")] public float TextureDark;
    [JsonProperty("texture_light")] public float TextureLight;
    [JsonProperty("texture_frequency")] public int TextureFrequency;
    [JsonProperty("sparkle_frequency")] public int SparkleFrequency;
}

internal sealed class ArtifactAppearanceModulesFile
{
    [JsonProperty("canvas")] public int Canvas = 28;
    [JsonProperty("modules")] public ArtifactAppearanceModuleDef[] Modules = [];
}

internal sealed class ArtifactAppearanceTemplatesFile
{
    [JsonProperty("canvas")] public int Canvas = 28;
    [JsonProperty("templates")] public ArtifactAppearanceTemplateDef[] Templates = [];
}

internal sealed class ArtifactAppearanceColorsFile
{
    [JsonProperty("schemes")] public ArtifactAppearanceColorSchemeDef[] Schemes = [];
}

internal sealed class ArtifactAppearanceSurfacesFile
{
    [JsonProperty("styles")] public ArtifactAppearanceSurfaceStyleDef[] Styles = [];
}
