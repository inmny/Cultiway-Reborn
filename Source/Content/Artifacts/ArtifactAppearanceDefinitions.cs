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
    /// <summary>
    /// 相对于 AppearanceCatalog 的 Blockbench OBJ 模型路径；为空时可由模块文件的 model_root 推导，
    /// 两者都未提供时才使用旧的 parts 几何。
    /// </summary>
    [JsonProperty("model")] public string Model;
    [JsonProperty("anchors")] public ArtifactAppearanceAnchorDef[] Anchors = [];
    /// <summary>OBJ 材质名到表面着色类型的映射，不在模型文件中固化最终颜色。</summary>
    [JsonProperty("material_surfaces")] public Dictionary<string, string> MaterialSurfaces = new();
    [JsonProperty("parts")] public JObject[] Parts = [];

    [JsonIgnore] internal ArtifactAppearanceModelData ModelData;

    public ArtifactAppearanceAnchorDef GetAnchor(string key)
    {
        for (int i = 0; i < Anchors.Length; i++)
        {
            if (Anchors[i].Key == key) return Anchors[i];
        }
        return null;
    }

    public string ResolveSurface(string material, string embeddedSurface)
    {
        if (!string.IsNullOrEmpty(embeddedSurface)) return embeddedSurface;
        return MaterialSurfaces != null && MaterialSurfaces.TryGetValue(material, out string surface)
            ? surface
            : "neutral";
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
    [JsonProperty("views")] public ArtifactAppearanceViewDef[] Views = [];
    [JsonProperty("placements")] public ArtifactAppearancePlacementDef[] Placements = [];

    public ArtifactAppearanceViewDef GetView(string key)
    {
        for (int i = 0; i < Views.Length; i++)
        {
            if (Views[i].Key == key) return Views[i];
        }
        return null;
    }
}

/// <summary>模板针对图标、常态实体或激活实体定义的相机与光照。</summary>
public sealed class ArtifactAppearanceViewDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("size")] public int Size;
    [JsonProperty("camera")] public JObject Camera = new();
    [JsonProperty("light")] public JObject Light = new();
    [JsonProperty("auto_frame")] public bool AutoFrame = true;
    [JsonProperty("margin")] public int Margin = 2;
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
    /// <summary>面朝相机且接近光照方向时进入高亮色阶的强度。</summary>
    [JsonProperty("specular")] public float Specular;
    /// <summary>高光聚拢程度；数值越高，高光越小且越锐利。</summary>
    [JsonProperty("specular_power")] public float SpecularPower = 14f;
    /// <summary>轮廓朝向光源一侧提升色阶的强度。</summary>
    [JsonProperty("rim_light")] public float RimLight;
    /// <summary>最终像素轮廓上额外提亮受光边，适用于硬质反光表面。</summary>
    [JsonProperty("pixel_rim")] public bool PixelRim;
    /// <summary>写入独立发光层的强度；与主体亮度分开。</summary>
    [JsonProperty("emission_layer")] public float EmissionLayer;
    /// <summary>连续光照量化为六级像素色阶时使用的五个边界。</summary>
    [JsonProperty("shade_thresholds")] public float[] ShadeThresholds = [0.10f, 0.28f, 0.47f, 0.67f, 0.87f];
    /// <summary>超采样降采样时保留低覆盖率细节，适用于发光纹路等细窄表面。</summary>
    [JsonProperty("preserve_detail")] public bool PreserveDetail;
    /// <summary>在模型空间生成的确定性像素纹理类型；空值表示只使用几何光照。</summary>
    [JsonProperty("pattern")] public string Pattern;
    /// <summary>纹理在一个模型单位内的重复频率。</summary>
    [JsonProperty("pattern_scale")] public float PatternScale = 1f;
    /// <summary>纹理对连续光照值的扰动强度。</summary>
    [JsonProperty("pattern_strength")] public float PatternStrength;
    /// <summary>小于该输出尺寸时不应用纹理，避免静置小图被细节噪声淹没。</summary>
    [JsonProperty("pattern_min_size")] public int PatternMinSize = 28;
}

internal sealed class ArtifactAppearanceModulesFile
{
    [JsonProperty("canvas")] public int Canvas = 28;
    /// <summary>variant 未显式指定 model 时使用的模型根目录。</summary>
    [JsonProperty("model_root")] public string ModelRoot { get; set; }
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
