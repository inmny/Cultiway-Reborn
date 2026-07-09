using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts;

public sealed class ArtifactIconCatalog
{
    public int Canvas = 28;
    public Dictionary<string, ArtifactIconModuleDef> Modules = new();
    public Dictionary<string, ArtifactIconTemplateDef> Templates = new();
    public Dictionary<string, ArtifactIconColorSchemeDef> ColorSchemes = new();

    public List<ArtifactIconTemplateDef> TemplatesForShape(string shape)
    {
        List<ArtifactIconTemplateDef> result = new();
        foreach (var template in Templates.Values)
        {
            if (template.Shape == shape)
            {
                result.Add(template);
            }
        }
        result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return result;
    }
}

public sealed class ArtifactIconModuleDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("variants")] public ArtifactIconVariantDef[] Variants = [];

    public ArtifactIconVariantDef GetVariant(string key)
    {
        if (Variants == null) return null;
        for (int i = 0; i < Variants.Length; i++)
        {
            if (Variants[i].Key == key) return Variants[i];
        }
        return null;
    }
}

public sealed class ArtifactIconVariantDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("anchors")] public ArtifactIconAnchorDef[] Anchors = [];
    [JsonProperty("parts")] public JObject[] Parts = [];

    public ArtifactIconAnchorDef GetAnchor(string key)
    {
        if (Anchors == null) return null;
        for (int i = 0; i < Anchors.Length; i++)
        {
            if (Anchors[i].Key == key) return Anchors[i];
        }
        return null;
    }
}

public sealed class ArtifactIconAnchorDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("position")] public float[] Position = [0f, 0f, 0f];
}

public sealed class ArtifactIconTemplateDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("shape")] public string Shape;
    [JsonProperty("artifact")] public string Artifact;
    [JsonProperty("camera")] public JObject Camera = new();
    [JsonProperty("light")] public JObject Light = new();
    [JsonProperty("placements")] public ArtifactIconPlacementDef[] Placements = [];
}

public sealed class ArtifactIconPlacementDef
{
    [JsonProperty("slot")] public string Slot;
    [JsonProperty("module")] public string Module;
    [JsonProperty("anchor")] public string Anchor = "origin";
    [JsonProperty("position")] public float[] Position = [0f, 0f, 0f];
    [JsonProperty("rotation")] public float[] Rotation = [0f, 0f, 0f];
    [JsonProperty("scale")] public float[] Scale = [1f, 1f, 1f];
    [JsonProperty("z")] public int Z;
}

public sealed class ArtifactIconColorSchemeDef
{
    [JsonProperty("key")] public string Key;
    [JsonProperty("colors")] public Dictionary<string, string> Colors = new();
}

internal sealed class ArtifactIconModulesFile
{
    [JsonProperty("canvas")] public int Canvas = 28;
    [JsonProperty("modules")] public ArtifactIconModuleDef[] Modules = [];
}

internal sealed class ArtifactIconTemplatesFile
{
    [JsonProperty("canvas")] public int Canvas = 28;
    [JsonProperty("templates")] public ArtifactIconTemplateDef[] Templates = [];
}

internal sealed class ArtifactIconColorsFile
{
    [JsonProperty("schemes")] public ArtifactIconColorSchemeDef[] Schemes = [];
}

public static class ArtifactIconCatalogLoader
{
    public static ArtifactIconCatalog Current { get; private set; } = new();

    public static void Load()
    {
        var root = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Artifacts", "IconCatalog");
        Current = LoadFrom(root);
        ArtifactIconRenderer.ClearCache();
    }

    private static ArtifactIconCatalog LoadFrom(string root)
    {
        ArtifactIconCatalog catalog = new();
        if (!Directory.Exists(root))
        {
            ModClass.LogWarning($"法器图标目录不存在: {root}");
            return catalog;
        }

        var modules = ReadJson<ArtifactIconModulesFile>(Path.Combine(root, "modules.json"));
        var templates = ReadJson<ArtifactIconTemplatesFile>(Path.Combine(root, "templates.json"));
        var colors = ReadJson<ArtifactIconColorsFile>(Path.Combine(root, "colors.json"));
        catalog.Canvas = modules?.Canvas > 0 ? modules.Canvas : templates?.Canvas ?? 28;

        if (modules?.Modules != null)
        {
            foreach (var module in modules.Modules)
            {
                if (module == null || string.IsNullOrEmpty(module.Key)) continue;
                if (ValidateModule(module)) catalog.Modules[module.Key] = module;
            }
        }
        if (templates?.Templates != null)
        {
            foreach (var template in templates.Templates)
            {
                if (template == null || string.IsNullOrEmpty(template.Key)) continue;
                if (ValidateTemplate(template, catalog)) catalog.Templates[template.Key] = template;
            }
        }
        if (colors?.Schemes != null)
        {
            foreach (var scheme in colors.Schemes)
            {
                if (scheme == null || string.IsNullOrEmpty(scheme.Key)) continue;
                catalog.ColorSchemes[scheme.Key] = scheme;
            }
        }
        return catalog;
    }

    private static T ReadJson<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                ModClass.LogWarning($"法器图标配置缺失: {path}");
                return null;
            }
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            ModClass.LogError($"读取法器图标配置失败: {path}\n{e.Message}");
            return null;
        }
    }

    private static bool ValidateModule(ArtifactIconModuleDef module)
    {
        if (module.Variants == null || module.Variants.Length == 0)
        {
            ModClass.LogWarning($"法器图标模块没有 variant: {module.Key}");
            return false;
        }
        HashSet<string> anchorKeys = null;
        foreach (var variant in module.Variants)
        {
            if (variant == null || string.IsNullOrEmpty(variant.Key)) return false;
            HashSet<string> current = new();
            if (variant.Anchors != null)
            {
                foreach (var anchor in variant.Anchors)
                {
                    if (!string.IsNullOrEmpty(anchor.Key)) current.Add(anchor.Key);
                }
            }
            if (anchorKeys == null)
            {
                anchorKeys = current;
            }
            else if (!anchorKeys.SetEquals(current))
            {
                ModClass.LogWarning($"法器图标模块 {module.Key} 的 variant 锚点 key 不一致: {variant.Key}");
                return false;
            }
        }
        return true;
    }

    private static bool ValidateTemplate(ArtifactIconTemplateDef template, ArtifactIconCatalog catalog)
    {
        if (template.Placements == null || template.Placements.Length == 0) return false;
        foreach (var placement in template.Placements)
        {
            if (placement == null || string.IsNullOrEmpty(placement.Module)) return false;
            if (!catalog.Modules.TryGetValue(placement.Module, out var module))
            {
                ModClass.LogWarning($"法器图标模板 {template.Key} 引用了不存在的模块 {placement.Module}");
                return false;
            }
            var anchor = placement.Anchor ?? "origin";
            var ok = false;
            foreach (var variant in module.Variants)
            {
                if (variant.GetAnchor(anchor) != null)
                {
                    ok = true;
                    break;
                }
            }
            if (!ok)
            {
                ModClass.LogWarning($"法器图标模板 {template.Key} 的放置 {placement.Slot} 找不到锚点 {anchor}");
                return false;
            }
        }
        return true;
    }
}

public sealed class ArtifactIconCatalogManager : ICanInit, ICanReload
{
    public void Init()
    {
        ArtifactIconCatalogLoader.Load();
    }

    public void OnReload()
    {
        ArtifactIconCatalogLoader.Load();
    }
}
