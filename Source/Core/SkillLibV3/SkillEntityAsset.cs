using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
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
    public Entity PrefabEntity;
    public string VisualEffectPath;
    public ElementComposition Element;
    public HashSet<string> SeriesTags { get; } = new();
    public EntityStore World => ModClass.I.SkillV3.World;
    public OnObjCollision OnObjCollision;
    public SkillEntityType Type;

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
        VisualEffectPath = effect_path;
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
                frames = LoadOrderedFrames(effect_path)
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
        return this;
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

        return entity;
    }

    public override string ToString()
    {
        return id;
    }

    public SkillEntityAsset AddSeriesTags(params string[] tags)
    {
        if (tags == null) return this;
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag)) continue;
            SeriesTags.Add(tag);
        }

        return this;
    }
}
