using System;

namespace Cultiway.Content.CreatureCompositions.Libraries;

/// <summary>器官能够占用的身体位置类别。</summary>
[Flags]
public enum CreatureOrganCategoryMask : ushort
{
    None = 0,
    Surface = 1 << 0,
    Locomotion = 1 << 1,
    NaturalWeapon = 1 << 2,
    Breath = 1 << 3,
    Perception = 1 << 4,
    Metabolism = 1 << 5,
    Appendage = 1 << 6,
    Spirit = 1 << 7,
    Neural = 1 << 8,
    Other = 1 << 9,
    All = Surface | Locomotion | NaturalWeapon | Breath | Perception | Metabolism | Appendage | Spirit |
          Neural | Other
}

/// <summary>一个身体位置在画面中的对称方式。</summary>
public enum CreatureSymmetryMode : byte
{
    Single,
    Paired,
    Merged
}

/// <summary>固定形态使用的基础移动方式。</summary>
public enum CreatureLocomotionKind : byte
{
    Ground,
    Flying,
    Aquatic,
    Amphibious,
    Stationary
}

/// <summary>一个器官对指定身体位置的占用量。</summary>
public readonly struct CreatureSlotRequirement
{
    public readonly string SlotId;
    public readonly int Capacity;

    public CreatureSlotRequirement(string slotId, int capacity = 1)
    {
        SlotId = slotId;
        Capacity = capacity;
    }
}

/// <summary>固定形态对身体位置容量的增加量。</summary>
public readonly struct CreatureSlotCapacityChange
{
    public readonly string SlotId;
    public readonly int AddedCapacity;

    public CreatureSlotCapacityChange(string slotId, int addedCapacity)
    {
        SlotId = slotId;
        AddedCapacity = addedCapacity;
    }
}

/// <summary>器官提供的一项 WorldBox 基础属性。</summary>
public readonly struct CreatureStatValue
{
    public readonly string StatId;
    public readonly float Value;

    public CreatureStatValue(string statId, float value)
    {
        StatId = statId;
        Value = value;
    }
}

/// <summary>器官提供的一个被动效果族及其等级。</summary>
public readonly struct CreatureEffectRank
{
    public readonly string EffectFamilyId;
    public readonly int Rank;

    public CreatureEffectRank(string effectFamilyId, int rank)
    {
        EffectFamilyId = effectFamilyId;
        Rank = rank;
    }
}

/// <summary>器官需要由所属玩法支付的周期维护量。</summary>
public readonly struct CreatureUpkeepDescriptor
{
    public readonly string ResourceId;
    public readonly float AmountPerWorldMonth;

    public CreatureUpkeepDescriptor(string resourceId, float amountPerWorldMonth)
    {
        ResourceId = resourceId;
        AmountPerWorldMonth = amountPerWorldMonth;
    }
}
