using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>依靠实际父体支撑取得地面控制；作用范围始终来自本部位的接触形状。</summary>
public sealed class ConstructionSupportGrip
{
    /// <summary>被外力移出最近确认位置这么远，就挣脱原抓持。</summary>
    public float EscapeDistance = .65f;

    /// <summary>每次实际接触沿接触方向推挤的世界距离，零表示只抓持。</summary>
    public float Pressure = .14f;

    /// <summary>救援解除时所需的有效余力。</summary>
    public float Strength = 3f;
}

/// <summary>交给运行时的不可变抓持规则。</summary>
internal sealed class CompiledSupportGrip
{
    /// <summary>挣脱距离、接触推挤和抵抗救援的实际量。</summary>
    internal readonly float EscapeDistance, Pressure, Strength;

    /// <summary>允许少量连续推挤，不允许一次作用跨过整个作用域。</summary>
    /// <param name="rule">可编辑的资料。</param><param name="path">错误部位。</param>
    internal CompiledSupportGrip(ConstructionSupportGrip rule, string path)
    {
        if (!(rule.EscapeDistance > 0f && rule.EscapeDistance <= 4f && rule.Pressure >= 0f &&
              rule.Pressure <= .35f && rule.Strength > 0f && rule.Strength <= 10000f))
            throw new InvalidDataException(path + "：抓持的挣脱距离、推挤距离或抗力无效");
        EscapeDistance = rule.EscapeDistance;
        Pressure = rule.Pressure;
        Strength = rule.Strength;
    }
}
