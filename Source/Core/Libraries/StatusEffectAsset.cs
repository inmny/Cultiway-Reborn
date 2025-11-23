using System;
using Cultiway.Core.Components;
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

public class StatusEffectAsset : Asset
{
    public BaseStats stats = new();
    public StatusParticleSettings ParticleSettings { get; private set; } = StatusParticleSettings.Disabled;
    public StatusTickSettings TickSettings { get; private set; } = StatusTickSettings.Disabled;
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
    public Entity NewEntity()
    {
        Entity entity = _world.CloneEntity(_prefab);
        foreach (Entity child in _prefab.ChildEntities) entity.AddChild(_world.CloneEntity(child));

        var list = new EntityList(_world);
        list.AddTree(entity);
        var batch = new EntityBatch();
        batch.RemoveTag<TagPrefab>();
        list.ApplyBatch(batch);

        if (entity.HasComponent<AnimData>())
            entity.GetComponent<AnimData>().next_frame_time = (float)(World.world.map_stats.world_time + Time.deltaTime);

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
    
    public class Builder
    {
        private StatusEffectAsset _under_build;
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
        public StatusEffectAsset Build()
        {
            ModClass.L.StatusEffectLibrary.add(_under_build);

            if (_under_build.ParticleSettings.enabled)
            {
                _under_build._prefab.AddComponent(new StatusParticleState());
            }
            if (_under_build.TickSettings.enabled)
            {
                _under_build._prefab.AddComponent(new StatusTickState());
            }
            return _under_build;
        }
    }
}
