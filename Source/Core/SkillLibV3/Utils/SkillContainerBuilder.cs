using System;
using System.Collections.Generic;
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Effects;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

public enum SkillContainerBuildMode
{
    Runtime,
    Preview,
    RuleOnly,

    /// <summary>
    /// 构建由体系、装备或其他来源直接授予的只读技能。
    /// 这类容器不要求模板可学习，也不会进入随机命名、改进或上传流程。
    /// </summary>
    SourceGranted
}

public class SkillContainerBuilder
{
    private readonly SkillEntityAsset _entityAsset;
    private int _animationIndex = -1;
    private SkillCastResourceRequirement _castResourceRequirement;
    public SkillContainerBuilder(SkillEntityAsset entity_asset)
    {
        _entityAsset = entity_asset ?? throw new ArgumentNullException(nameof(entity_asset));
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

    public SkillContainerBuilder UseCastResources(SkillCastResourceRequirement requirement)
    {
        _castResourceRequirement = (requirement ?? throw new ArgumentNullException(nameof(requirement))).DeepClone();
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
        if (EntityAsset?.IsRequiredModifier(typeof(TModifier)) == true)
            throw new InvalidOperationException($"{EntityAsset.id} 的必选词条 {typeof(TModifier).Name} 不能删除");
        _modifiersToAdd.Remove(typeof(TModifier));
        _modifiersToSet.Remove(typeof(TModifier));
        _modifiersToRemove.Add(typeof(TModifier), default(TModifier));
    }

    public Entity Build(SkillContainerBuildMode mode = SkillContainerBuildMode.Runtime)
    {
        ApplyRequiredModifiers();
        if (_containerEntity.IsNull)
        {
            if (mode != SkillContainerBuildMode.SourceGranted && !_entityAsset.CanBeLearned)
                throw new InvalidOperationException($"技能实体 {_entityAsset.id} 未声明为可学习技能模板");
            if (_entityAsset.Animations.Count == 0)
                throw new InvalidOperationException($"技能实体 {_entityAsset.id} 没有可用动画");

            var castResourceRequirement = _castResourceRequirement ?? _entityAsset.DefaultCastResourceRequirement;
            if (castResourceRequirement == null || !castResourceRequirement.IsConfigured)
                throw new InvalidOperationException($"技能实体 {_entityAsset.id} 没有配置有效的施法资源需求");

            var animationIndex = _animationIndex;
            if (animationIndex < 0)
            {
                animationIndex = _entityAsset.Animations.Count == 0
                    ? -1
                    : mode == SkillContainerBuildMode.Runtime
                    ? _entityAsset.GetRandomAnimationIndex()
                    : 0;
            }

            _containerEntity = ModClass.I.W.CreateEntity();
            _containerEntity.Add(new SkillContainer()
            {
                SkillEntityAssetID = _entityAsset.id,
                AnimationIndex = animationIndex,
                CastResourceRequirement = castResourceRequirement.DeepClone()
            });
        }
        // 增删词条组件会触发 archetype 迁移，不能跨结构变更持有组件引用。
        var skill_container = _containerEntity.GetComponent<SkillContainer>();
        if (_animationIndex >= 0) skill_container.AnimationIndex = _animationIndex;
        if (_castResourceRequirement != null)
        {
            skill_container.CastResourceRequirement = _castResourceRequirement.DeepClone();
        }
        foreach (var modifier in _modifiersToAdd)
        {
            _containerEntity.AddNonGeneric(modifier.Value);
        }

        foreach (var modifier in _modifiersToRemove)
        {
            _containerEntity.RemoveNonGeneric(modifier.Key);
        }

        foreach (var modifier in _modifiersToSet)
        {
            _containerEntity.SetNonGeneric(modifier.Value);
        }

        RebuildRuntimePipeline(ref skill_container);
        _containerEntity.GetComponent<SkillContainer>() = skill_container;
        
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
        SkillCastParametersResolver.Refresh(_containerEntity);
        SkillCastResourceResolver.Invalidate(_containerEntity);
        SkillContainerEvaluator.Refresh(_containerEntity);
        if (mode == SkillContainerBuildMode.Runtime)
        {
            SkillNameGenerator.Instance.GenerateFor(_containerEntity);
        }
        else if (mode != SkillContainerBuildMode.SourceGranted)
        {
            SkillNameGenerator.Instance.GenerateRuleFor(_containerEntity);
        }

        if (mode == SkillContainerBuildMode.Preview)
        {
            _containerEntity.AddTag<TagOccupied>();
        }
        else if (mode == SkillContainerBuildMode.SourceGranted)
        {
            if (!_containerEntity.HasComponent<SourceGrantedSkill>())
                _containerEntity.AddComponent(new SourceGrantedSkill());
            SourceGrantedSkillRegistry.Master(_containerEntity);
        }
        return _containerEntity;
    }

    /// <summary>把法术本体声明的必选词条物化到新容器或补回被外部构造遗漏的词条。</summary>
    private void ApplyRequiredModifiers()
    {
        SkillEntityAsset asset = EntityAsset;
        if (asset == null) return;
        foreach (var spec in asset.RequiredModifiers)
        {
            SkillModifierAsset modifier = ModClass.I.SkillV3.ModifierLib.get(spec.AssetId);
            if (modifier == null || modifier.EditorComponentType == null)
                throw new InvalidOperationException($"{asset.id} 的必选词条 {spec.AssetId} 未完成注册");
            if (_modifiersToAdd.ContainsKey(modifier.EditorComponentType) ||
                _modifiersToSet.ContainsKey(modifier.EditorComponentType) ||
                (!_containerEntity.IsNull && _containerEntity.HasComponent(modifier.EditorComponentType)))
                continue;
            modifier.Materialize(this, spec.DeepClone());
        }
    }

    /// <summary>从法术本体和实体上的真实词条重新编译委托及类型化效果，避免增删操作留下重复回调。</summary>
    private void RebuildRuntimePipeline(ref SkillContainer container)
    {
        container.OnSetup = null;
        container.OnTravel = null;
        container.OnEffectObj = null;
        var effects = new List<SkillEffectDescriptor>(container.Asset.Effects);
        foreach (Type componentType in _containerEntity.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(componentType)) continue;
            var modifier = (IModifier)_containerEntity.GetComponent(componentType);
            SkillModifierAsset asset = modifier.ModifierAsset;
            if (asset == null) continue;
            container.OnSetup += asset.OnSetup;
            container.OnTravel += asset.OnTravel;
            container.OnEffectObj += asset.OnEffectObj;
            effects.AddRange(asset.Effects);
        }
        container.EffectPipeline = effects.Count == 0
            ? SkillEffectPipeline.Empty
            : new SkillEffectPipeline(effects);
    }
}
