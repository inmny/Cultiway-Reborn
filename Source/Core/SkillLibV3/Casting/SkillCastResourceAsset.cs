using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Components;

namespace Cultiway.Core.SkillLibV3;

public delegate bool SkillCastResourceAvailability(ActorExtend caster);

/// <summary>读取原资源的实际余额或容量，整数不会经过单精度中转。</summary>
/// <param name="caster">资源所属人物。</param>
public delegate double SkillCastResourceAmountReader(ActorExtend caster);

/// <summary>由资源所属玩法写入余额或扣除费用。</summary>
/// <param name="caster">实际人物。</param><param name="amount">已经确定的余额或费用。</param>
public delegate void SkillCastResourceAmountWriter(ActorExtend caster, double amount);

/// <summary>把已经计算好的施法需求换算为当前人物的实际资源费用。</summary>
/// <param name="caster">实际支付者。</param><param name="demand">已经包含范围和词条的需求。</param>
public delegate float SkillCastResourceQuote(ActorExtend caster, float demand);

public delegate string SkillCastResourceItemLevelFormatter(ItemLevel itemLevel);

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

    /// <summary>支付已经报价的费用；不能顺带夹掉原本超过容量的余额。</summary>
    public SkillCastResourceAmountWriter SpendAmount;

    /// <summary>资源自身的当前容量；回收只能进入已有容量。</summary>
    public SkillCastResourceAmountReader MaximumAmount;

    /// <summary>此资源只能按整数保存，累计回收后才向下取整。</summary>
    public bool IntegerAmount;

    public SkillCastResourceItemLevelFormatter ItemLevelFormatter = itemLevel => itemLevel.GetName();
    public string EditorDescriptionKey;
    public string EditorIconPath;
    public int EditorSortOrder;
    public bool EditorSelectable;
    public bool EditorPersistWhenHidden;

    public SkillCastResourceAsset Configure(SkillCastResourceAvailability isAvailable,
        SkillCastResourceAmountReader readAmount, SkillCastResourceAmountWriter writeAmount,
        SkillCastResourceQuote quote)
    {
        IsAvailable = isAvailable;
        ReadAmount = readAmount;
        WriteAmount = writeAmount;
        SpendAmount = (caster, amount) => writeAmount(caster, readAmount(caster) - amount);
        Quote = quote;
        return this;
    }

    /// <summary>资源所属玩法可提供专用扣费，保留整数精度及资源变化通知。</summary>
    /// <param name="spendAmount">只扣除本次费用的真实入口。</param>
    public SkillCastResourceAsset ConfigureSpending(SkillCastResourceAmountWriter spendAmount)
    {
        SpendAmount = spendAmount;
        return this;
    }

    /// <summary>登记实际容量和整数规则，供生成材料收束及日常需求共用。</summary>
    /// <param name="maximumAmount">资源所属玩法提供的当前容量。</param>
    /// <param name="integerAmount">是否只能保存整数。</param>
    public SkillCastResourceAsset ConfigureRecovery(SkillCastResourceAmountReader maximumAmount,
        bool integerAmount = false)
    {
        MaximumAmount = maximumAmount;
        IntegerAmount = integerAmount;
        return this;
    }

    public SkillCastResourceAsset ConfigureEditor(string descriptionKey, string iconPath, int sortOrder = 0,
        bool selectable = true, bool persistWhenHidden = true)
    {
        EditorDescriptionKey = descriptionKey;
        EditorIconPath = iconPath;
        EditorSortOrder = sortOrder;
        EditorSelectable = selectable;
        EditorPersistWhenHidden = persistWhenHidden;
        return this;
    }

    public SkillCastResourceAsset ConfigureItemLevelDisplay(SkillCastResourceItemLevelFormatter formatter)
    {
        ItemLevelFormatter = formatter;
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
/// 法术容器绑定的资源语义。顺序具有意义：AnyOf 按声明顺序选择首个能够支付本次施法的通道。
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