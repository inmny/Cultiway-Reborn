using System;
using System.Collections.Generic;
using System.Reflection;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Modifiers;
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
public delegate void TravelAction(Entity skill_entity);
public delegate void EffectObjAction(Entity skill_entity, BaseSimObject obj);
public delegate bool AddOrUpgradeAction(SkillContainerBuilder builder);
public delegate string GetDescription(Entity skill_entity);
public delegate void SkillModifierNormalizeAction(SkillContainerBuilder builder, SkillModifierSpec spec);
public delegate SkillCompatibilityResult SkillModifierCompatibilityAction(SkillEditContext context,
    SkillModifierSpec spec);
public class SkillModifierAsset : Asset
{
    /// <summary>
    /// 词条稀有度
    /// </summary>
    public SkillModifierRarity Rarity = SkillModifierRarity.Common;

    /// <summary>
    /// 权重修正，用于细调该词条的抽取概率（默认为1）
    /// </summary>
    public float WeightMod = 1f;

    /// <summary>
    /// 冲突标签，用于互斥判定
    /// </summary>
    public HashSet<string> ConflictTags { get; } = new();

    /// <summary>
    /// 相似度标签，用于避免同类法术词条过于雷同
    /// </summary>
    public HashSet<string> SimilarityTags { get; } = new();

    public Type EditorComponentType;
    public string EditorCategoryKey;
    public string EditorIconPath;
    public int EditorSortOrder;
    public bool EditorSelectable;
    public bool EditorDerived;
    public bool EditorPersistWhenHidden;
    public List<SkillEditorFieldAsset> EditorFields { get; } = new();
    public HashSet<string> EditorSemanticTags { get; } = new(StringComparer.Ordinal);
    public SkillModifierNormalizeAction EditorNormalize;
    public SkillModifierCompatibilityAction EditorCompatibility;
    
    /// <summary>
    /// 是否禁用该词条（禁用的词条不会被抽取）
    /// </summary>
    public bool IsDisabled = false;
    
    public SetupAction OnSetup;
    public TravelAction OnTravel;
    public EffectObjAction OnEffectObj;
    public AddOrUpgradeAction OnAddOrUpgrade;
    public GetDescription GetDescription;

    public SkillModifierAsset AddSimilarityTags(params string[] tags)
    {
        if (tags == null) return this;
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag)) continue;
            SimilarityTags.Add(tag);
        }
        return this;
    }

    public SkillModifierAsset BindEditor<TModifier>() where TModifier : struct, IModifier
    {
        EditorComponentType = typeof(TModifier);
        return this;
    }

    public SkillModifierSpec CreateDefaultSpec()
    {
        var spec = new SkillModifierSpec { AssetId = id };
        foreach (var field in EditorFields)
        {
            spec.Parameters[field.ParameterKey] = field.DefaultValue;
        }
        return spec;
    }

    public void Materialize(SkillContainerBuilder builder, SkillModifierSpec spec)
    {
        object component = Activator.CreateInstance(EditorComponentType);
        foreach (var field in EditorFields)
        {
            var componentField = EditorComponentType.GetField(field.ParameterKey,
                                     BindingFlags.Instance | BindingFlags.Public)
                                 ?? throw new InvalidOperationException(
                                     string.Format("Cultiway.SkillEditor.ComponentFieldMissing".Localize(),
                                         EditorComponentType.FullName, field.ParameterKey));
            componentField.SetValue(component, field.Deserialize(componentField.FieldType,
                spec.Parameters[field.ParameterKey]));
        }
        builder.AddModifier((IModifier)component);
    }

    public SkillModifierSpec Export(Entity container)
    {
        var component = container.GetComponent(EditorComponentType);
        var spec = new SkillModifierSpec { AssetId = id };
        foreach (var field in EditorFields)
        {
            var componentField = EditorComponentType.GetField(field.ParameterKey,
                                     BindingFlags.Instance | BindingFlags.Public)
                                 ?? throw new InvalidOperationException(
                                     string.Format("Cultiway.SkillEditor.ComponentFieldMissing".Localize(),
                                         EditorComponentType.FullName, field.ParameterKey));
            spec.Parameters[field.ParameterKey] = field.Serialize(componentField.GetValue(component));
        }
        return spec;
    }

    public SkillCompatibilityResult CheckEditorCompatibility(SkillEditContext context, SkillModifierSpec spec)
    {
        return EditorCompatibility == null
            ? new SkillCompatibilityResult()
            : EditorCompatibility(context, spec);
    }
}
