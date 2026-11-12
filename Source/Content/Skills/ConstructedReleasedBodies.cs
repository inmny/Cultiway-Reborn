using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>离手身体与其他施放共同推进和确认，不由人物原版回调抢先移动。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>离手与仍在施放的构造共用最早世界时间。</summary>
    /// <param name="now">本帧世界时间。</param>
    private static double BodyFlightClock(double now)
    {
        foreach (var body in ConstructedActorImpulse.Bodies) now = Math.Min(now, body.Motion.SampledAt);
        return now;
    }

    /// <summary>离手身体即使原施放已结束，也继续拥有共同步骤。</summary>
    private static bool HasBodyFlights
    {
        get { foreach (var body in ConstructedActorImpulse.Bodies) return true; return false; }
    }

    /// <summary>尚未到本轮身体起点时只推进到起点，不提前处理它。</summary>
    /// <param name="clock">共同起点。</param><param name="step">当前候选时长。</param>
    private static float BodyFlightStep(double clock, float step)
    {
        foreach (var body in ConstructedActorImpulse.Bodies)
        {
            step = Mathf.Min(step, body.Motion.SampledAt > clock ? (float)(body.Motion.SampledAt - clock) : .02f);
            if (body.Motion.SlideUntil > clock) step = Mathf.Min(step, (float)(body.Motion.SlideUntil - clock));
        }
        return step;
    }

    /// <summary>只更新数字候选范围，不提前发布人物位置。</summary>
    /// <param name="time">本步预测末端。</param>
    private void PredictBodyFlights(double time)
    {
        foreach (var body in ConstructedActorImpulse.Bodies)
            if (time >= body.Motion.SampledAt && actorIndices.TryGetValue(body.Motion.ActorId, out int index))
                actorIndex.Update(index, body.Ground?.Bounds(time) ?? body.Flight.Bounds(body.Geometry, body.Motion.SampledAt - body.Motion.StartedAt,
                    time - body.Motion.StartedAt));
    }

    /// <summary>真实离手几何参与同一候选，前一接触可以让全部施放共同回退。</summary>
    /// <param name="clock">共同起点。</param><param name="step">预测时长。</param><param name="include">共同候选入口。</param>
    private void FindBodyFlightContacts(double clock, float step, Action<ContactChoice> include)
    {
        foreach (var body in ConstructedActorImpulse.Bodies)
        {
            if (body.Motion.SampledAt > clock || body.Ground != null) continue;
            double from = clock - body.Motion.StartedAt, to = from + step;
            Bounds area = body.Flight.Bounds(body.Geometry, from, to);
            if (!body.Flight.Find(body.Geometry, from, to, terrain, CaptureBodyEnvironment(area, terrain), out BodySurfaceHit hit)) continue;
            include(new ContactChoice(default, default, null, hit.Fraction, hit.Point, hit.Normal, step,
                carrying: hit, flight: body));
        }
    }

    /// <summary>在共同确认后才写入目标，预测和重算均不归还或移动它。</summary>
    /// <param name="time">本步确认时点。</param>
    private static void CommitBodyFlights(double time)
    {
        long generation = worldGeneration;
        foreach (var body in ConstructedActorImpulse.Bodies)
        {
            if (generation != worldGeneration) return;
            if (time >= body.Motion.SampledAt) ConstructedActorImpulse.CommitBody(body, time, simulation.terrain);
        }
    }
}
