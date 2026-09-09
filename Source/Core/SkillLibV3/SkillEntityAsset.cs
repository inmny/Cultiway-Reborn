using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public enum SkillEntityType
{
    Attack,
    Defense,
    Support,
    Utility,
}

/// <summary>技能可以由哪一种人物载体执行。</summary>
public enum SkillCarrierRequirement : byte
{
    /// <summary>物质肉身与魂体都能执行。</summary>
    General,

    /// <summary>必须由仍有物质肉身的人物本体执行。</summary>
    PhysicalBody,

    /// <summary>必须由魂体或无身元神执行。</summary>
    Soul,
}
public delegate bool OnObjCollision(ref SkillContext context, Entity skill_container, Entity skill_entity, BaseSimObject target);
public class SkillEntityAsset : Asset
{
    private readonly Dictionary<string, float> _modifierWeightMultipliers = new(StringComparer.Ordinal);
    private readonly List<SkillEntityAnimation> _animations = new();
    private readonly List<SkillEffectDescriptor> effects = new();
    private readonly List<SkillModifierSpec> requiredModifiers = new();

    internal float DefaultAnimationFrameInterval { get; private set; } = 0.1f;
    internal bool DefaultAnimationLoop { get; private set; } = true;
    public Entity PrefabEntity;
    public ElementComposition Element;
    public SemanticDescriptor Semantics { get; set; } = new();
    public IReadOnlyList<SkillEntityAnimation> Animations => _animations;
    public IReadOnlyList<SkillEffectDescriptor> Effects => effects;
    public IReadOnlyList<SkillModifierSpec> RequiredModifiers => requiredModifiers;
    public string EditorCategoryKey;
    public string EditorDescriptionKey;
    public int EditorSortOrder;
    public bool EditorSelectable;
    /// <summary>技能列表、编辑器与主动能力入口使用的独立图标。</summary>
    public Sprite Icon { get; private set; }
    /// <summary>技能在世界中结算时使用的结构化视觉配置。</summary>
    public SkillWorldVisualProfile WorldVisualProfile { get; private set; }
    /// <summary>该实体能否作为角色持有、改进和释放的技能容器模板。</summary>
    public bool CanBeLearned { get; private set; }
    public EntityStore World => ModClass.I.SkillV3.World;
    public OnObjCollision OnObjCollision;
    public SkillEntityType Type;
    /// <summary>技能执行所需的人物载体；默认身魂皆可。</summary>
    public SkillCarrierRequirement CarrierRequirement { get; private set; }
    public SkillImpactProfileAsset ImpactProfile { get; private set; }
    public SkillImpactTuning ImpactTuning { get; } = new();
    public SkillUseProfileAsset UseProfile { get; private set; }
    public SkillCastResourceRequirement DefaultCastResourceRequirement { get; private set; }
    public float BaseCastDemand { get; private set; } = 1f;
    public float AreaCostBaseRadius { get; private set; }
    public bool DealsBaseDamage { get; private set; } = true;
    private Func<Entity, float> runtimeLifetimeResolver;

    /// <summary>设置独立 UI 图标，避免把运行时动画帧当作技能图标。</summary>
    public SkillEntityAsset SetIcon(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            throw new ArgumentException("技能图标必须提供资源路径", nameof(iconPath));
        Icon = SpriteTextureLoader.getSprite(iconPath);
        if (Icon == null) throw new InvalidOperationException($"技能图标加载失败: {iconPath}");
        return this;
    }

