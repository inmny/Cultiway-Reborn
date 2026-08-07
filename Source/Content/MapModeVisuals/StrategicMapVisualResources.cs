using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.MapModeVisuals;

internal static class StrategicMapVisualResources
{
    private const string FillShaderPath = "Assets/Cultiway/Shaders/StrategicKingdomFill.shader";
    private const string BorderShaderPath = "Assets/Cultiway/Shaders/StrategicKingdomBorder.shader";
    private static Material fillMaterial;
    private static Material borderMaterial;

    internal static Material FillMaterial => fillMaterial ??= CreateMaterial(
        "Cultiway_StrategicKingdomFill",
        WorldVisualResources.LoadShader(FillShaderPath));

    internal static Material BorderMaterial => borderMaterial ??= CreateMaterial(
        "Cultiway_StrategicKingdomBorder",
        WorldVisualResources.LoadShader(BorderShaderPath));

    private static Material CreateMaterial(string name, Shader shader)
    {
        return new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };
    }
}
