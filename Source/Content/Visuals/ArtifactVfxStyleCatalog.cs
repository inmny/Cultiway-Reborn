using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Abstract;
using Newtonsoft.Json;

namespace Cultiway.Content.Visuals;

/// <summary>能力配置引用的法器视觉语义样式。</summary>
internal static class ArtifactVfxStyleCatalog
{
    private const string RelativeDirectory = "Content/Artifacts/Visuals";
    private static readonly Dictionary<string, ArtifactVfxStyleDef> Styles = new(StringComparer.Ordinal);

    internal static void Load()
    {
        string directory = Path.Combine(ModClass.I.GetDeclaration().FolderPath, RelativeDirectory);
        string[] paths = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        Styles.Clear();
        foreach (string path in paths)
        {
            ArtifactVfxStylesFile file = JsonConvert.DeserializeObject<ArtifactVfxStylesFile>(File.ReadAllText(path));
            foreach (ArtifactVfxStyleDef style in file.Styles)
            {
                if (style == null || string.IsNullOrEmpty(style.Key)) continue;
                style.Surface ??= new ArtifactVfxSurfaceStyleDef();
                style.Path ??= new ArtifactVfxPathStyleDef();
                Styles[style.Key] = style;
            }
        }
        ArtifactVfxTextureLibrary.Clear();
    }

    internal static ArtifactVfxStyleDef Get(string key)
    {
        return Styles.TryGetValue(key, out ArtifactVfxStyleDef style)
            ? style
            : Styles[ArtifactVfxStyles.Arcane];
    }
}
