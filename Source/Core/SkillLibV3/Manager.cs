using System;
using System.Text;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    
    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }
    public SkillEntityLibrary SkillLib { get; } = new SkillEntityLibrary();
    public SkillModifierLibrary ModifierLib { get; } = new();
    public TrajectoryLibrary TrajLib { get; } = new TrajectoryLibrary();
    public SystemGroup SkillLogicSystemGroup { get; } = new SystemGroup("SkillLibV3");
    internal Manager(WorldboxGame game)
    {
        Game = game;
        World = ModClass.I.W;
        
        //ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleSkillContainerSystem());
        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleNonMasteredSkillContainerSystem());
        ModClass.I.GeneralLogicSystems.Add(SkillLogicSystemGroup);
        
        SkillLogicSystemGroup.Add(new LogicTrajectorySystem());
        SkillLogicSystemGroup.Add(new LogicSkillTravelSystem());
        SkillLogicSystemGroup.Add(new LogicActorCollisionSystem());
    }

    internal void Init()
    {
        AssetManager._instance.add(SkillLib, "cultiway.skills");
        AssetManager._instance.add(ModifierLib, "cultiway.skill_modifiers");
        AssetManager._instance.add(TrajLib, "cultiway.trajectories");
    }

    public void SpawnAnim(string path, Vector3 pos, Vector3 rot, float scale = 0.1f)
    {
        var entity = SkillEntityLibrary.RawAnim.NewEntity();
        var frames = SpriteTextureLoader.getSpriteList(path);
        var data = entity.Data;
        data.Get<Position>().value = pos;
        data.Get<Rotation>().value = rot;
        data.Get<Scale>().value = scale * Vector3.one;
        data.Get<AnimData>().frames = frames;
        data.Get<AliveTimeLimit>().value = frames.Length * data.Get<AnimController>().meta.frame_interval;
    }
    public void SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength)
    {
        ref var container = ref skill_container.GetComponent<SkillContainer>();
        var salvo_count = skill_container.TryGetComponent(out SalvoCount salvo_modifier) ? salvo_modifier.Value : 1;
        var burst_count = skill_container.TryGetComponent(out BurstCount burst_modifier) ? burst_modifier.Value : 1;

        var source_pos = source.GetSimPos();
        var target_pos = target.GetSimPos();
        var base_dir = (target_pos - source_pos).normalized;
        
        // 计算齐射散射角度
        var scatter_angles = CalculateSalvoScatterAngles(burst_count);
        
        for (int i = 0; i < salvo_count; i++)
        {
            var delay = i * 0.5f;
            
            for (int j = 0; j < burst_count; j++)
            {
                // 计算当前齐射的散射方向
                var scatter_angle = scatter_angles[j];
                var scatter_dir = RotateDirection(base_dir, scatter_angle);

                var entity = container.Asset.NewEntity();
                entity.AddRelation(new SkillMasterRelation()
                {
                    SkillContainer = skill_container
                });
                var data = entity.Data;
                ref var context = ref data.Get<SkillContext>();
                context.Strength = strength;
                context.SourceObj = source;
                context.TargetObj = target;
                ref var skill_entity = ref data.Get<SkillEntity>();
                skill_entity.SkillContainer = skill_container;
                context.TargetPos = target_pos;
                context.TargetDir = scatter_dir;
                ref var pos = ref data.Get<Position>();
                pos.value = source_pos;
                // 设置初始方向到Rotation组件，用于平滑转向
                ref var rot = ref data.Get<Rotation>();
                rot.value = scatter_dir;

                if (delay > 0)
                {
                    entity.Add(new DelayActive()
                    {
                        LeftTime = delay
                    }, Tags.Get<TagInactive>());
                }
        
                container.OnSetup?.Invoke(entity);
            }
        }
    }

    /// <summary>
    /// 计算齐射散射角度：第一个朝向目标（0度），后面的左右轮流放置
    /// </summary>
    private float[] CalculateSalvoScatterAngles(int salvo_count)
    {
        if (salvo_count <= 1)
        {
            return new[] { 0f };
        }

        const float base_interval = 30f; // 基础间隔30度
        var angles = new float[salvo_count];
        angles[0] = 0f; // 第一个朝向目标

        // 计算是否需要自适应间隔
        var total_angle_needed = (salvo_count - 1) * base_interval;
        var adaptive_interval = total_angle_needed > 360f ? 360f / (salvo_count - 1) : base_interval;

        // 左右轮流放置：+interval, -interval, +2*interval, -2*interval, ...
        for (int i = 1; i < salvo_count; i++)
        {
            var side = (i % 2 == 1) ? 1f : -1f; // 奇数向右，偶数向左
            var multiplier = (i + 1) / 2; // 第1个是1倍，第2个是1倍，第3个是2倍，第4个是2倍...
            angles[i] = side * multiplier * adaptive_interval;
        }

        return angles;
    }

    /// <summary>
    /// 绕垂直轴旋转方向向量（在水平面上旋转）
    /// </summary>
    private Vector3 RotateDirection(Vector3 direction, float angle_degrees)
    {
        if (Mathf.Abs(angle_degrees) < 0.01f)
        {
            return direction;
        }

        // 计算垂直轴（向上方向）
        var up = Vector3.forward;
        var axis = Vector3.Cross(direction, up);
        
        axis.Normalize();
        var rotation = Quaternion.AngleAxis(angle_degrees, up);
        return rotation * direction;
    }
}