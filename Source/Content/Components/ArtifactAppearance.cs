using System.Text;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 一个具体法器的统一外观实例。背包图标与世界贴图都由同一份组合数据生成。
/// </summary>
public struct ArtifactAppearance : IComponent
{
    public string template_key;
    public ArtifactAppearancePart[] parts = [];

    public ArtifactAppearance()
    {
    }

    public string GetCacheKey()
    {
        StringBuilder builder = new(template_key);
        for (int i = 0; i < parts.Length; i++)
        {
            builder.Append('|').Append(parts[i].GetCacheKey());
        }
        return builder.ToString();
    }
}

/// <summary>
/// 外观模板中一个放置位置实际选中的模块变体与配色。
/// </summary>
public struct ArtifactAppearancePart
{
    public string slot;
    public string module;
    public string variant;
    public string color_scheme;
    public ArtifactAppearanceColor[] colors = [];

    public ArtifactAppearancePart()
    {
    }

    public string GetCacheKey()
    {
        StringBuilder builder = new($"{slot}:{module}.{variant}:{color_scheme}");
        for (int i = 0; i < colors.Length; i++)
        {
            builder.Append(':')
                .Append(colors[i].material)
                .Append('=')
                .Append(colors[i].color_hex);
        }
        return builder.ToString();
    }
}

/// <summary>
/// 对模块某个材质通道的显式颜色覆盖。
/// </summary>
public struct ArtifactAppearanceColor
{
    public string material;
    public string color_hex;
}
