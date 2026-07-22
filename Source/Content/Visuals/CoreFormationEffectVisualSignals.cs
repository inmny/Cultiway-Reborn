using System.Collections.Generic;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>逻辑层提交给表现层的一次核心形成特效信号。</summary>
public readonly struct CoreFormationEffectVisualSignal
{
    /// <summary>效果持有者。</summary>
    public readonly Actor Owner;

    /// <summary>可选的受影响目标。</summary>
    public readonly Actor Target;

    /// <summary>没有有效目标时使用的动画坐标，其中 y 为地面位置、z 为显示高度。</summary>
    public readonly Vector3 Position;

    /// <summary>效果族 ID。</summary>
    public readonly string FamilyId;

    /// <summary>表现阶段。</summary>
    public readonly CoreFormationVisualChannel Channel;

    /// <summary>该阶段的帧动画配置。</summary>
    public readonly CoreFormationEffectVisualCue Cue;

    /// <summary>影响动画缩放和粒子数量的有界倍率。</summary>
    public readonly float Potency;

    /// <summary>创建一条只包含稳定运行时引用的表现信号。</summary>
    public CoreFormationEffectVisualSignal(
        Actor owner,
        Actor target,
        Vector3 position,
        string familyId,
        CoreFormationVisualChannel channel,
        CoreFormationEffectVisualCue cue,
        float potency)
    {
        Owner = owner;
        Target = target;
        Position = position;
        FamilyId = familyId;
        Channel = channel;
        Cue = cue;
        Potency = potency;
    }
}

/// <summary>隔离逻辑事件迭代和 Unity 表现对象创建的有界信号队列。</summary>
public static class CoreFormationEffectVisualSignals
{
    private const int MaxPendingSignals = 256;
    private const float MergeWindow = 0.1f;
    private static readonly List<CoreFormationEffectVisualSignal> Pending = new(MaxPendingSignals);
    private static readonly Dictionary<string, float> LastSignalTimes = new();

    /// <summary>为一次成功结算提交对应表现阶段；不可见角色不会进入队列。</summary>
    public static void Emit(
        in CoreFormationResolvedEffect effect,
        ActorExtend owner,
        Actor target,
        CoreFormationVisualChannel channel,
        Vector3? position = null)
    {
        if (owner?.Base == null || owner.Base.isRekt() || !owner.Base.is_visible) return;
        CoreFormationEffectVisualCue cue = effect.Definition.visual?.Get(channel);
        if (cue == null || string.IsNullOrEmpty(cue.path)) return;
        string key = $"{owner.Base.data.id}|{effect.Definition.family_id}|{channel}";
        float now = Time.time;
        if (LastSignalTimes.TryGetValue(key, out float previous) && now - previous < MergeWindow) return;
        LastSignalTimes[key] = now;
        if (Pending.Count >= MaxPendingSignals) Pending.RemoveAt(0);
        Vector3 resolvedPosition = position ?? (!target.isRekt()
            ? CoreFormationVisualCoordinates.FromActor(target)
            : CoreFormationVisualCoordinates.FromActor(owner.Base));
        Pending.Add(new CoreFormationEffectVisualSignal(
            owner.Base,
            target,
            resolvedPosition,
            effect.Definition.family_id,
            channel,
            cue,
            effect.Potency));
    }

    /// <summary>把当前全部待处理信号转移到表现系统持有的列表。</summary>
    internal static void Drain(List<CoreFormationEffectVisualSignal> output)
    {
        output.Clear();
        output.AddRange(Pending);
        Pending.Clear();
        if (LastSignalTimes.Count > 512) LastSignalTimes.Clear();
    }
}

/// <summary>在原版角色显示坐标与通用动画 ECS 坐标之间进行无损转换。</summary>
internal static class CoreFormationVisualCoordinates
{
    /// <summary>
    /// 把已经在 y 中包含高度的角色显示坐标转换为渲染器需要的地面 y 与独立 z 高度。
    /// </summary>
    public static Vector3 FromActor(Actor actor)
    {
        Vector3 position = actor.cur_transform_position;
        position.y -= position.z;
        return position;
    }

    /// <summary>把动画 ECS 坐标还原为 Unity 粒子等直接消费的显示坐标。</summary>
    public static Vector3 ToUnityPosition(Vector3 position)
    {
        position.y += position.z;
        return position;
    }

    /// <summary>把动画 ECS 方向投影到 Unity 实际显示平面。</summary>
    public static Vector3 ToUnityDirection(Vector3 direction)
    {
        direction.y += direction.z;
        direction.z = 0f;
        return direction;
    }
}
