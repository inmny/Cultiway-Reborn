using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>由实际接触在目标部件上留下短暂薄霜，继承普通接触的阵营和次数规则。</summary>
public sealed class ConstructionFrostCoating
{
    /// <summary>可接触的实际部位；surface 包含身体和可见装备，item 只选实际持物。</summary>
    public string Part = "surface";

    /// <summary>沿接触处向周围蔓延的最大距离，按地图格计算。</summary>
    public float Radius = .55f;

    /// <summary>从确认接触到霜渍消退的世界秒数。</summary>
    public float Seconds = 1.5f;
}

/// <summary>检查并冻结薄霜接触和显示资料。</summary>
public sealed class CompiledFrostCoating
{
    /// <summary>实际目标部位。</summary>
    internal readonly string Part;

    /// <summary>实际接触周围的最大范围和消退时间。</summary>
    internal readonly float Radius, Seconds;

    /// <summary>复制有明确边界的资料。部位是否存在由实际模型判断。</summary>
    /// <param name="value">作者填写的薄霜。</param><param name="path">资料位置。</param>
    internal CompiledFrostCoating(ConstructionFrostCoating value, string path)
    {
        if (string.IsNullOrWhiteSpace(value.Part) || value.Part.Length > 120 ||
            !(value.Radius >= .04f && value.Radius <= 4f) || !(value.Seconds >= .1f && value.Seconds <= 12f))
            throw new InvalidDataException(path + "：需要实际部位、0.04至4格范围和0.1至12秒寿命");
        Part = value.Part; Radius = value.Radius; Seconds = value.Seconds;
    }
}
