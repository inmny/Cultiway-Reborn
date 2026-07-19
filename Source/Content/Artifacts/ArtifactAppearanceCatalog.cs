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
    public ArtifactAppearanceColorRoleDef[] ColorRoles = [];

    private readonly Dictionary<string, ArtifactAppearanceColorRoleDef> _colorRolesByChannel =
        new(StringComparer.Ordinal);

    public ArtifactAppearanceColorRoleDef BaseColorRole => ColorRoles.First(role => role.Base);
    public ArtifactAppearanceColorRoleDef VisualColorRole => ColorRoles.FirstOrDefault(role => role.DrivesVisuals);

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

    public bool TryGetColorRole(string channel, out ArtifactAppearanceColorRoleDef role)
    {
        return _colorRolesByChannel.TryGetValue(channel, out role);
    }

    internal void SetColorRoles(IEnumerable<ArtifactAppearanceColorRoleDef> roles)
    {
        ColorRoles = roles.OrderBy(role => role.Order).ThenBy(role => role.Key, StringComparer.Ordinal).ToArray();
        _colorRolesByChannel.Clear();
        for (int roleIndex = 0; roleIndex < ColorRoles.Length; roleIndex++)
        {
            ArtifactAppearanceColorRoleDef role = ColorRoles[roleIndex];
            for (int channelIndex = 0; channelIndex < role.Channels.Length; channelIndex++)
                _colorRolesByChannel.Add(role.Channels[channelIndex], role);
        }
    }
}