    /// <summary>设置技能在世界中的声明式视觉配置。</summary>
    public SkillEntityAsset SetWorldVisual(SkillWorldVisualProfile profile)
    {
        WorldVisualProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    /// <summary>解析指定动画变体的 UI 图标；旧技能未声明图标时回退到运行时首帧。</summary>
    public Sprite ResolveIcon(int animationIndex)
    {
        if (Icon != null) return Icon;
        if (!IsAnimationIndexValid(animationIndex)) return null;
        Sprite[] frames = GetAnimation(animationIndex).Runtime.Frames;
        return frames.Length > 0 ? frames[0] : null;
    }

    /// <summary>声明技能只能由指定类型的人物载体执行。</summary>
    public SkillEntityAsset RequireCarrier(SkillCarrierRequirement requirement)
    {
        CarrierRequirement = requirement;
        return this;
    }

    /// <summary>设置每个施放步骤在词条和范围修正之前的基础资源需求。</summary>
    public SkillEntityAsset SetBaseCastDemand(float demand)
    {
        BaseCastDemand = Mathf.Max(0f, demand);
        return this;
    }

    /// <summary>设置该法术本体是否执行通用伤害结算。</summary>
    public SkillEntityAsset SetDealsBaseDamage(bool enabled)
    {
        DealsBaseDamage = enabled;
        return this;
    }

    /// <summary>设置该法术用于面积成本换算的原始半径；零表示不按面积追加成本。</summary>
    public SkillEntityAsset SetAreaCostBaseRadius(float radius)
    {
        AreaCostBaseRadius = Mathf.Max(0f, radius);
        return this;
    }

    /// <summary>声明运行时实体寿命如何从实际技能容器参数解析。</summary>
    public SkillEntityAsset SetRuntimeLifetime(Func<Entity, float> resolver)
    {
        runtimeLifetimeResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        return this;
    }

    /// <summary>解析当前技能容器对应的运行时实体寿命；未声明时保留命中配置寿命。</summary>
    internal float ResolveRuntimeLifetime(Entity skillContainer, float fallback)
    {
        return runtimeLifetimeResolver == null
            ? fallback
            : Mathf.Max(0.05f, runtimeLifetimeResolver(skillContainer));
    }

    /// <summary>向法术本体追加一个不依赖词条的结构化效果。</summary>
    public SkillEntityAsset AddEffect(SkillEffectDescriptor effect)
    {
        if (effect == null) throw new ArgumentNullException(nameof(effect));
        effects.Add(effect);
        return this;
    }

    /// <summary>
    /// 声明法术定义所必需的词条。新建容器会自动物化默认值，蓝图必须保留该词条，
    /// 但仍可通过编辑字段改进其参数。
    /// </summary>
    public SkillEntityAsset RequireModifier(SkillModifierAsset modifier)
    {
        if (modifier == null) throw new ArgumentNullException(nameof(modifier));
        if (requiredModifiers.Any(spec => spec.AssetId == modifier.id)) return this;
        requiredModifiers.Add(modifier.CreateDefaultSpec());
        return this;
    }

    public bool IsRequiredModifier(string modifierId)
    {
        return requiredModifiers.Any(spec => spec.AssetId == modifierId);
    }

    public bool IsRequiredModifier(Type componentType)
    {
        return requiredModifiers.Any(spec =>
            ModClass.I.SkillV3.ModifierLib.get(spec.AssetId)?.EditorComponentType == componentType);
    }

    /// <summary>
    /// 设置由该法术实体新建容器时采用的默认施法资源需求。
    /// </summary>
    public SkillEntityAsset RequireCastResources(SkillCastResourceRequirement requirement)
    {
        DefaultCastResourceRequirement = requirement.DeepClone();
        return this;
    }

    public SkillEntityAsset RequireCastResource(SkillCastResourceAsset resource)
    {
        return RequireCastResources(SkillCastResourceRequirement.Single(resource));
    }

    /// <summary>声明该实体具备创建可学习技能容器的语义；内部动画和执行体保持默认关闭。</summary>
    public SkillEntityAsset AllowLearning()
    {
        if (ImpactProfile == null) throw new InvalidOperationException($"{id} 缺少命中配置");
        if (UseProfile == null) throw new InvalidOperationException($"{id} 缺少施法用途配置");
        if (DefaultCastResourceRequirement == null)
            throw new InvalidOperationException($"{id} 缺少施法资源需求");
        if (AcceptedTrajectoryDomains == SkillTrajectoryDomain.None)
            throw new InvalidOperationException($"{id} 缺少可接受的轨迹运行形态");
        CanBeLearned = true;
        return this;
    }

    /// <summary>该法术实体允许采用的运行形态轨迹。</summary>
    public SkillTrajectoryDomain AcceptedTrajectoryDomains { get; private set; }

    /// <summary>
    /// 设置该法术实体抽取指定词条时的权重倍率。倍率只参与候选加权，不绕过稀有度、冲突和相似度规则。
    /// </summary>
    public SkillEntityAsset SetModifierWeightMultiplier(SkillModifierAsset modifier, float multiplier)
    {
        if (multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));

        _modifierWeightMultipliers[modifier.id] = multiplier;
        return this;
    }

