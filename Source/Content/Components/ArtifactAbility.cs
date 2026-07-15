using System;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 法器能力参数支持的基础值类型。复杂能力可在法器本体上附加专用组件保存额外结构。
/// </summary>
public enum ArtifactAbilityValueKind
{
    Number,
    Integer,
    Boolean,
    AssetId,
}

/// <summary>
/// 一个经过能力资产参数规格校验的命名值。
/// </summary>
public struct ArtifactAbilityValue
{
    public string key;
    public ArtifactAbilityValueKind kind;
    public float number;
    public int integer;
    public bool boolean;
    public string asset_id;

    public static ArtifactAbilityValue Number(string key, float value)
    {
        return new ArtifactAbilityValue { key = key, kind = ArtifactAbilityValueKind.Number, number = value };
    }

    public static ArtifactAbilityValue Integer(string key, int value)
    {
        return new ArtifactAbilityValue { key = key, kind = ArtifactAbilityValueKind.Integer, integer = value };
    }

    public static ArtifactAbilityValue Boolean(string key, bool value)
    {
        return new ArtifactAbilityValue { key = key, kind = ArtifactAbilityValueKind.Boolean, boolean = value };
    }

    public static ArtifactAbilityValue AssetId(string key, string value)
    {
        return new ArtifactAbilityValue { key = key, kind = ArtifactAbilityValueKind.AssetId, asset_id = value };
    }
}

/// <summary>
/// 一件法器上的一个能力实例。能力 ID 决定行为，参数仅保存该能力声明过的配置。
/// </summary>
public struct ArtifactAbilityInstance
{
    public string instance_id;
    public string ability_id;
    public ArtifactAbilityValue[] parameters = [];
    public ArtifactAbilityValue[] initial_state = [];

    public ArtifactAbilityInstance()
    {
    }

    public float GetNumber(string key)
    {
        return ArtifactAbilityValues.Get(parameters, ability_id, key, ArtifactAbilityValueKind.Number).number;
    }

    public int GetInteger(string key)
    {
        return ArtifactAbilityValues.Get(parameters, ability_id, key, ArtifactAbilityValueKind.Integer).integer;
    }

    public bool GetBoolean(string key)
    {
        return ArtifactAbilityValues.Get(parameters, ability_id, key, ArtifactAbilityValueKind.Boolean).boolean;
    }

    public string GetAssetId(string key)
    {
        return ArtifactAbilityValues.Get(parameters, ability_id, key, ArtifactAbilityValueKind.AssetId).asset_id;
    }
}

/// <summary>
/// 法器拥有的全部能力定义。数组顺序稳定，并与运行状态数组一一对应。
/// </summary>
public struct ArtifactAbilitySet : IComponent
{
    public ArtifactAbilityInstance[] abilities = [];

    public ArtifactAbilitySet()
    {
    }
}

/// <summary>
/// 单个能力实例的可变运行状态。具体字段由能力资产自己的状态规格解释。
/// </summary>
public struct ArtifactAbilityRuntimeEntry
{
    public string instance_id;
    public ArtifactAbilityValue[] values = [];

    public ArtifactAbilityRuntimeEntry()
    {
    }

    public float GetNumber(string key)
    {
        return ArtifactAbilityValues.Get(values, instance_id, key, ArtifactAbilityValueKind.Number).number;
    }

    public int GetInteger(string key)
    {
        return ArtifactAbilityValues.Get(values, instance_id, key, ArtifactAbilityValueKind.Integer).integer;
    }

    public bool GetBoolean(string key)
    {
        return ArtifactAbilityValues.Get(values, instance_id, key, ArtifactAbilityValueKind.Boolean).boolean;
    }

    public string GetAssetId(string key)
    {
        return ArtifactAbilityValues.Get(values, instance_id, key, ArtifactAbilityValueKind.AssetId).asset_id;
    }

    public void SetNumber(string key, float value)
    {
        int index = ArtifactAbilityValues.FindIndex(values, instance_id, key, ArtifactAbilityValueKind.Number);
        values[index].number = value;
    }

    public void SetInteger(string key, int value)
    {
        int index = ArtifactAbilityValues.FindIndex(values, instance_id, key, ArtifactAbilityValueKind.Integer);
        values[index].integer = value;
    }

    public void SetBoolean(string key, bool value)
    {
        int index = ArtifactAbilityValues.FindIndex(values, instance_id, key, ArtifactAbilityValueKind.Boolean);
        values[index].boolean = value;
    }

    public void SetAssetId(string key, string value)
    {
        int index = ArtifactAbilityValues.FindIndex(values, instance_id, key, ArtifactAbilityValueKind.AssetId);
        values[index].asset_id = value;
    }
}

/// <summary>
/// 法器全部能力的可变运行状态，与 <see cref="ArtifactAbilitySet"/> 分开保存。
/// </summary>
public struct ArtifactAbilityRuntime : IComponent
{
    public ArtifactAbilityRuntimeEntry[] abilities = [];

    public ArtifactAbilityRuntime()
    {
    }

    /// <summary>
    /// 为一件新制造的法器从能力固有初态创建独立运行状态。
    /// </summary>
    public static ArtifactAbilityRuntime CreateInitial(ArtifactAbilitySet abilitySet)
    {
        ArtifactAbilityRuntimeEntry[] entries = new ArtifactAbilityRuntimeEntry[abilitySet.abilities.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            entries[i] = new ArtifactAbilityRuntimeEntry
            {
                instance_id = ability.instance_id,
                values = ArtifactAbilityValues.Clone(ability.initial_state),
            };
        }
        return new ArtifactAbilityRuntime { abilities = entries };
    }
}

internal static class ArtifactAbilityValues
{
    internal static ArtifactAbilityValue[] Clone(ArtifactAbilityValue[] values)
    {
        if (values == null || values.Length == 0) return [];
        ArtifactAbilityValue[] copy = new ArtifactAbilityValue[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }

    internal static ArtifactAbilityValue Get(
        ArtifactAbilityValue[] values,
        string abilityId,
        string key,
        ArtifactAbilityValueKind expectedKind)
    {
        return values[FindIndex(values, abilityId, key, expectedKind)];
    }

    internal static int FindIndex(
        ArtifactAbilityValue[] values,
        string abilityId,
        string key,
        ArtifactAbilityValueKind expectedKind)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].key != key) continue;
            if (values[i].kind != expectedKind)
            {
                throw new InvalidOperationException($"法器能力参数 {abilityId}.{key} 类型不匹配");
            }
            return i;
        }
        throw new InvalidOperationException($"法器能力缺少参数 {abilityId}.{key}");
    }
}
