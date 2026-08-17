using System;
using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.UI.SubWorlds;

/// <summary>小世界远景索引纹理使用的共享 Shader、材质与地形调色板。</summary>
internal static class SubWorldVisualResources
{
    private const int PaletteSize = 256;
    private const string OverviewShaderPath = "Assets/Cultiway/Shaders/SubWorldOverview.shader";
    private static readonly Dictionary<TileTypeBase, byte> terrainIndices = new();
    private static Material overviewMaterial;
    private static Texture2D paletteTexture;

    internal static Material OverviewMaterial
    {
        get
        {
            EnsurePalette();
            if (overviewMaterial != null) return overviewMaterial;
            overviewMaterial = new Material(WorldVisualResources.LoadShader(OverviewShaderPath))
            {
                name = "Cultiway_SubWorldOverview",
                hideFlags = HideFlags.DontSave
            };
            overviewMaterial.SetTexture("_PaletteTex", paletteTexture);
            return overviewMaterial;
        }
    }

    internal static byte GetTerrainIndex(TileTypeBase terrainType)
    {
        EnsurePalette();
        return terrainIndices[terrainType];
    }

    private static void EnsurePalette()
    {
        if (paletteTexture != null) return;

        var colorIndices = new Dictionary<Color32, byte>();
        var colors = new List<Color32>();
        for (int i = 0; i < AssetManager.tiles.list.Count; i++)
        {
            RegisterTerrain(AssetManager.tiles.list[i], colorIndices, colors);
        }
        for (int i = 0; i < AssetManager.top_tiles.list.Count; i++)
        {
            RegisterTerrain(AssetManager.top_tiles.list[i], colorIndices, colors);
        }

        var palette = new Color32[PaletteSize];
        for (int i = 0; i < colors.Count; i++) palette[i] = colors[i];
        paletteTexture = new Texture2D(PaletteSize, 1, TextureFormat.RGBA32, false, false)
        {
            name = "Cultiway_SubWorldTerrainPalette",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        paletteTexture.SetPixels32(palette);
        paletteTexture.Apply(false, true);
    }

    private static void RegisterTerrain(TileTypeBase terrainType,
        Dictionary<Color32, byte> colorIndices, List<Color32> colors)
    {
        Color32 color = terrainType.color;
        if (!colorIndices.TryGetValue(color, out byte index))
        {
            if (colors.Count == PaletteSize)
            {
                throw new InvalidOperationException("小世界远景地形颜色超过 256 种");
            }
            index = (byte)colors.Count;
            colorIndices.Add(color, index);
            colors.Add(color);
        }
        terrainIndices[terrainType] = index;
    }
}
