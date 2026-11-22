using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>当前支持的实际环境条件。</summary>
public enum ConstructionWorldConditionKind
{
    /// <summary>作用中的区域或世界不接受新的地块点火。</summary>
    PreventIgnition = 0
}

/// <summary>由已付款的施放维护，在本次活动结束后解除。</summary>
public sealed class ConstructionWorldCondition
{
    public ConstructionWorldConditionKind Kind;
    public float Radius = 5f;
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool EntireWorld;
}

/// <summary>施放注册、点火与实际显示共用的冻结范围。</summary>
internal sealed class CompiledWorldCondition
{
    internal readonly float Radius;
    internal readonly bool EntireWorld;

    internal CompiledWorldCondition(ConstructionWorldCondition source)
    {
        if (source.Kind != ConstructionWorldConditionKind.PreventIgnition ||
            !(source.Radius >= .5f && source.Radius <= 256f))
            throw new InvalidDataException("WorldCondition：需要已登记的条件，区域半径须为0.5至256格");
        Radius = source.Radius;
        EntireWorld = source.EntireWorld;
    }
}
