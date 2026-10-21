using Cultiway.Content.Combat;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>接触回调仍处于共同逻辑时点，新启动能力沿用这一时点。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>是否正在确认一次真实接触，回调结束后立即解除。</summary>
    private bool confirmingContact;
    /// <summary>正在确认的真实世界时点。</summary>
    private double contactTime;
    /// <summary>回调内使用共同时间，普通游戏入口使用当前世界时间。</summary>
    private static double LogicTime => simulation?.confirmingContact == true ? simulation.contactTime : World.world.getCurWorldTime();

    /// <summary>施放距离也采用同一时点，不能用帧末位置拒绝仍在触达范围内的接触反应。</summary>
    /// <param name="source">实际来源。</param>
    /// <param name="target">实际目标。</param>
    private static float StartingDistance(Actor source, Actor target)
    {
        StartingPose(source, out Vector3 from, out _); StartingPose(target, out Vector3 to, out _);
        return Vector2.Distance(from, to);
    }

    /// <summary>取得出手时的身体数值；本轮采集后没有被实际改变的身体使用共同时间位置。</summary>
    /// <param name="actor">实际来源或目标。</param>
    /// <param name="foot">该时点的脚下。</param>
    /// <param name="height">该时点的身体高度。</param>
    private static void StartingPose(Actor actor, out Vector3 foot, out float height)
    {
        foot = SpatialActorPose.Foot(actor); height = SpatialActorPose.Height(actor);
        if (simulation?.confirmingContact != true || !simulation.actorIndices.TryGetValue(actor.id, out int index)) return;
        ActorMotion sampled = simulation.actors[index];
        // 位置瞬变或新生采用新脚下；只有体型改变时仍保留原有运动，并采用新高度。
        if (sampled.Actor != actor || sampled.To != foot) return;
        float actualHeight = height;
        simulation.Sample(sampled, simulation.contactTime, out foot, out height);
        if (sampled.Height != actualHeight) height = actualHeight;
    }
}
