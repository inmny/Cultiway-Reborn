using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public enum SkillEntityType
{
    Attack
}
public delegate bool OnObjCollision(ref SkillContext context, Entity skill_container, Entity skill_entity, BaseSimObject target);
public class SkillEntityAsset : Asset
{
    private readonly Dictionary<string, float> _modifierWeightMultipliers = new(StringComparer.Ordinal);
    private readonly List<SkillEntityAnimation> _animations = new();

    public Entity PrefabEntity;
    public ElementComposition Element;
    public SemanticDescriptor Semantics { get; set; } = new();
    public IReadOnlyList<SkillEntityAnimation> Animations => _animations;
    public string EditorCategoryKey;
    public int EditorSortOrder;
    public bool EditorSelectable;
    /// <summary>该实体能否作为角色持有、改进和释放的技能容器模板。</summary>
    public bool CanBeLearned { get; private set; }
    public EntityStore World => ModClass.I.SkillV3.World;
    public OnObjCollision OnObjCollision;
    public SkillEntityType Type;
    public SkillCastResourceRequirement DefaultCastResourceRequirement { get; private set; }

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
        CanBeLearned = true;
        return this;
    }

    /// <summary>
    /// 该法术视觉上可接受的方向姿态集合（按位或）。
    /// 默认 <see cref="TrajectoryOrientation.Horizontal"/>，兼容现有绝大多数水平移动法术。
    /// 由 <see cref="SkillModifierLibrary.SetTrajectory"/> 词条在随机选取轨迹时，
    /// 与候选 <see cref="TrajectoryAsset.Orientations"/> 取交集过滤，避免方向不兼容的轨迹替换。
    /// </summary>
    public TrajectoryOrientation AcceptedOrientations { get; set; } = TrajectoryOrientation.Horizontal;

    /// <summary>
    /// 流式声明该法术可接受的方向姿态，便于在 <c>Configure(...)</c> 之后链式调用。
    /// </summary>
    public SkillEntityAsset AcceptOrientations(TrajectoryOrientation orientations)
    {
        AcceptedOrientations = orientations;
        return this;
    }

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
        PrefabEntity = World.CreateEntity(
            new SkillEntity()
            {
                SkillContainer = default,
                Asset = this
            },
            new SkillContext(),
            new Position(),
            new Rotation(Vector3.right),
            new Scale(scale),
            new AnimBindRenderer(),
            new AnimController()
            {
                meta = new ()
                {
                    frame_interval = 0.1f,
                    loop = anim_loop
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
        AddAnimation(effect_path, scale, animationSettings);
        return this;
    }

    public SkillEntityAsset AddAnimation(string effectPath, float scale = 0.1f)
    {
        return AddAnimation(effectPath, scale, SkillEntityAnimationSettings.Inherit);
    }

    public SkillEntityAsset AddAnimation(string effectPath, float scale, SkillEntityAnimationSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        var animation = new SkillEntityAnimation(effectPath, LoadOrderedFrames(effectPath), scale, settings);
        _animations.Add(animation);
        if (_animations.Count == 1)
        {
            PrefabEntity.GetComponent<AnimData>().frames = animation.Frames;
            PrefabEntity.GetComponent<Scale>().value = Vector3.one * animation.Scale;
            ref var controller = ref PrefabEntity.GetComponent<AnimController>();
            animation.Settings.Apply(ref controller.meta);
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
        animData.frames = animation.Frames;
        animData.frame_idx = 0;
        animData.frame_timer = 0f;
        entity.GetComponent<Scale>().value = Vector3.one * animation.Scale;
        ref var controller = ref entity.GetComponent<AnimController>();
        animation.Settings.Apply(ref controller.meta);
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