    public float GetModifierWeightMultiplier(SkillModifierAsset modifier)
    {
        return _modifierWeightMultipliers.TryGetValue(modifier.id, out var multiplier) ? multiplier : 1f;
    }

    public SkillEntityAsset SetupColliderSphere(float radius, ColliderConfig config)
    {
        PrefabEntity.Add(new ColliderSphere()
        {
            Radius = radius
        }, config);
        return this;
    }

    public SkillEntityAsset SetupDefaultTraj(TrajectoryAsset traj)
    {
        var traj_component = new Trajectory()
        {
            ID = traj.id
        };
        PrefabEntity.AddComponent(traj_component);
        traj.OnInit?.Invoke(PrefabEntity);
        return this;
    }

    public SkillEntityAsset SetupVisualRotation(VisualRotation visualRotation)
    {
        PrefabEntity.AddComponent(visualRotation);
        return this;
    }

    public static Sprite[] LoadOrderedFrames(string effect_path)
    {
        var frames = SpriteTextureLoader.getSpriteList(effect_path);
        return frames?.OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray() ?? Array.Empty<Sprite>();
    }

    public SkillEntityAsset SetupCommonPrefab(string effect_path, float scale = 0.1f, bool anim_loop = true)
    {
        return SetupCommonPrefab(effect_path, scale, anim_loop, SkillEntityAnimationSettings.Inherit);
    }

    public SkillEntityAsset SetupCommonPrefab(string effect_path, float scale, bool anim_loop,
        SkillEntityAnimationSettings animationSettings)
    {
        if (animationSettings == null) throw new ArgumentNullException(nameof(animationSettings));

        return SetupCommonPrefab(
            SkillEntityAnimation.Create(effect_path, scale, animationSettings),
            anim_loop);
    }

    public SkillEntityAsset SetupCommonPrefab(SkillEntityAnimation animation, bool animLoop = true)
    {
        if (animation == null) throw new ArgumentNullException(nameof(animation));

        DefaultAnimationFrameInterval = 0.1f;
        DefaultAnimationLoop = animLoop;
        PrefabEntity = World.CreateEntity(
            new SkillEntity()
            {
                SkillContainer = default,
                Asset = this
            },
            new SkillContext(),
            new Position(),
            new Rotation(Vector3.right),
            new Scale(animation.Scale),
            new AnimBindRenderer(),
            new AnimController()
            {
                meta = new ()
                {
                    frame_interval = DefaultAnimationFrameInterval,
                    loop = DefaultAnimationLoop
                }
            },
            new AnimData()
            {
                frames = Array.Empty<Sprite>()
            },
            new AliveTimer()
            {
                value = 0f  
            },
            new AliveTimeLimit()
            {
                value  = 5f
            },
            Tags.Get<TagPrefab>());
        AddAnimation(animation);
        return this;
    }

