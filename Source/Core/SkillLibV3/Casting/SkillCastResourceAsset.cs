using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

public delegate bool SkillCastResourceAvailability(ActorExtend caster);
public delegate float SkillCastResourceAmountReader(ActorExtend caster);
public delegate void SkillCastResourceAmountWriter(ActorExtend caster, float amount);
public delegate float SkillCastResourceQuote(ActorExtend caster, Entity skill, float demand);

/// <summary>
/// 一种可被法术声明为消耗目标的施法资源通道。
/// 具体体系负责注册可用性、余额读写和需求报价。
/// </summary>
public sealed class SkillCastResourceAsset : Asset
{
    public SkillCastResourceAvailability IsAvailable;
    public SkillCastResourceAmountReader ReadAmount;
    public SkillCastResourceAmountWriter WriteAmount;
    public SkillCastResourceQuote Quote;

    public SkillCastResourceAsset Configure(SkillCastResourceAvailability isAvailable,
        SkillCastResourceAmountReader readAmount, SkillCastResourceAmountWriter writeAmount,
        SkillCastResourceQuote quote)
    {
        IsAvailable = isAvailable;
        ReadAmount = readAmount;
        WriteAmount = writeAmount;
        Quote = quote;
        return this;
    }
}

public sealed class SkillCastResourceLibrary : AssetLibrary<SkillCastResourceAsset>
{
}

public enum SkillCastResourceRequirementMode
{
    Single,
    AnyOf,
    AllOf
}

/// <summary>
/// 法术容器绑定的资源语义。顺序具有意义：AnyOf 按声明顺序选择首个可用通道。
/// </summary>
[Serializable]
public sealed class SkillCastResourceRequirement
{
    public SkillCastResourceRequirementMode Mode;
    public List<string> ResourceAssetIds = new();

    public bool IsConfigured => ResourceAssetIds is { Count: > 0 } &&
                                ResourceAssetIds.All(id => !string.IsNullOrWhiteSpace(id));

    public SkillCastResourceRequirement DeepClone()
    {
        return new SkillCastResourceRequirement
        {
            Mode = Mode,
            ResourceAssetIds = ResourceAssetIds == null
                ? new List<string>()
                : new List<string>(ResourceAssetIds)
        };
    }

    public static SkillCastResourceRequirement Single(SkillCastResourceAsset resource)
    {
        return Create(SkillCastResourceRequirementMode.Single, resource);
    }

    public static SkillCastResourceRequirement AnyOf(params SkillCastResourceAsset[] resources)
    {
        return Create(SkillCastResourceRequirementMode.AnyOf, resources);
    }

    public static SkillCastResourceRequirement AllOf(params SkillCastResourceAsset[] resources)
    {
        return Create(SkillCastResourceRequirementMode.AllOf, resources);
    }

    private static SkillCastResourceRequirement Create(SkillCastResourceRequirementMode mode,
        params SkillCastResourceAsset[] resources)
    {
        return new SkillCastResourceRequirement
        {
            Mode = mode,
            ResourceAssetIds = resources.Select(resource => resource.id).Distinct(StringComparer.Ordinal).ToList()
        };
    }
}

/// <summary>
/// 某个施法者与某个法术之间已经解析完成的资源绑定。
/// </summary>
public sealed class SkillCastResourceBinding
{
    public SkillCastResourceRequirementMode Mode { get; }
    public IReadOnlyList<SkillCastResourceAsset> Resources { get; }

    internal SkillCastResourceBinding(SkillCastResourceRequirementMode mode,
        IReadOnlyList<SkillCastResourceAsset> resources)
    {
        Mode = mode;
        Resources = resources;
    }

    public bool Contains(SkillCastResourceAsset resource)
    {
        return Resources.Any(item => item == resource);
    }
}
