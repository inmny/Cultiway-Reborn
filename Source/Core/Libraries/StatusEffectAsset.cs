using System;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class StatusParticleSettings
{
    public bool enabled;
    public Color color;
    public int count;
    public float interval;

    public static StatusParticleSettings Disabled => new()
    {
        enabled = false,
        color = Color.white,
        count = 1,
        interval = 0.5f
    };
}

public delegate void StatusTickAction(Entity statusEntity, float deltaTime);

public class StatusTickSettings
{
    public bool enabled;
    public float interval;
    public StatusTickAction Action;

    public static StatusTickSettings Disabled => new()
    {
        enabled = false,
        interval = 1f,
        Action = null
    };
}

/// <summary>共享状态在持有者身上播放的 Skill 帧动画配置。</summary>
public sealed class StatusAnimationSettings
{
    /// <summary>包含出现、运行和消散阶段的通用 Skill 动画。</summary>
    public SkillEntityAnimation Animation;

    /// <summary>未被动画片段覆盖时使用的基础帧间隔。</summary>
    public float FrameInterval = 0.1f;

    /// <summary>是否始终保持贴图竖直。</summary>
    public bool FixedUpright = true;

    /// <summary>关闭状态动画时使用的空配置。</summary>
    public static StatusAnimationSettings Disabled => new();
}

public class StatusEffectAsset : Asset
{
    public const string DefaultIconPath = "cultiway/icons/iconWakan";

    public BaseStats stats = new();
    public string IconPath;
    public StatusParticleSettings ParticleSettings { get; private set; } = StatusParticleSettings.Disabled;
    public StatusTickSettings TickSettings { get; private set; } = StatusTickSettings.Disabled;
    public StatusAnimationSettings AnimationSettings { get; private set; } = StatusAnimationSettings.Disabled;
    private Entity _prefab;
    private EntityStore _world;
    public StatusEffectAsset()
    {
        
    }

    private string f_desc_key;
    private string f_name_key;
    private string name_key => f_name_key ??= $"Cultiway.{id}";
    private string desc_key => f_desc_key ??= $"Cultiway.{id}.Description";
    private string given_name;
    private string given_desc;

    public override string ToString()
    {
        return id;
    }

    public string GetName()
    {
        return string.IsNullOrEmpty(given_name) ?LM.Get(name_key) : given_name;
    }

    public string GetDescription()
    {
        return string.IsNullOrEmpty(given_desc) ? LM.Get(desc_key) : given_desc;
    }

    public Sprite GetSpriteIcon()
    {
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(IconPath))
        {
            sprite = SpriteTextureLoader.getSprite(IconPath);
        }

