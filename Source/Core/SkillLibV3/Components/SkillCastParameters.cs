using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 构建法术容器时汇总出的通用施法参数，运行时不依赖具体词条类型。
/// </summary>
public struct SkillCastParameters : IComponent
{
    public float CostMultiplier;
    public float SalvoIntervalMultiplier;

    public static SkillCastParameters Default => new()
    {
        CostMultiplier = 1f,
        SalvoIntervalMultiplier = 1f
    };
}
