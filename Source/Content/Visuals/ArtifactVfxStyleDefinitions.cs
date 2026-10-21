using Newtonsoft.Json;

namespace Cultiway.Content.Visuals;

/// <summary>能力配置引用的空间表现样式，不再携带旧纹理尺寸和帧率。</summary>
public sealed class ArtifactVfxStyleDef
{
    /// <summary>稳定的样式编号。</summary>
    [JsonProperty("key")] public string Key;
    /// <summary>圆域、扇区和符阵的边界样式。</summary>
    [JsonProperty("surface")] public ArtifactVfxSurfaceStyleDef Surface = new();
    /// <summary>光束、牵引和历史轨迹样式。</summary>
    [JsonProperty("path")] public ArtifactVfxPathStyleDef Path = new();
}

/// <summary>空间边界与节点参数。</summary>
public sealed class ArtifactVfxSurfaceStyleDef
{
    /// <summary>区别风、治疗与符纹等形式的样式编号。</summary>
    [JsonProperty("pattern")] public string Pattern = ArtifactVfxStyles.Arcane;
    /// <summary>是否保留内侧的第二层弧。</summary>
    [JsonProperty("ring_count")] public int RingCount = 2;
    /// <summary>范围周围的装饰节点数量，受共同预算约束。</summary>
    [JsonProperty("node_count")] public int NodeCount = 6;
    /// <summary>外侧节点每世界秒转过的角度。</summary>
    [JsonProperty("outer_rotation")] public float OuterRotation = 9f;
}

/// <summary>空间通道参数。</summary>
public sealed class ArtifactVfxPathStyleDef
{
    /// <summary>装饰光点每世界秒流过的次数。</summary>
    [JsonProperty("flow_speed")] public float FlowSpeed = 1.8f;
    /// <summary>通道内部光点密度，不代表额外攻击。</summary>
    [JsonProperty("strands")] public int Strands = 1;
    /// <summary>起点的宽度比例。</summary>
    [JsonProperty("start_width")] public float StartWidth = .72f;
    /// <summary>中段的宽度比例。</summary>
    [JsonProperty("middle_width")] public float MiddleWidth = 1f;
    /// <summary>终点的宽度比例。</summary>
    [JsonProperty("end_width")] public float EndWidth = .72f;
}

/// <summary>磁盘上的空间样式集合。</summary>
internal sealed class ArtifactVfxStylesFile
{
    /// <summary>按稳定编号登记的样式。</summary>
    [JsonProperty("styles")] public ArtifactVfxStyleDef[] Styles = [];
}