        sprite ??= SpriteTextureLoader.getSprite($"cultiway/icons/status_effects/{id}") 
                    ?? SpriteTextureLoader.getSprite(DefaultIconPath);
        return sprite;
    }

    public Entity NewEntity()
    {
        Entity entity = _world.CloneEntity(_prefab);
        foreach (Entity child in _prefab.ChildEntities) entity.AddChild(_world.CloneEntity(child));

        var list = new EntityList(_world);
        list.AddTree(entity);
        foreach (var e in list) 
        {
            ModClass.I.CommandBuffer.RemoveTag<TagPrefab>(e.Id);
        }

        if (entity.HasComponent<AnimData>())
            entity.GetComponent<AnimData>().frame_timer = 0f;

        return entity;
    }

    public static Builder StartBuild(string id)
    {
        return new Builder(id);
    }

    private static StatusParticleSettings NormalizeParticleSettings(StatusParticleSettings settings)
    {
        settings.count = Mathf.Max(1, settings.count);
        settings.interval = Mathf.Max(0.05f, settings.interval);
        return settings;
    }

    private static StatusTickSettings NormalizeTickSettings(StatusTickSettings settings)
    {
        settings.interval = Mathf.Max(0.05f, settings.interval);
        settings.enabled = settings.enabled && settings.Action != null;
        return settings;
    }

    private static StatusAnimationSettings NormalizeAnimationSettings(StatusAnimationSettings settings)
    {
        settings.FrameInterval = Mathf.Max(0.01f, settings.FrameInterval);
        return settings;
    }
    
    public class Builder
    {
        private StatusEffectAsset _under_build;
        private bool _negative;
        public Builder(string id)
        {
            _under_build = new StatusEffectAsset()
            {
                id = id
            };
            _under_build._world = ModClass.I.W;
            _under_build._prefab = _under_build._world.CreateEntity(new StatusComponent()
            {
                id = id
            }, new AliveTimer(), Tags.Get<TagPrefab>());
        }
        public Builder SetStats(BaseStats stats)
        {
            _under_build.stats = stats;
            return this;
        }
        /// <summary>将状态标记为可被抗性、净化和驱散规则识别的负面状态。</summary>
        public Builder SetNegative(bool negative = true)
        {
            _negative = negative;
            return this;
        }
        public Builder SetDuration(float duration)
        {
            _under_build._prefab.AddComponent(new AliveTimeLimit()
            {
                value = duration
            });
            return this;
        }
        public Builder SetName(string name)
        {
            _under_build.given_name = name;
            return this;
        }
        public Builder SetDescription(string desc)
        {
            _under_build.given_desc = desc;
            return this;
        }
        public Builder SetIconPath(string iconPath)
        {
            _under_build.IconPath = iconPath;
            return this;
        }
        public Builder SetParticle(StatusParticleSettings settings)
        {
            _under_build.ParticleSettings = NormalizeParticleSettings(settings);
            return this;
        }
        public Builder EnableParticle(Color color, int count = 1, float interval = 0.1f)
        {
            return SetParticle(new StatusParticleSettings
            {
                enabled = true,
                color = color,
                count = count,
                interval = interval
            });
        }
        public Builder SetTick(StatusTickSettings settings)
        {
            _under_build.TickSettings = NormalizeTickSettings(settings);
            return this;
        }
        public Builder EnableTick(float interval, StatusTickAction action)
        {
            return SetTick(new StatusTickSettings
            {
                enabled = true,
                interval = interval,
                Action = action
            });
        }
        /// <summary>让状态直接持有一套通用 Skill 帧动画生命周期。</summary>
        public Builder SetAnimation(StatusAnimationSettings settings)
        {
            _under_build.AnimationSettings =
                NormalizeAnimationSettings(settings ?? throw new ArgumentNullException(nameof(settings)));
            return this;
        }
        /// <summary>使用常用参数启用状态帧动画。</summary>
        public Builder EnableAnimation(
            SkillEntityAnimation animation,
            float frameInterval = 0.1f,
            bool fixedUpright = true)
        {
            return SetAnimation(new StatusAnimationSettings
            {
                Animation = animation ?? throw new ArgumentNullException(nameof(animation)),
                FrameInterval = frameInterval,
                FixedUpright = fixedUpright
            });
        }
        /// <summary>向状态预制体加入由具体内容系统解释的运行时组件。</summary>
        public Builder AddComponent<T>(T component) where T : struct, IComponent
        {
            _under_build._prefab.AddComponent(component);
            return this;
        }
        public StatusEffectAsset Build()
        {
            ModClass.L.StatusEffectLibrary.add(_under_build);
            _under_build.GetExtend<StatusAssetExtend>().negative = _negative;

            if (_under_build.ParticleSettings.enabled)
            {
                _under_build._prefab.AddComponent(new StatusParticleState());
            }
            if (_under_build.TickSettings.enabled)
            {
                _under_build._prefab.AddComponent(new StatusTickState());
            }
            if (_under_build.AnimationSettings.Animation != null)
            {
                _under_build._prefab.AddComponent(new StatusAnimationState
                {
                    ScaleMultiplier = 1f
                });
            }
            return _under_build;
        }
    }
}
