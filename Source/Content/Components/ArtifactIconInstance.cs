using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 一个具体法器图标组合实例。只保存可序列化数据，运行时由渲染器生成 Sprite。
/// </summary>
public struct ArtifactIconInstance : IComponent
{
    public string template_key;
    public ArtifactIconSlot[] slots;

    public string GetCacheKey()
    {
        var key = template_key ?? string.Empty;
        if (slots == null) return key;
        for (int i = 0; i < slots.Length; i++)
        {
            key += "|" + slots[i].GetCacheKey();
        }
        return key;
    }
}

public struct ArtifactIconSlot
{
    public string slot;
    public string module;
    public string variant;
    public string color_scheme;
    public ArtifactIconColor[] colors;

    public string GetCacheKey()
    {
        var key = $"{slot}:{module}.{variant}:{color_scheme}";
        if (colors == null) return key;
        for (int i = 0; i < colors.Length; i++)
        {
            key += $":{colors[i].material}={colors[i].color_hex}";
        }
        return key;
    }
}

public struct ArtifactIconColor
{
    public string material;
    public string color_hex;
}
