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

/// <summary>持续状态的空间标记，直接跟随真实状态持有者。</summary>
public enum StatusSpatialForm
{
    /// <summary>低亮护持标记，不声称具有拦截能力。</summary>
    Guard,
    /// <summary>环绕的实际储备提示。</summary>
    Reserve,
    /// <summary>禁锢状态的围束。</summary>
    Bind,
    /// <summary>冻结状态的冰棱。</summary>
    Freeze,
}

/// <summary>状态的空间形式、颜色和相对体型。</summary>
public sealed class StatusSpatialSettings
{
    /// <summary>显示颜色；透明度保持较低，避免持续遮住身体。</summary>
    public Color Color;
    /// <summary>相对身体高度的显示半径。</summary>
    public float RadiusRatio = .6f;
    /// <summary>是否表现为环绕的储备光点。</summary>
    public StatusSpatialForm Form;
}

public class StatusEffectAsset : Asset
{
    public const string DefaultIconPath = "cultiway/icons/iconWakan";

    public BaseStats stats = new();
    public string IconPath;
    public StatusParticleSettings ParticleSettings { get; private set; } = StatusParticleSettings.Disabled;
    public StatusTickSettings TickSettings { get; private set; } = StatusTickSettings.Disabled;
    public StatusSpatialSettings SpatialSettings { get; private set; }
    /// <summary>该已登记状态是否提供同类强度与可覆盖属性，供构造资料边界核对。</summary>
    internal bool SupportsStatOverwrite => !_prefab.IsNull && _prefab.HasComponent<StatusPotency>() && _prefab.HasComponent<StatusOverwriteStats>();

    /// <summary>周期值声明只用于已有周期动作及运行组件的共享状态。</summary>
    internal bool SupportsTickValue => TickSettings.enabled && TickSettings.Action != null && !_prefab.IsNull && _prefab.HasComponent<StatusTickState>();

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

    internal void DeletePrefab()
    {
        if (_prefab.IsNull) return;

        var entities = new EntityList(_world);
        entities.AddTree(_prefab);
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            var entity = entities[i];
            if (!entity.IsNull) entity.DeleteEntity();
        }
        _prefab = default;
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
        /// <summary>给当前状态建立跟随持有者的空间标记。</summary>
        /// <param name="settings">实际颜色、范围与形式。</param>
        public Builder SetSpatialVisual(StatusSpatialSettings settings)
        {
            _under_build.SpatialSettings = settings;
            return this;
        }
        /// <summary>向状态预制体加入由具体内容系统解释的运行时组件。</summary>
        public Builder AddComponent<T>(T component) where T : struct, IComponent
        {
            _under_build._prefab.AddComponent(component);
            return this;
        }
        public StatusEffectAsset Build()
        {
            return Build(false);
        }

        public StatusEffectAsset BuildDynamic()
        {
            return Build(true);
        }

        private StatusEffectAsset Build(bool dynamic)
        {
            if (dynamic)
            {
                ModClass.L.StatusEffectLibrary.AddDynamic(_under_build);
            }
            else
            {
                ModClass.L.StatusEffectLibrary.add(_under_build);
            }
            _under_build.GetExtend<StatusAssetExtend>().negative = _negative;

            if (_under_build.ParticleSettings.enabled)
            {
                _under_build._prefab.AddComponent(new StatusParticleState());
            }
            if (_under_build.TickSettings.enabled)
            {
                _under_build._prefab.AddComponent(new StatusTickState());
            }
            if (_under_build.SpatialSettings != null)
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
