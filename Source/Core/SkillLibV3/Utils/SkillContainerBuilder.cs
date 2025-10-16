using System;
using System.Collections.Generic;
using Cultiway.Core.AIGCLib;
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
            skill_container.OnEffectObj += modifier.Value.ModifierAsset.OnEffectObj;
        }

        foreach (var modifier in _modifiersToRemove)
        {
            _containerEntity.RemoveNonGeneric(modifier.Key);
            skill_container.OnSetup -= modifier.Value.ModifierAsset.OnSetup;
            skill_container.OnEffectObj -= modifier.Value.ModifierAsset.OnEffectObj;
        }
        SkillNameGenerator.Instance.GenerateFor(_containerEntity);
        return _containerEntity;
    }
}