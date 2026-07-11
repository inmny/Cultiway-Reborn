using System;
using System.Collections.Generic;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

public enum SkillContainerBuildMode
{
    Runtime,
    Preview,
    RuleOnly
}

public class SkillContainerBuilder
{
    private readonly SkillEntityAsset _entityAsset;
    private int _animationIndex = -1;
    public SkillContainerBuilder(SkillEntityAsset entity_asset)
    {
        this._entityAsset = entity_asset;
    }
    private Entity _containerEntity;
    public SkillContainerBuilder(Entity container_entity)
    {
        this._containerEntity = container_entity;
    }

    /// <summary>
    /// 解析当前构建器所操作的技能资产。
    /// 新建容器走 <see cref="SkillEntityAsset"/> 构造分支；升级已有容器则从其
    /// <see cref="SkillContainer"/> 组件的 <see cref="SkillContainer.Asset"/> 回查。
    /// 用于让词条的 <c>OnAddOrUpgrade</c> 回调读取法术侧的约束（例如方向姿态）。
    /// </summary>
    public SkillEntityAsset EntityAsset
    {
        get
        {
            if (_entityAsset != null) return _entityAsset;
            if (!_containerEntity.IsNull && _containerEntity.HasComponent<SkillContainer>())
            {
                return _containerEntity.GetComponent<SkillContainer>().Asset;
            }

            return null;
        }
    }

    public bool HasModifier<TModifier>() where TModifier : struct, IModifier
    {
        if (_modifiersToAdd.ContainsKey(typeof(TModifier))) return true;
        if (_modifiersToRemove.ContainsKey(typeof(TModifier))) return false;
        return !_containerEntity.IsNull && _containerEntity.HasComponent<TModifier>();
    }

    public TModifier GetModifier<TModifier>() where TModifier : struct, IModifier
    {
        if (_modifiersToAdd.TryGetValue(typeof(TModifier), out var modifier))
        {
            return (TModifier)modifier;
        }
        if (_containerEntity.IsNull) return default(TModifier);
        return _containerEntity.GetComponent<TModifier>();
    }

    public void SetModifier<TModifier>(TModifier modifier) where TModifier : struct, IModifier
    {
        _modifiersToRemove.Remove(typeof(TModifier));
        _modifiersToSet[typeof(TModifier)] = modifier;
    }

    public SkillContainerBuilder UseAnimation(int animationIndex)
    {
        var entityAsset = EntityAsset;
        if (animationIndex < 0 || animationIndex >= entityAsset.Animations.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(animationIndex));
        }
        _animationIndex = animationIndex;
        return this;
    }
    private readonly Dictionary<Type, IModifier> _modifiersToSet = new Dictionary<Type, IModifier>();
    private readonly Dictionary<Type, IModifier> _modifiersToAdd = new Dictionary<Type, IModifier>();
    private readonly Dictionary<Type, IModifier> _modifiersToRemove = new Dictionary<Type, IModifier>();
    public void AddModifier<TModifier>(TModifier modifier) where TModifier : struct, IModifier
    {
        _modifiersToAdd[typeof(TModifier)] = modifier;
        _modifiersToRemove.Remove(typeof(TModifier));
    }

    public void AddModifier(IModifier modifier)
    {
        var type = modifier.GetType();
        _modifiersToAdd[type] = modifier;
        _modifiersToRemove.Remove(type);
    }

    public void RemoveModifier<TModifier>() where TModifier : struct, IModifier
    {
        _modifiersToAdd.Remove(typeof(TModifier));
        _modifiersToSet.Remove(typeof(TModifier));
        _modifiersToRemove.Add(typeof(TModifier), default(TModifier));
    }

    public Entity Build(SkillContainerBuildMode mode = SkillContainerBuildMode.Runtime)
    {
        if (_containerEntity.IsNull)
        {
            _containerEntity = ModClass.I.W.CreateEntity();
            var animationIndex = _animationIndex;
            if (animationIndex < 0)
            {
                animationIndex = mode == SkillContainerBuildMode.Runtime
                    ? _entityAsset.GetRandomAnimationIndex()
                    : 0;
            }
            _containerEntity.Add(new SkillContainer()
            {
                SkillEntityAssetID = _entityAsset.id,
                AnimationIndex = animationIndex
            });
        }
        ref var skill_container = ref _containerEntity.GetComponent<SkillContainer>();
        if (_animationIndex >= 0) skill_container.AnimationIndex = _animationIndex;
        foreach (var modifier in _modifiersToAdd)
        {
            _containerEntity.AddNonGeneric(modifier.Value);
            skill_container.OnSetup += modifier.Value.ModifierAsset.OnSetup;
            skill_container.OnTravel += modifier.Value.ModifierAsset.OnTravel;
            skill_container.OnEffectObj += modifier.Value.ModifierAsset.OnEffectObj;
        }

        foreach (var modifier in _modifiersToRemove)
        {
            _containerEntity.RemoveNonGeneric(modifier.Key);
            skill_container.OnSetup -= modifier.Value.ModifierAsset.OnSetup;
            skill_container.OnTravel -= modifier.Value.ModifierAsset.OnTravel;
            skill_container.OnEffectObj -= modifier.Value.ModifierAsset.OnEffectObj;
        }

        foreach (var modifier in _modifiersToSet)
        {
            _containerEntity.SetNonGeneric(modifier.Value);
        }
        
        // 如果OnTravel非空，添加tag用于过滤
        if (skill_container.OnTravel != null)
        {
            _containerEntity.AddTag<TagHasOnTravel>();
        }
        else
        {
            _containerEntity.RemoveTag<TagHasOnTravel>();
        }

        SkillContainerUtils.RefreshVfxElement(_containerEntity);
        SkillContainerUtils.RefreshMotionProfile(_containerEntity);
        if (mode == SkillContainerBuildMode.Runtime)
        {
            SkillNameGenerator.Instance.GenerateFor(_containerEntity);
        }
        else
        {
            SkillNameGenerator.Instance.GenerateRuleFor(_containerEntity);
        }

        if (mode == SkillContainerBuildMode.Preview)
        {
            _containerEntity.AddTag<TagOccupied>();
        }
        return _containerEntity;
    }
}
