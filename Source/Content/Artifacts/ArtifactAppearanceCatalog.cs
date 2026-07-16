using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public Dictionary<string, ArtifactAppearanceSurfaceStyleDef> SurfaceStyles = new();

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
