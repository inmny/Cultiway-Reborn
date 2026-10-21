using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Content.Skills;

/// <summary>完整计算结束后一起交给各个显示方式的身体、持物与形体。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>最后完成的一轮画面，推进过程不会修改其中的集合。</summary>
    private static PresentedFrame presentation = new(0d, Array.Empty<FormVisualInstance>(), new());
    /// <summary>换世界时变化，旧轮次不得继续发布或覆盖新世界画面。</summary>
    private static long worldGeneration;

    /// <summary>从同一轮完成状态收集画面，不在各个绘制回调分别采集。</summary>
    /// <param name="time">本轮已经完整推进的世界时间。</param>
    private void PublishPresentation(double time)
    {
        var bodies = new Dictionary<long, PresentedBody>();
        foreach (RunningCast running in casts)
            if (running.BodyControlled)
                bodies[running.SourceId] = new PresentedBody(running.Cast.BodyPose.Value,
                    running.Cast.Plan.Body.Occupancy, running.Body.Weapon != null);
        var completed = new PresentedFrame(time, visuals.ToArray(), bodies);
        // 两处发布都在主线程连续完成，中间不让出、不调用游戏或其他订阅者。
        FormVisualScene.Publish(ProviderId, completed.Forms);
        presentation = completed;
    }

    /// <summary>各显示方式只读已经完成的一轮，不访问工作线程正在修改的施放。</summary>
    /// <param name="actor">真实身体编号。</param>
    /// <param name="pose">已经完成的共同姿态。</param>
    public static bool TryGetBodyPose(long actor, out BodyPose pose) => TryGetBodyPose(actor, out pose, out _);

    /// <summary>一次读取同一轮的姿态与接管部位，避免遮蔽部位与显示手臂来自不同轮次。</summary>
    /// <param name="actor">真实身体编号。</param>
    /// <param name="pose">完成的姿态。</param>
    /// <param name="occupancy">本轮实际接管的身体部位。</param>
    public static bool TryGetBodyPose(long actor, out BodyPose pose, out BodyOccupancy occupancy)
    {
        if (presentation.Bodies.TryGetValue(actor, out PresentedBody body))
        { pose = body.Pose; occupancy = body.Occupancy; return true; }
        pose = default; occupancy = BodyOccupancy.None; return false;
    }

    /// <summary>原版与第一人称的静态持物随整份画面切换，受理但尚未发布时仍显示原持物。</summary>
    /// <param name="actor">当前真实人物。</param>
    public static bool IsWeaponBorrowed(Actor actor) => actor?.data != null &&
        presentation.Bodies.TryGetValue(actor.data.id, out PresentedBody body) && body.Weapon;

    /// <summary>实际借用控制在受理时取得，供玩法检查，不作为绘制状态使用。</summary>
    /// <param name="actor">实际人物。</param>
    public static bool HasWeaponControl(Actor actor)
    {
        if (actor?.data == null) return false;
        foreach (RunningCast running in casts)
            if (running.SourceId == actor.data.id && running.BodyControlled && running.Body?.Weapon != null) return true;
        return false;
    }

    /// <summary>仅保存一轮完成的数值和显示资料，不保存人物或借用器物对象。</summary>
    private sealed class PresentedFrame
    {
        /// <summary>这轮已经完成的世界时间。</summary>
        internal readonly double Time;
        /// <summary>与身体同一轮的全部生成及借用形体。</summary>
        internal readonly FormVisualInstance[] Forms;
        /// <summary>该轮身体姿态及接管部位，只在构造时填充。</summary>
        internal readonly Dictionary<long, PresentedBody> Bodies;
        /// <summary>接收已完成资料，后续不再修改集合。</summary>
        /// <param name="time">完成时间。</param>
        /// <param name="forms">共同形体。</param>
        /// <param name="bodies">共同身体。</param>
        internal PresentedFrame(double time, FormVisualInstance[] forms, Dictionary<long, PresentedBody> bodies)
        { Time = time; Forms = forms; Bodies = bodies; }
    }

    /// <summary>同一身体的姿态、接管部位及持物显示决定。</summary>
    private readonly struct PresentedBody
    {
        /// <summary>完整共同姿态。</summary>
        internal readonly BodyPose Pose;
        /// <summary>本轮接管部位。</summary>
        internal readonly BodyOccupancy Occupancy;
        /// <summary>本轮是否由共同形体显示武器。</summary>
        internal readonly bool Weapon;
        /// <summary>复制完成的显示决定。</summary>
        /// <param name="pose">共同姿态。</param>
        /// <param name="occupancy">接管部位。</param>
        /// <param name="weapon">是否接管持物显示。</param>
        internal PresentedBody(BodyPose pose, BodyOccupancy occupancy, bool weapon)
        { Pose = pose; Occupancy = occupancy; Weapon = weapon; }
    }
}
