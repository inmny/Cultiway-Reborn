using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>有实际施放时，范围运动跟随共同步骤；最后一段结束恢复继续现有原版入口。</summary>
    internal static bool DrivesRadialMotions => casts.Count > 0;

    /// <summary>独立保存本次范围查询，受力回调不能覆盖其他接触正在使用的候选列表。</summary>
    private readonly List<int> radialTargets = new();

    /// <summary>先确认本步共同整点，再从现成空间索引选目标，最后受理有限沿地运动。</summary>
    /// <param name="clock">已经确认的共同世界时间。</param>
    private void ConfirmRadialMotions(double clock)
    {
        long generation = worldGeneration;
        foreach (RunningCast running in active)
        {
            for (int node = 0; node < running.Cast.Plan.FormCount; node++)
            {
                CompiledConstructionRadialMotion field = running.Cast.Plan.Nodes[node].RadialMotion;
                if (field == null || !running.Cast.TryRadialMotion(node, out Vector3 centre)) continue;
                bool inChannel = running.Cast.TryWindChannel(node, out var channel);
                if (inChannel && !channel.Pressing) continue;
                int tick = Mathf.FloorToInt((running.Cast.Age + .000001f) / field.Interval);
                running.RadialTicks ??= new int[running.Cast.Plan.FormCount];
                if (tick <= running.RadialTicks[node]) continue;
                running.RadialTicks[node] = tick;
                CastAuthority owner = running.Owner(node);
                actorIndex.Query(inChannel ? channel.Bounds : new Bounds(centre, Vector3.one * (field.Radius * 2f)), radialTargets);
                foreach (int index in radialTargets)
                {
                    ActorMotion candidate = actors[index]; Actor actor = candidate.Actor;
                    if (actor.data?.id != candidate.Id || !actor.isAlive() || !actor.hasHealth() ||
                        !actor.asset.can_be_moved_by_powers || actor.isImmovable() || actor.position_height > 0f ||
                        !owner.Matches(field.Targets, actor)) continue;
                    Sample(candidate, clock, out Vector3 foot, out _);
                    Vector2 velocity;
                    if (inChannel)
                    {
                        if (!channel.Sample(foot, out float along, out float lateral)) continue;
                        velocity = channel.Velocity(field.Speed, along, lateral);
                    }
                    else
                    {
                        Vector3 delta = foot - centre;
                        float fade = 1f - delta.magnitude / field.Radius;
                        Vector2 direction = delta;
                        if (fade <= 0f || direction.sqrMagnitude < .000001f) continue;
                        velocity = direction.normalized * (field.Speed * fade);
                    }
                    float response = actor.GetExtend().ForceResponseScale(owner.IsLive ? owner.Actor : null,
                        owner.Damage.AttackerPowerLevel);
                    if (response <= 0f) continue;
                    // 每次明确本段速度，不把旧逐帧力度或上段速度再次叠加；竖直速度始终为零。
                    ConstructedActorImpulse.BeginRadial(actor, velocity * response, field.Interval, clock, terrain);
                    if (generation != worldGeneration) return;
                    InterruptContactBody(actor);
                    if (generation != worldGeneration) return;
                }
                if (field.GroundItemLimit == 0) continue;
                if (inChannel)
                {
                    running.ChannelItems ??= new List<long>[running.Cast.Plan.FormCount];
                    running.ChannelItems[node] ??= new List<long>(field.GroundItemLimit);
                    Combat.GroundWeaponService.ApplyChannel(channel, field.Speed, field.GroundItemLimit, running.ChannelItems[node]);
                }
                else Combat.GroundWeaponService.ApplyAirflow(centre, field.Radius, field.Speed, field.GroundItemLimit);
            }
        }
    }
}
