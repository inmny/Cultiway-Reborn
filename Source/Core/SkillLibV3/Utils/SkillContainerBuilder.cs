using System;
using System.Collections.Generic;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

public class SkillContainerBuilder
{
    private readonly SkillEntityAsset _entityAsset;
    public SkillContainerBuilder(SkillEntityAsset entity_asset)
    {
        this._entityAsset = entity_asset;
    }
    private Entity _containerEntity;
    public SkillContainerBuilder(Entity container_entity)
    {
        this._containerEntity = container_entity;
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
    private readonly Dictionary<Type, IModifier> _modifiersToSet = new Dictionary<Type, IModifier>();
    private readonly Dictionary<Type, IModifier> _modifiersToAdd = new Dictionary<Type, IModifier>();
    private readonly Dictionary<Type, IModifier> _modifiersToRemove = new Dictionary<Type, IModifier>();
    public void AddModifier<TModifier>(TModifier modifier) where TModifier : struct, IModifier
    {
        _modifiersToAdd[typeof(TModifier)] = modifier;
        _modifiersToRemove.Remove(typeof(TModifier));
    }

    public void RemoveModifier<TModifier>() where TModifier : struct, IModifier
    {
        _modifiersToAdd.Remove(typeof(TModifier));
        _modifiersToSet.Remove(typeof(TModifier));
        _modifiersToRemove.Add(typeof(TModifier), default(TModifier));
    }

    public Entity Build()
    {
        if (_containerEntity.IsNull)
        {
            _containerEntity = ModClass.I.W.CreateEntity();
            _containerEntity.Add(new SkillContainer()
            {
                SkillEntityAssetID = _entityAsset.id
            });
        }
        ref var skill_container = ref _containerEntity.GetComponent<SkillContainer>();
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
        
        SkillNameGenerator.Instance.GenerateFor(_containerEntity);
        return _containerEntity;
    }
}