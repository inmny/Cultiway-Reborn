using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;
using UnityEngine;
using Position = Cultiway.Core.SkillLib.Components.Position;
using Rotation = Cultiway.Core.SkillLib.Components.Rotation;

namespace Cultiway.Core.SkillLib;

public class SkillEntityAsset : Asset
{
    private Archetype _actual_archetype;

    private  Entity        _prefab;
    private  CommandBuffer _prefab_buffer;
    internal AnimSetting   anim_setting;

    private SkillEntityAsset(string id)
    {
        this.id = id;
    }

    public Entity NewEntity(ref SkillInfo skill_info)
    {
        var entity = _prefab.Store.CloneEntitySimply(_prefab);

        entity.AddComponent(skill_info);
        entity.RemoveTag<PrefabTag>();

        return entity;
    }

    public class AnimSetting
    {
        public Sprite[] frames;
        public float    interval;
        public bool     loop;
    }

    public class Builder
    {
        private SkillEntityAsset _on_build_asset;

        public Builder(string id)
        {
            _on_build_asset = new(id);
            _on_build_asset._prefab = ModClass.I.Skill.NewPrefab();
            _on_build_asset._prefab.AddComponent(new SkillEntityComponent()
            {
                asset = _on_build_asset
            });
            _on_build_asset._prefab.AddComponent(new AliveTimer());
        }

        /// <summary>
        /// 一般用于debug：利用重载修改实体本身的一些默认设定
        /// </summary>
        /// <param name="asset"></param>
        public Builder(SkillEntityAsset asset)
        {
            _on_build_asset = asset;
        }

        public Builder NewTrigger<T, TVal>(T trigger_component) where T : struct, ITriggerComponent<TVal>
        {
            _on_build_asset._prefab.AddComponent(trigger_component);
            return this;
        }

        public Builder AppendComponent<T>(T component) where T : struct, IComponent
        {
            _on_build_asset._prefab.AddComponent(component);
            return this;
        }

        public Builder SetTrajectory(TrajectoryAsset trajectory_asset)
        {
            _on_build_asset._prefab.AddComponent(new TrajectoryInfo()
            {
                asset = trajectory_asset
            });
            if (trajectory_asset.rotation_required && !_on_build_asset._prefab.HasComponent<Rotation>())
            {
                _on_build_asset._prefab.AddComponent(new Rotation(trajectory_asset.default_rotation));
            }

            if (trajectory_asset.velocity_required && !_on_build_asset._prefab.HasComponent<Velocity>())
            {
                _on_build_asset._prefab.AddComponent(new Velocity(trajectory_asset.default_velocity));
            }

            return this;
        }

        public Builder OverwriteTrajectoryDefaultData<TData, TValue>(TData data)
            where TData : struct, ITrajectoryData<TValue>
        {
            _on_build_asset._prefab.AddComponent(data);
            return this;
        }

        public Builder SetAnimation(Sprite[] frames, float interval = 0.1f, bool loop = true)
        {
            _on_build_asset.anim_setting = new AnimSetting()
            {
                interval = interval,
                frames = frames,
                loop = loop
            };
            if (!_on_build_asset._prefab.TryGetComponent(out Position pos))
            {
                _on_build_asset._prefab.AddComponent(new Position(0, 0, 0));
            }

            if (!_on_build_asset._prefab.TryGetComponent(out SkillAnimData anim_data))
            {
                _on_build_asset._prefab.AddComponent(new SkillAnimData()
                {
                    idx = 0, timer = 0
                });
            }

            return this;
        }

        public SkillEntityAsset Build()
        {
            return _on_build_asset;
        }
    }
}