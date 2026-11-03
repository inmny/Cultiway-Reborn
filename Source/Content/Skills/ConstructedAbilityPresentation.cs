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
            {
                bodies.TryGetValue(running.SourceId, out PresentedBody previous);
                bodies[running.SourceId] = new PresentedBody(previous, running.Cast.BodyPose.Value,
                    running.Cast.Plan.Body, running.Body.Weapon != null);
            }

        var completed = new PresentedFrame(time, visuals.ToArray(), bodies, terrain);
        // 两处发布都在主线程连续完成，中间不让出、不调用游戏或其他订阅者。
        FormVisualScene.Publish(ProviderId, completed.Forms);
        presentation = completed;
    }

    /// <summary>各显示方式只读已经完成的一轮，不访问工作线程正在修改的施放。</summary>
    /// <param name="actor">真实身体编号。</param>
    /// <param name="pose">已经完成的共同姿态。</param>
    public static bool TryGetBodyPose(long actor, out BodyPose pose) => TryGetBodyPose(actor, out pose, out _);

    /// <summary>同时取得共同身体及同轮地表换算的原版脚下，不混用新地形或人物位置。</summary>
    /// <param name="actor">真实身体编号。</param><param name="pose">完成的绝对高度姿态。</param>
    /// <param name="foot">原版使用的离地高度与地图位置。</param>
    internal static bool TryGetBodyRoot(long actor, out BodyPose pose, out UnityEngine.Vector3 foot)
    {
        PresentedFrame frame = presentation;
        if (!frame.Bodies.TryGetValue(actor, out PresentedBody body))
        {
            pose = default;
            foot = default;
            return false;
        }

        pose = body.Pose;
        foot = pose.Foot;
        foot.z -= frame.Terrain?.Sample(foot.x, foot.y) ?? 0f;
        return true;
    }

    /// <summary>一次读取同一轮的姿态与接管部位，避免遮蔽部位与显示手臂来自不同轮次。</summary>
    /// <param name="actor">真实身体编号。</param>
    /// <param name="pose">完成的姿态。</param>
    /// <param name="occupancy">本轮实际接管的身体部位。</param>
    public static bool TryGetBodyPose(long actor, out BodyPose pose, out BodyOccupancy occupancy)
    {
        if (presentation.Bodies.TryGetValue(actor, out PresentedBody body))
        {
            pose = body.Pose;
            occupancy = body.Occupancy;
            return true;
        }

        pose = default;
        occupancy = BodyOccupancy.None;
        return false;
    }

    /// <summary>读取指定肢体的同轮姿态；并行的另一只手不会覆盖本手的肩肘腕。</summary>
    /// <param name="actor">真实身体编号。</param><param name="part">右臂、左臂或全身支撑。</param>
    /// <param name="pose">该部位已经完成的姿态。</param>
    public static bool TryGetBodyPose(long actor, BodyOccupancy part, out BodyPose pose)
    {
        pose = default;
        if (!presentation.Bodies.TryGetValue(actor, out PresentedBody body) || (body.Occupancy & part) == 0)
            return false;
        switch (part)
        {
            case BodyOccupancy.RightArm:
                pose = body.Right;
                return true;
            case BodyOccupancy.LeftArm:
                pose = body.Left;
                return true;
            case BodyOccupancy.Stance:
                pose = body.Pose;
                return true;
            default: return false;
        }
    }

    /// <summary>原版与第一人称的静态持物随整份画面切换，受理但尚未发布时仍显示原持物。</summary>
    /// <param name="actor">当前真实人物。</param>
    public static bool IsWeaponBorrowed(Actor actor) => actor?.data != null &&
                                                        presentation.Bodies.TryGetValue(actor.data.id,
                                                            out PresentedBody body) && body.Weapon;

    /// <summary>实际借用控制在受理时取得，供玩法检查，不作为绘制状态使用。</summary>
    /// <param name="actor">实际人物。</param>
    public static bool HasWeaponControl(Actor actor)
    {
        if (actor?.data == null) return false;
        foreach (RunningCast running in casts)
            if (running.SourceId == actor.data.id && running.BodyControlled && running.Body?.Weapon != null)
                return true;
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

        /// <summary>与本轮身体同刻的不可变地表，用于原版投影。</summary>
        internal readonly AbilityHeightField Terrain;

        /// <summary>接收已完成资料，后续不再修改集合。</summary>
        /// <param name="time">完成时间。</param>
        /// <param name="forms">共同形体。</param>
        /// <param name="bodies">共同身体。</param>
        /// <param name="terrain">同轮绝对高度地表，空值为原版平地。</param>
        internal PresentedFrame(double time, FormVisualInstance[] forms, Dictionary<long, PresentedBody> bodies,
            AbilityHeightField terrain = null)
        {
            Time = time;
            Forms = forms;
            Bodies = bodies;
            Terrain = terrain;
        }
    }

    /// <summary>同一身体的姿态、接管部位及持物显示决定。</summary>
    private readonly struct PresentedBody
    {
        /// <summary>全身动作的姿态；只有局部动作时采用首个动作供既有观察入口读取。</summary>
        internal readonly BodyPose Pose;

        /// <summary>右侧实际显示姿态。</summary>
        internal readonly BodyPose Right;

        /// <summary>左侧实际显示姿态。</summary>
        internal readonly BodyPose Left;

        /// <summary>本轮实际替换显示的肢体及支撑；预留但没有动作的肢体仍显示原身体。</summary>
        internal readonly BodyOccupancy Occupancy;

        /// <summary>本轮是否由共同形体显示武器。</summary>
        internal readonly bool Weapon;

        /// <summary>合并同一人物的不冲突动作，发布后不再修改。</summary>
        /// <param name="previous">本轮已有的显示决定。</param>
        /// <param name="pose">共同姿态。</param>
        /// <param name="action">本次动作及实际末端。</param>
        /// <param name="weapon">是否接管持物显示。</param>
        internal PresentedBody(PresentedBody previous, BodyPose pose, CompiledBodyAction action, bool weapon)
        {
            bool left = action.Part.Left;
            Pose = previous.Occupancy == BodyOccupancy.None || action.ControlsStance ? pose : previous.Pose;
            Right = !action.Part.IsArm || left ? previous.Right : pose;
            Left = action.Part.IsArm && left ? pose : previous.Left;
            Occupancy = previous.Occupancy | (action.Part.IsArm
                            ? left ? BodyOccupancy.LeftArm : BodyOccupancy.RightArm
                            : BodyOccupancy.None) |
                        (action.ControlsStance ? BodyOccupancy.Stance : BodyOccupancy.None);
            Weapon = previous.Weapon || weapon;
        }
    }
}