using System.IO;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.Skills;

/// <summary>选择施放对象与使用场景，接触后作用的人物关系仍由实际作用规则决定。</summary>
public sealed class ConstructionUse
{
    /// <summary>允许由战斗或世界用途选择；两者可以同时存在。</summary>
    public ActiveAbilityChannel Channels = ActiveAbilityChannel.Combat;

    /// <summary>选择本人、人物、地点或两者之一，不用人物充当环境用途的定位物。</summary>
    public ActiveAbilityTargetMode Target = ActiveAbilityTargetMode.Object;

    /// <summary>以自己为目标并正式受理时施加的状态，不依赖攻击接触。</summary>
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public ConstructionStatus SelfStatus;
}

/// <summary>已经验证并复制的选择规则，运行与说明共用，不随外部资料修改。</summary>
public sealed class CompiledConstructionUse
{
    /// <summary>实际允许的使用场景。</summary>
    public ActiveAbilityChannel Channels { get; }

    /// <summary>本次选取人物或地点的方式。</summary>
    public ActiveAbilityTargetMode Target { get; }

    /// <summary>正式受理时施加到自己的状态。</summary>
    public CompiledConstructionStatus SelfStatus { get; }

    /// <summary>旧资料保持原有战斗及本人或人物选取，新资料只在入口验证一次。</summary>
    /// <param name="source">可选的明确使用规则。</param>
    /// <param name="relation">旧资料原有的接触人物关系。</param>
    internal CompiledConstructionUse(ConstructionUse source, SkillUseTargetRelation relation)
    {
        Channels = source?.Channels ?? ActiveAbilityChannel.Combat;
        Target = source?.Target ?? (relation == SkillUseTargetRelation.Self
            ? ActiveAbilityTargetMode.Self
            : ActiveAbilityTargetMode.Object);
        if (Channels == ActiveAbilityChannel.None || (Channels & ~(ActiveAbilityChannel.Combat | ActiveAbilityChannel.World)) != 0)
            throw new InvalidDataException("Use/Channels：需要已实现的战斗或世界使用场景");
        if (Target is not (ActiveAbilityTargetMode.Self or ActiveAbilityTargetMode.Object or ActiveAbilityTargetMode.Point or ActiveAbilityTargetMode.ObjectOrPoint))
            throw new InvalidDataException("Use/Target：需要明确的本人、人物、地点或人物与地点选取方式，收到" + Target);
        if (source?.SelfStatus == null) return;
        if (Target != ActiveAbilityTargetMode.Self)
            throw new InvalidDataException("Use/SelfStatus：起势状态需要以自己为目标");
        SelfStatus = new CompiledConstructionStatus(source.SelfStatus, "Use/SelfStatus");
    }
}
