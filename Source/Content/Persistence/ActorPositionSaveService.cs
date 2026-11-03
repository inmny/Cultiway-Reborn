using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Persistence;

/// <summary>只保存模组实际移动过的人物；世界关系就绪后恢复连续坐标与已站稳的承托。</summary>
internal static class ActorPositionSaveService
{
    /// <summary>人物读取时仅登记，建筑与完整世界就绪后再应用。</summary>
    private static readonly Dictionary<long, ActorPositionSaveData> pending = new();

    /// <summary>正式人物分区独立维护版本，不改变旧人物基础或原版世界格式。</summary>
    internal static void Initialize()
    {
        var saves = ModClass.I.WorldSaves;
        saves.Actors.Register(new SaveSection<ActorExtend, ActorPositionSaveData>("position", "人物位置", 1,
            Capture, Restore, Validate));
        saves.WorldRestored += FinishLoad;
        saves.WorldCleared += pending.Clear;
    }

    /// <summary>仅在主线程确认本次移动后标记，原版并行下落不修改实体存储。</summary>
    /// <param name="actor">实际被模组移动的人物。</param>
    internal static void Remember(ActorExtend actor)
    {
        if (!actor.E.HasComponent<PreciseActorPosition>()) actor.E.AddComponent(new PreciseActorPosition());
    }

    /// <summary>连续坐标随人物分区保存；只有已站稳的模型表面需要恢复垂直位置。</summary>
    /// <param name="actor">已准备原版保存资料的人物。</param>
    private static ActorPositionSaveData Capture(ActorExtend actor)
    {
        if (!actor.E.HasComponent<PreciseActorPosition>()) return null;
        Actor body = actor.Base;
        var data = new ActorPositionSaveData { X = body.current_position.x, Y = body.current_position.y };
        if (ConstructedActorImpulse.TryGetSurface(body, out float z) && BodyLocomotion.SurfaceContact != null)
        {
            var point = new Vector3(data.X, data.Y, z);
            BodySurfaceContact contact = BodyLocomotion.SurfaceContact(point, SpatialActorPose.Height(body) * .16f,
                z + .0001f);
            if (contact.Owner != 0 && Mathf.Abs(contact.Height - z) < .03f)
            {
                data.Surface = contact.Owner;
                data.SurfaceHeight = contact.LocalHeight;
            }
        }

        return data;
    }

    /// <summary>外部存档先完整检查，不能将缺失坐标误读成合法的零坐标。</summary>
    /// <param name="data">正式位置记录。</param>
    private static void Validate(ActorPositionSaveData data)
    {
        if (float.IsNaN(data.X) || float.IsInfinity(data.X) || float.IsNaN(data.Y) || float.IsInfinity(data.Y) ||
            data.X < 0 || data.X >= MapBox.width || data.Y < 0 || data.Y >= MapBox.height)
            throw new InvalidDataException("人物位置超出当前地图或不是有限数值");
        if (data.SurfaceHeight.HasValue != (data.Surface > 0) || data.Surface < 0 ||
            data.SurfaceHeight.HasValue &&
            (float.IsNaN(data.SurfaceHeight.Value) || float.IsInfinity(data.SurfaceHeight.Value)))
            throw new InvalidDataException("人物承托身份与高度不完整");
    }

    /// <summary>原版仍在逐个载入人物时不改变位置，避免被后续整格定位覆盖。</summary>
    /// <param name="actor">已恢复身份的人物。</param><param name="data">已经验证的正式资料。</param>
    private static void Restore(ActorExtend actor, ActorPositionSaveData data)
    {
        Remember(actor);
        pending[actor.Base.id] = data;
    }

    /// <summary>先恢复全部连续坐标，再采集实际表面；失败只舍弃对应承托并进入原档保留报告。</summary>
    private static void FinishLoad()
    {
        // 原版人物和建筑均已就绪。承托单独处理，不让一个缺失模型丢弃人物及已学能力。
        Rect? area = null;
        foreach (var entry in pending)
        {
            Actor actor = World.world.units.get(entry.Key);
            if (actor == null) continue;
            ActorPositionSaveData data = entry.Value;
            actor.stopMovement();
            actor.current_position = new Vector2(data.X, data.Y);
            actor.setCurrentTile(World.world.GetTileSimple((int)data.X, (int)data.Y));
            actor.dirty_current_tile = true;
            if (data.SurfaceHeight.HasValue)
            {
                Rect bounds = area ?? new Rect(data.X, data.Y, 0, 0);
                bounds.xMin = Mathf.Min(bounds.xMin, data.X - 1);
                bounds.xMax = Mathf.Max(bounds.xMax, data.X + 1);
                bounds.yMin = Mathf.Min(bounds.yMin, data.Y - 1);
                bounds.yMax = Mathf.Max(bounds.yMax, data.Y + 1);
                area = bounds;
            }
        }

        AbilityHeightField terrain = null;
        if (area.HasValue && ConstructedAbilityService.GroundProvider != null && BodyLocomotion.SavedSurface != null)
        {
            terrain = ConstructedAbilityService.GroundProvider(area.Value);
            ConstructedAbilityService.CaptureBodyObstacles?.Invoke(area.Value, terrain);
            ConstructedActorImpulse.PublishTerrain(terrain);
        }

        foreach (var entry in pending)
        {
            Actor actor = World.world.units.get(entry.Key);
            ActorPositionSaveData data = entry.Value;
            if (actor == null) continue;
            if (data.SurfaceHeight.HasValue)
            {
                float height = SpatialActorPose.Height(actor);
                BodySurfaceContact support = terrain == null
                    ? new BodySurfaceContact(0, float.NegativeInfinity)
                    : BodyLocomotion.SavedSurface(new Vector2(data.X, data.Y), height * .16f,
                        data.Surface, data.SurfaceHeight.Value);
                if (support.Owner != data.Surface)
                    ModClass.I.WorldSaves.LastReport.Skip("人物 " + entry.Key, "人物位置/承托",
                        "原建筑或当前模型无法提供所存承托，已保留连续位置并采用原版地面位置");
                else ConstructedActorImpulse.RestoreSurface(actor, support.Height, terrain);
            }

            actor.updatePos();
        }

        pending.Clear();
    }
}