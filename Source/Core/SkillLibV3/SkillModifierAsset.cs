using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 词条稀有度（普通/稀有/罕见/珍稀）
/// </summary>
public enum SkillModifierRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// 挂接在技能实体上的持续特效请求
/// </summary>
public struct AttachAnimRequest
{
    public string PrefabId;
    public Vector3 Offset;
    public Vector3 Scale;
    public bool FollowTarget;
    public bool Loop;
}

/// <summary>
/// 一次性生成的命中特效请求
/// </summary>
public struct SpawnAnimRequest
{
    public string PrefabId;
    public Vector3 Position;
    public Vector3 Forward;
    public float LifeTime;
}

/// <summary>
/// 可选接口：用于在构建阶段生成跟随特效
/// </summary>
public interface IAttachAnimRequestProvider
{
    IEnumerable<AttachAnimRequest> GetAttachAnimRequests(Entity skill_entity);
}

/// <summary>
/// 可选接口：用于在命中时生成一次性特效
/// </summary>
public interface ISpawnAnimRequestProvider
{
    IEnumerable<SpawnAnimRequest> GetSpawnAnimRequests(Entity skill_entity, BaseSimObject obj);
}

public delegate void SetupAction(Entity skill_entity);
public delegate void EffectObjAction(Entity skill_entity, BaseSimObject obj);
public delegate bool AddOrUpgradeAction(SkillContainerBuilder builder);
public delegate string GetDescription(Entity skill_entity);
public class SkillModifierAsset : Asset
{
    /// <summary>
    /// 词条稀有度
    /// </summary>
    public SkillModifierRarity Rarity = SkillModifierRarity.Common;

    /// <summary>
    /// 冲突标签，用于互斥判定
    /// </summary>
    public HashSet<string> ConflictTags { get; } = new();
    public SetupAction OnSetup;
    public EffectObjAction OnEffectObj;
    public AddOrUpgradeAction OnAddOrUpgrade;
    public GetDescription GetDescription;
}
