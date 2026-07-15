using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts;

public sealed class ArtifactAppearanceCatalog
{
    public int Canvas = 28;
    public Dictionary<string, ArtifactAppearanceModuleDef> Modules = new();
    public Dictionary<string, ArtifactAppearanceTemplateDef> Templates = new();
    public Dictionary<string, ArtifactAppearanceColorSchemeDef> ColorSchemes = new();

    public List<ArtifactAppearanceTemplateDef> TemplatesForShape(string shape)
    {
        List<ArtifactAppearanceTemplateDef> result = new();
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

public static class ArtifactAppearanceCatalogLoader
{
    public static ArtifactAppearanceCatalog Current { get; private set; } = new();

    public static void Load()
    {
        var root = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Artifacts", "AppearanceCatalog");
        Current = LoadFrom(root);
        ArtifactAppearanceRenderer.ClearCache();
    }

    private static ArtifactAppearanceCatalog LoadFrom(string root)
    {
        ArtifactAppearanceCatalog catalog = new();
        if (!Directory.Exists(root))
        {
            ModClass.LogWarning($"法器外观目录不存在: {root}");
            return catalog;
        }

        var modules = ReadJson<ArtifactAppearanceModulesFile>(Path.Combine(root, "modules.json"));
        var templates = ReadJson<ArtifactAppearanceTemplatesFile>(Path.Combine(root, "templates.json"));
        var colors = ReadJson<ArtifactAppearanceColorsFile>(Path.Combine(root, "colors.json"));
        int canvas = modules?.Canvas ?? templates?.Canvas ?? 28;
        catalog.Canvas = canvas > 0 ? canvas : 28;

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
                ModClass.LogWarning($"法器外观配置缺失: {path}");
                return null;
            }
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            ModClass.LogError($"读取法器外观配置失败: {path}\n{e.Message}");
            return null;
        }
    }

    private static bool ValidateModule(ArtifactAppearanceModuleDef module)
    {
        if (module.Variants == null || module.Variants.Length == 0)
        {
            ModClass.LogWarning($"法器外观模块没有 variant: {module.Key}");
            return false;
        }
        HashSet<string> anchorKeys = null;
        foreach (var variant in module.Variants)
        {
            if (variant == null || string.IsNullOrEmpty(variant.Key)) return false;
            variant.Anchors ??= [];
            variant.Parts ??= [];
            HashSet<string> current = new();
            foreach (var anchor in variant.Anchors)
            {
                if (!string.IsNullOrEmpty(anchor.Key)) current.Add(anchor.Key);
            }
            if (anchorKeys == null)
            {
                anchorKeys = current;
            }
            else if (!anchorKeys.SetEquals(current))
            {
                ModClass.LogWarning($"法器外观模块 {module.Key} 的 variant 锚点 key 不一致: {variant.Key}");
                return false;
            }
        }
        return true;
    }

    private static bool ValidateTemplate(ArtifactAppearanceTemplateDef template, ArtifactAppearanceCatalog catalog)
    {
        if (template.Placements == null || template.Placements.Length == 0) return false;
        template.Camera ??= new JObject();
        template.Light ??= new JObject();
        foreach (var placement in template.Placements)
        {
            if (placement == null || string.IsNullOrEmpty(placement.Module)) return false;
            if (!catalog.Modules.TryGetValue(placement.Module, out var module))
            {
                ModClass.LogWarning($"法器外观模板 {template.Key} 引用了不存在的模块 {placement.Module}");
                return false;
            }
            placement.Anchor ??= "origin";
            var ok = false;
            foreach (var variant in module.Variants)
            {
                if (variant.GetAnchor(placement.Anchor) != null)
                {
                    ok = true;
                    break;
                }
            }
            if (!ok)
            {
                ModClass.LogWarning($"法器外观模板 {template.Key} 的放置 {placement.Slot} 找不到锚点 {placement.Anchor}");
                return false;
            }
        }
        return true;
    }
}

public sealed class ArtifactAppearanceCatalogManager : ICanInit, ICanReload
{
    public void Init()
    {
        ArtifactAppearanceCatalogLoader.Load();
    }

    public void OnReload()
    {
        ArtifactAppearanceCatalogLoader.Load();
    }
}
