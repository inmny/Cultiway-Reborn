using System.Collections.Concurrent;
using System.Threading;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>结构化技能结算向渲染阶段发送的世界视觉事件类型。</summary>
internal enum SkillWorldVisualEventKind : byte
{
    /// <summary>在技能落点创建并维持法阵。</summary>
    BeginField,

    /// <summary>一次范围结算完成，可播放整体冲击动画。</summary>
    AreaResolved,

    /// <summary>一个对象或地块确实发生变化，可播放局部反馈。</summary>
    EffectResolved,
}

/// <summary>跨逻辑与渲染阶段传递的不可变视觉事件。</summary>
internal readonly struct SkillWorldVisualEvent
{
    public readonly SkillWorldVisualEventKind Kind;
    public readonly long SkillEntityKey;
    public readonly SkillWorldVisualProfile Profile;
    public readonly string EffectId;
    public readonly Vector3 Center;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Duration;
    public readonly float Delay;
    public readonly SkillEffectResult Result;

    public SkillWorldVisualEvent(
        SkillWorldVisualEventKind kind,
        long skillEntityKey,
        SkillWorldVisualProfile profile,
        string effectId,
        Vector3 center,
        Vector3 position,
        float radius,
        float duration,
        float delay,
        in SkillEffectResult result)
    {
        Kind = kind;
        SkillEntityKey = skillEntityKey;
        Profile = profile;
        EffectId = effectId;
        Center = center;
        Position = position;
        Radius = radius;
        Duration = duration;
        Delay = delay;
        Result = result;
    }
}

/// <summary>
/// 把逻辑阶段已经确认成功的技能结果提交给渲染阶段。队列只保存不可变值，
/// 不持有对象或 ECS 实体引用，因此切换世界时可以直接整体清空。
/// </summary>
public static class SkillWorldVisualService
{
    private const int MaxPendingEvents = 2048;
    private static readonly ConcurrentQueue<SkillWorldVisualEvent> Events = new();
    private static int pendingCount;

    /// <summary>在范围技能抵达落点时创建其持续法阵；无持续法阵配置时不产生事件。</summary>
    public static void BeginArea(Entity skillEntity, Vector3 position, float radius)
    {
        if (!TryResolveProfile(skillEntity, out SkillWorldVisualProfile profile) || profile.Field == null) return;
        position = ResolveGroundPosition(position);
        float duration = skillEntity.TryGetComponent(out AliveTimeLimit limit) ? limit.value : 0.5f;
        if (skillEntity.TryGetComponent(out AliveTimer timer)) duration -= timer.value;
        Enqueue(new SkillWorldVisualEvent(
            SkillWorldVisualEventKind.BeginField,
            skillEntity.Pid,
            profile,
            null,
            position,
            position,
            Mathf.Max(0.05f, radius),
            Mathf.Max(0.05f, duration),
            0f,
            default));
    }

    /// <summary>在范围结算确实改变游戏状态后提交一次整体冲击动画。</summary>
    public static void ReportAreaResolution(
        Entity skillEntity,
        Vector3 position,
        float radius,
        in SkillEffectResult result)
    {
        if (!result.Changed || !TryResolveProfile(skillEntity, out SkillWorldVisualProfile profile) ||
            profile.AreaImpact == SkillAreaImpactVisualKind.None) return;
        position = ResolveGroundPosition(position);
        Enqueue(new SkillWorldVisualEvent(
            SkillWorldVisualEventKind.AreaResolved,
            skillEntity.Pid,
            profile,
            null,
            position,
            position,
            Mathf.Max(0.05f, radius),
            0f,
            0f,
            in result));
    }

    /// <summary>提交单个对象或地块的实际变化，并为扩散净化波计算传播延迟。</summary>
    public static void ReportEffectResolution(
        Entity skillEntity,
        string effectId,
        Vector3 center,
        float radius,
        Vector3 position,
        in SkillEffectResult result)
    {
        if (!result.Changed || !TryResolveProfile(skillEntity, out SkillWorldVisualProfile profile) ||
            profile.LocalEffect == SkillLocalEffectVisualKind.None) return;
        center = ResolveGroundPosition(center);
        float delay = profile.AreaImpact == SkillAreaImpactVisualKind.PurificationWave && radius > 0.001f
            ? 0.32f * Mathf.Clamp01(Vector2.Distance(center, position) / radius)
            : 0f;
        Enqueue(new SkillWorldVisualEvent(
            SkillWorldVisualEventKind.EffectResolved,
            skillEntity.Pid,
            profile,
            effectId,
            center,
            position,
            Mathf.Max(0f, radius),
            0f,
            delay,
            in result));
    }

    /// <summary>切换或清空世界时丢弃全部旧世界视觉事件与活动视图。</summary>
    public static void ClearWorldState()
    {
        while (Events.TryDequeue(out _))
        {
        }
        Interlocked.Exchange(ref pendingCount, 0);
        SkillWorldVisualRuntime.ClearWorldState();
    }

    /// <summary>由渲染系统消费一个事件。</summary>
    internal static bool TryDequeue(out SkillWorldVisualEvent visualEvent)
    {
        if (!Events.TryDequeue(out visualEvent)) return false;
        Interlocked.Decrement(ref pendingCount);
        return true;
    }

    /// <summary>从运行时技能实体解析已经声明的世界视觉配置。</summary>
    private static bool TryResolveProfile(Entity skillEntity, out SkillWorldVisualProfile profile)
    {
        profile = null;
        if (skillEntity.IsNull || !skillEntity.HasComponent<SkillEntity>()) return false;
        profile = skillEntity.GetComponent<SkillEntity>().Asset?.WorldVisualProfile;
        return profile != null;
    }

    /// <summary>把范围中心固定到地块平面，避免施法者飞行高度被重复带入法阵位置。</summary>
    private static Vector3 ResolveGroundPosition(Vector3 position)
    {
        WorldTile tile = World.world?.GetTile(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        return tile?.posV3 ?? new Vector3(position.x, position.y, 0f);
    }

    /// <summary>在固定容量内提交事件，避免不可见的大范围连续效果无限积压。</summary>
    private static void Enqueue(in SkillWorldVisualEvent visualEvent)
    {
        int count = Interlocked.Increment(ref pendingCount);
        if (count > MaxPendingEvents)
        {
            Interlocked.Decrement(ref pendingCount);
            return;
        }
        Events.Enqueue(visualEvent);
    }
}