    /// <summary>绑定通用命中配置，并将其碰撞参数写入实体预制体。</summary>
    public SkillEntityAsset SetupImpactProfile(SkillImpactProfileAsset profile, ColliderConfig config)
    {
        ImpactProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        OnObjCollision = SkillHitResolver.ResolveProfile;
        if (profile.CollisionRadius > 0f)
        {
            SetupColliderSphere(profile.CollisionRadius, config);
        }
        if (profile.LinearForward > 0f || profile.LinearBackward > 0f)
        {
            PrefabEntity.AddComponent(new ColliderLinearExtent
            {
                Forward = profile.LinearForward,
                Backward = profile.LinearBackward
            });
        }
        if (profile.IsBeam || profile.Kind == SkillImpactKind.Wall)
        {
            PrefabEntity.AddComponent(new AnimLinearLayout
            {
                Mode = profile.Kind == SkillImpactKind.Wall
                    ? AnimLinearLayoutMode.Tile
                    : AnimLinearLayoutMode.Stretch
            });
        }
        if (profile.HitOncePerTarget || profile.RepeatHitInterval > 0f)
        {
            PrefabEntity.AddComponent(SkillHitMemory.Create());
        }
        if (profile.CanResolveAtPosition)
        {
            PrefabEntity.AddComponent(new SkillPositionImpactState());
        }
        PrefabEntity.GetComponent<AliveTimeLimit>().value = profile.Lifetime;
        return this;
    }

