using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Artifacts;

public static class ArtifactAppearanceCatalogLoader
{
    public static ArtifactAppearanceCatalog Current { get; private set; } = new();

    public static void Load()
    {
        var root = Path.Combine(ModClass.I.GetDeclaration().FolderPath, "Content", "Artifacts", "AppearanceCatalog");
        Current = LoadFrom(root);
        ArtifactAppearanceRenderer.ClearCache();
        ArtifactAbilityVisuals.ClearThemeCache();
    }

    private static ArtifactAppearanceCatalog LoadFrom(string root)
    {
        ArtifactAppearanceCatalog catalog = new();
        if (!Directory.Exists(root))
        {
            ModClass.LogWarning($"法器外观目录不存在: {root}");
            return catalog;
        }

        ArtifactAppearanceModulesFile[] moduleFiles = ReadJsonFiles<ArtifactAppearanceModulesFile>(root, "modules*.json");
        ArtifactAppearanceTemplatesFile[] templateFiles = ReadJsonFiles<ArtifactAppearanceTemplatesFile>(root, "templates*.json");
        ArtifactAppearanceColorsFile[] colorFiles = ReadJsonFiles<ArtifactAppearanceColorsFile>(root, "colors*.json");
        ArtifactAppearanceSurfacesFile[] surfaceFiles = ReadJsonFiles<ArtifactAppearanceSurfacesFile>(root, "surfaces*.json");
        int canvas = moduleFiles.FirstOrDefault()?.Canvas ?? templateFiles.FirstOrDefault()?.Canvas ?? 28;
        catalog.Canvas = canvas > 0 ? canvas : 28;

        for (int fileIndex = 0; fileIndex < surfaceFiles.Length; fileIndex++)
        {
            ArtifactAppearanceSurfaceStyleDef[] styles = surfaceFiles[fileIndex].Styles ?? [];
            foreach (ArtifactAppearanceSurfaceStyleDef style in styles)
            {
                if (style == null || string.IsNullOrEmpty(style.Key)) continue;
                catalog.SurfaceStyles[style.Key] = style;
            }
        }
        for (int fileIndex = 0; fileIndex < moduleFiles.Length; fileIndex++)
        {
            ArtifactAppearanceModuleDef[] modules = moduleFiles[fileIndex].Modules ?? [];
            foreach (ArtifactAppearanceModuleDef module in modules)
            {
                if (module == null || string.IsNullOrEmpty(module.Key)) continue;
                if (PrepareModule(root, moduleFiles[fileIndex].ModelRoot, module) && ValidateModule(module, catalog))
                    catalog.Modules[module.Key] = module;
            }
        }
        for (int fileIndex = 0; fileIndex < templateFiles.Length; fileIndex++)
        {
            ArtifactAppearanceTemplateDef[] templates = templateFiles[fileIndex].Templates ?? [];
            foreach (ArtifactAppearanceTemplateDef template in templates)
            {
                if (template == null || string.IsNullOrEmpty(template.Key)) continue;
                if (ValidateTemplate(template, catalog)) catalog.Templates[template.Key] = template;
            }
        }
        for (int fileIndex = 0; fileIndex < colorFiles.Length; fileIndex++)
        {
            ArtifactAppearanceColorSchemeDef[] schemes = colorFiles[fileIndex].Schemes ?? [];
            foreach (ArtifactAppearanceColorSchemeDef scheme in schemes)
            {
                if (scheme == null || string.IsNullOrEmpty(scheme.Key)) continue;
                catalog.ColorSchemes[scheme.Key] = scheme;
            }
        }
        return catalog;
    }

