using System;
using Friflo.Engine.ECS;
using UnityEngine;

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
/// 能力当前占用法器本体的方式。瞬时能力不建立活动，持续能力由对应类型决定如何结束。
/// </summary>
public enum ArtifactAbilityActivityKind
{
    /// <summary>能力当前没有占用法器本体的持续活动。</summary>
    None,

    /// <summary>仅由通用持续时间推进的活动。</summary>
    Timed,

    /// <summary>由一个短时 SkillExecution 驱动的活动。</summary>
    SkillExecution,

    /// <summary>法器本体脱离驾驭者并驻留在世界中的活动。</summary>
    Deployment,
}

/// <summary>
/// 一次持续能力活动结束的原因，供能力释放资源或产生收尾效果。
/// </summary>
public enum ArtifactAbilityEndReason
{
    /// <summary>活动载体正常完成。</summary>
    Completed,

    /// <summary>配置的持续时间耗尽。</summary>
    DurationElapsed,

    /// <summary>驾驭者主动召回。</summary>
    Recalled,

    /// <summary>法器控制状态低于活动维持要求。</summary>
    ControlStateLost,

    /// <summary>法器与驾驭者解除装备关系。</summary>
    Unequipped,

    /// <summary>驾驭者死亡或失去有效实体。</summary>
    ControllerLost,

    /// <summary>无法继续支付活动维持资源。</summary>
    ResourceDepleted,

    /// <summary>同一法器改由另一驾驭者接管。</summary>
    Replaced,
}

/// <summary>
/// 单个能力实例的可变运行状态。values 由能力资产自己的状态规格解释，其余字段由通用生命周期维护。
/// </summary>
public struct ArtifactAbilityRuntimeEntry
{
    public string instance_id;
    public ArtifactAbilityValue[] values = [];

    /// <summary>通用生命周期运行头是否已经按能力资产初始化。</summary>
    public bool lifecycle_initialized;

    /// <summary>当前可用充能。能力未配置充能上限时该字段不参与判断。</summary>
    public int charges;

    /// <summary>下一次允许触发的世界时间。</summary>
    public double cooldown_until;

    /// <summary>下一点充能恢复的世界时间。</summary>
    public double next_charge_at;

    /// <summary>下一次周期回调的世界时间。</summary>
    public double next_tick_at;

    /// <summary>最近一次成功触发的世界时间。</summary>
    public double last_triggered_at;

    /// <summary>当前持续活动开始的世界时间。</summary>
    public double activity_started_at;

    /// <summary>当前持续活动的自动结束时间；0 表示不由时长结束。</summary>
    public double activity_until;

    /// <summary>当前持续活动如何占用法器本体。</summary>
    public ArtifactAbilityActivityKind activity_kind;

    /// <summary>由能力启动并与法器本体绑定的短时技能执行会话。</summary>
    public Entity active_execution;

    /// <summary>持续活动的权威作用点；部署、投影和定点领域共用。</summary>
    public Vector3 activity_position;

    /// <summary>持续活动的权威朝向；扇形、束流和定向投影共用。</summary>
    public Vector3 activity_direction;

    /// <summary>当前活动是否显式声明了作用点。</summary>
    public bool has_activity_position;

    /// <summary>当前活动是否显式声明了朝向。</summary>
    public bool has_activity_direction;

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

    /// <summary>生命周期当前观察到的驾驭者。</summary>
    public Entity controller;

    /// <summary>生命周期当前观察到的法器控制状态。</summary>
    public ArtifactControlState control_state;

    /// <summary>法器当前是否已经接入某个驾驭者的能力生命周期。</summary>
    public bool attached;

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