    /// <summary>绑定法术对 AI 与玩家控制层公开的目标模式。</summary>
    public SkillEntityAsset SetupUseProfile(SkillUseProfileAsset profile)
    {
        UseProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    public SkillEntityAsset TuneImpact(float damageMultiplier = 1f, float effectRadiusMultiplier = 1f,
        float lifetimeMultiplier = 1f, float barrierLengthMultiplier = 1f, bool contactDamage = false,
        float contactForce = 0f)
    {
        ImpactTuning.DamageMultiplier = damageMultiplier;
        ImpactTuning.EffectRadiusMultiplier = effectRadiusMultiplier;
        ImpactTuning.LifetimeMultiplier = lifetimeMultiplier;
        ImpactTuning.BarrierLengthMultiplier = barrierLengthMultiplier;
        ImpactTuning.ContactDamage = contactDamage;
        ImpactTuning.ContactForce = contactForce;
        return this;
    }

    /// <summary>声明该法术实体允许采用的运行形态轨迹。</summary>
    public SkillEntityAsset AcceptTrajectoryDomains(SkillTrajectoryDomain domains)
    {
        AcceptedTrajectoryDomains = domains;
        return this;
    }

    public SkillEntityAsset AddAnimation(string effectPath, float scale = 0.1f)
    {
        return AddAnimation(effectPath, scale, SkillEntityAnimationSettings.Inherit);
    }

    public SkillEntityAsset AddAnimation(string effectPath, float scale, SkillEntityAnimationSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        return AddAnimation(SkillEntityAnimation.Create(effectPath, scale, settings));
    }

    public SkillEntityAsset AddAnimation(SkillEntityAnimation animation)
    {
        if (animation == null) throw new ArgumentNullException(nameof(animation));

        _animations.Add(animation);
        if (_animations.Count == 1)
        {
            PrefabEntity.GetComponent<AnimData>().frames = animation.Runtime.Frames;
            PrefabEntity.GetComponent<Scale>().value = Vector3.one * animation.Scale;
            ref var controller = ref PrefabEntity.GetComponent<AnimController>();
            animation.Runtime.Settings.Apply(ref controller.meta);
        }
        return this;
    }

    /// <summary>
    /// 追加动画变体,并按"目标循环时长"根据实际帧数自动计算 frame_interval,
    /// 使该动画约在 <paramref name="targetCycleSeconds"/> 内播完一轮。帧间隔夹在 [0.02s, 0.12s],
    /// 避免高帧动画过快闪烁或低帧动画过慢卡顿。适合给帧数明显偏多(如 48/60 帧)的变体加快节奏。
    /// </summary>
    public SkillEntityAsset AddAnimation(string effectPath, float scale, float targetCycleSeconds)
    {
        var frame_count = LoadOrderedFrames(effectPath).Length;
        var settings = targetCycleSeconds > 0f && frame_count > 0
            ? SkillEntityAnimationSettings.Inherit
                .WithFrameInterval(Mathf.Clamp(targetCycleSeconds / frame_count, 0.02f, 0.12f))
            : SkillEntityAnimationSettings.Inherit;
        return AddAnimation(effectPath, scale, settings);
    }

    public SkillEntityAnimation GetAnimation(int index)
    {
        return _animations[index];
    }

    public bool IsAnimationIndexValid(int index)
    {
        return index >= 0 && index < _animations.Count;
    }

    public int GetRandomAnimationIndex()
    {
        return _animations.Count == 1 ? 0 : Randy.randomInt(0, _animations.Count);
    }

    public Entity NewEntity()
    {
        Entity entity = World.CloneEntity(PrefabEntity);
        foreach (Entity child in PrefabEntity.ChildEntities) entity.AddChild(World.CloneEntity(child));

        var list = new EntityList(World);
        list.AddTree(entity);
        foreach (var e in list)
        {
            ModClass.I.CommandBuffer.RemoveTag<TagPrefab>(e.Id);
        }

        if (entity.HasComponent<AnimData>())
            entity.GetComponent<AnimData>().frame_timer = 0f;

        SetupRuntimeFx(entity);

        return entity;
    }

    public Entity NewEntity(int animationIndex)
    {
        var entity = NewEntity();
        var animation = GetAnimation(animationIndex);
        ref var animData = ref entity.GetComponent<AnimData>();
        animData.frames = animation.Runtime.Frames;
        animData.frame_idx = 0;
        animData.frame_timer = 0f;
        Vector3 intrinsicScale = Vector3.one * animation.Scale;
        entity.GetComponent<Scale>().value = intrinsicScale;
        ref var controller = ref entity.GetComponent<AnimController>();
        controller.meta.frame_interval = DefaultAnimationFrameInterval;
        controller.meta.loop = DefaultAnimationLoop;
        animation.Runtime.Settings.Apply(ref controller.meta);
        return entity;
    }

    /// <summary>
    /// 为运行时法术实体挂载地面影响、扫掠碰撞和命中反馈节流所需的组件。
    /// 出生位置会在 <see cref="Manager.SpawnSkill(Entity, BaseSimObject, BaseSimObject, Vector3, float, float, float?, float, Kingdom)"/>
    /// 写入后同步刷新。
    /// </summary>
    private void SetupRuntimeFx(Entity entity)
    {
        var pos = entity.HasComponent<Position>() ? entity.GetComponent<Position>().value : Vector3.zero;

        // 扫掠碰撞：记录上一帧位置
        entity.AddComponent(new PrevPosition { Value = new Vector2(pos.x, pos.y) });

        // 地面影响距离节流
        entity.AddComponent(new SkillGroundFxState
        {
            DistanceAccumulator = 0f,
            LastX = pos.x,
            LastY = pos.y
        });

        entity.AddComponent(new SkillImpactFeedbackState
        {
            NextAllowedTime = 0f
        });

        entity.AddComponent(new EffectRadiusScale
        {
            Value = 1f
        });

        if (entity.HasComponent<SkillHitMemory>())
        {
            ref var hitMemory = ref entity.GetComponent<SkillHitMemory>();
            hitMemory = SkillHitMemory.Create();
        }

        // 确保贴身残影能读到 tint（法术未显式设色时给白色默认）
        if (!entity.HasComponent<AnimTint>())
        {
            entity.AddComponent(new AnimTint(Color.white));
        }

    }

    public override string ToString()
    {
        return id;
    }

    public SkillEntityAsset AddSemantics(params SemanticAsset[] semantics)
    {
        Semantics = SemanticDescriptor.Weighted(
            Semantics.contributions
                .Concat(semantics.Select(x => new SemanticContribution(x)))
                .GroupBy(x => x.semantic_id, StringComparer.Ordinal)
                .Select(x => x.First())
                .ToArray());
        return this;
    }
}