    private static T[] ReadJsonFiles<T>(string root, string pattern) where T : class
    {
        string[] paths = Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        List<T> files = new(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            T value = ReadJson<T>(paths[i]);
            if (value != null) files.Add(value);
        }
        if (files.Count == 0) ModClass.LogWarning($"法器外观配置缺失: {Path.Combine(root, pattern)}");
        return files.ToArray();
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

    private static bool ValidateModule(ArtifactAppearanceModuleDef module, ArtifactAppearanceCatalog catalog)
    {
        if (module.Variants == null || module.Variants.Length == 0)
        {
            ModClass.LogWarning($"法器外观模块没有 variant: {module.Key}");
            return false;
        }
        HashSet<string> anchorKeys = null;
        HashSet<string> variantKeys = new(StringComparer.Ordinal);
        foreach (var variant in module.Variants)
        {
            if (variant == null || string.IsNullOrEmpty(variant.Key) || !variantKeys.Add(variant.Key)) return false;
            variant.Anchors ??= [];
            variant.Parts ??= [];
            variant.MaterialSurfaces ??= new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(variant.Model) && variant.ModelData == null) return false;
            if (variant.ModelData == null && variant.Parts.Length == 0)
            {
                ModClass.LogWarning($"法器外观 variant 没有模型或 parts: {module.Key}.{variant.Key}");
                return false;
            }
            HashSet<string> current = new();
            foreach (var anchor in variant.Anchors)
            {
                if (anchor == null || string.IsNullOrEmpty(anchor.Key) || !current.Add(anchor.Key)) return false;
            }
            if (current.Count == 0) return false;
            if (anchorKeys == null)
            {
                anchorKeys = current;
            }
            else if (!anchorKeys.SetEquals(current))
            {
                ModClass.LogWarning($"法器外观模块 {module.Key} 的 variant 锚点 key 不一致: {variant.Key}");
                return false;
            }
            if (variant.ModelData == null) continue;
            for (int faceIndex = 0; faceIndex < variant.ModelData.Faces.Length; faceIndex++)
            {
                ArtifactAppearanceModelFace face = variant.ModelData.Faces[faceIndex];
                string surface = variant.ResolveSurface(face.Material, face.Surface);
                if (catalog.SurfaceStyles.ContainsKey(surface)) continue;
                ModClass.LogWarning($"法器外观 {module.Key}.{variant.Key} 引用了不存在的表面 {surface}");
                return false;
            }
        }
        return true;
    }

    private static bool PrepareModule(string root, string modelRoot, ArtifactAppearanceModuleDef module)
    {
        ArtifactAppearanceVariantDef[] variants = module.Variants ?? [];
        for (int i = 0; i < variants.Length; i++)
        {
            ArtifactAppearanceVariantDef variant = variants[i];
            if (variant != null && string.IsNullOrEmpty(variant.Model) && !string.IsNullOrEmpty(modelRoot))
            {
                variant.Model = Path.Combine(modelRoot, module.Key, $"{variant.Key}.obj");
            }
            if (variant == null || string.IsNullOrEmpty(variant.Model)) continue;
            try
            {
                variant.ModelData = ArtifactAppearanceObjLoader.Load(root, variant.Model);
                variant.Anchors = variant.ModelData.Anchors;
            }
            catch (Exception e)
            {
                ModClass.LogError($"读取法器模型失败: {module.Key}.{variant.Key}\n{e.Message}");
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
        template.Views ??= [];
        HashSet<string> viewKeys = new(StringComparer.Ordinal);
        foreach (ArtifactAppearanceViewDef view in template.Views)
        {
            if (view == null || string.IsNullOrEmpty(view.Key) || !viewKeys.Add(view.Key)) return false;
            view.Camera ??= new JObject();
            view.Light ??= new JObject();
            if (view.Size < 0 || view.Size > 256 || view.Margin < 0) return false;
        }
        HashSet<string> placementSlots = new(StringComparer.Ordinal);
        foreach (var placement in template.Placements)
        {
            if (placement == null || string.IsNullOrEmpty(placement.Slot) ||
                !placementSlots.Add(placement.Slot) || string.IsNullOrEmpty(placement.Module)) return false;
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
