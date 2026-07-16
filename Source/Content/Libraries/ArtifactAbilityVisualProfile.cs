using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 多个同类法器视觉同时出现时的合并方式。该策略只影响表现，不改变能力结算。
/// </summary>
public enum ArtifactVisualStackPolicy
{
    /// <summary>每个能力实例保留自己的视觉。</summary>
    Independent,

    /// <summary>同一驾驭者只显示强度最高的一份视觉。</summary>
    Strongest,

    /// <summary>同一驾驭者合并成一份视觉，并累加强度。</summary>
    MergeIntensity,

    /// <summary>同一驾驭者只保留一份视觉；再次触发时刷新其上下文和时长。</summary>
    SinglePerController,
}

/// <summary>
/// 视觉相对于玩法对象的跟随锚点。
/// </summary>
public enum ArtifactVisualAnchorKind
{
    Controller,
    Artifact,
    ActiveExecution,
    Target,
    Point,
    DeploymentOrigin,
}

/// <summary>
/// cue 从法器外观或能力后备元素得到的统一配色。
/// </summary>
public readonly struct ArtifactVisualTheme
{
    public readonly Color primary;
    public readonly Color secondary;
    public readonly Color glow;

    public ArtifactVisualTheme(Color primary, Color secondary, Color glow)
    {
        this.primary = primary;
        this.secondary = secondary;
        this.glow = glow;
    }

    public static ArtifactVisualTheme FromPrimary(Color primary)
    {
        return new ArtifactVisualTheme(
            primary,
            Color.Lerp(primary, Color.black, 0.28f),
            Color.Lerp(primary, Color.white, 0.58f));
    }
}

/// <summary>
/// 一份视觉在创建和逐帧更新时读取的瞬时上下文。这里不保存任何渲染句柄，也不会进入存档。
/// </summary>
public readonly struct ArtifactAbilityVisualContext
{
    public readonly Entity controller;
    public readonly Entity artifact;
    public readonly ArtifactAbilityAsset asset;
    public readonly ArtifactAbilityInstance ability;
    public readonly ArtifactAbilityRuntimeEntry runtime;
    public readonly ArtifactControlState control_state;
    public readonly ArtifactVisualTheme theme;
    public readonly string channel;
    public readonly Vector3 position;
    public readonly Vector3 direction;
    public readonly BaseSimObject target;
    public readonly float intensity;
    public readonly ArtifactAbilityEndReason? end_reason;

    public ArtifactAbilityVisualContext(
        Entity controller,
        Entity artifact,
        ArtifactAbilityAsset asset,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        ArtifactControlState controlState,
        ArtifactVisualTheme theme,
        string channel = "",
        Vector3 position = default,
        Vector3 direction = default,
        BaseSimObject target = null,
        float intensity = 1f,
        ArtifactAbilityEndReason? endReason = null)
    {
        this.controller = controller;
        this.artifact = artifact;
        this.asset = asset;
        this.ability = ability;
        this.runtime = runtime;
        control_state = controlState;
        this.theme = theme;
        this.channel = channel ?? string.Empty;
        this.position = position;
        this.direction = direction;
        this.target = target;
        this.intensity = intensity;
        end_reason = endReason;
    }

    public ArtifactAbilityVisualContext WithIntensity(float value)
    {
        return new ArtifactAbilityVisualContext(
            controller,
            artifact,
            asset,
            ability,
            runtime,
            control_state,
            theme,
            channel,
            position,
            direction,
            target,
            value,
            end_reason);
    }
}

/// <summary>
/// 可插拔视觉 cue。实现负责创建一种表现载体，生命周期统一交给视觉系统管理。
/// </summary>
public interface IArtifactVisualCue
{
    IArtifactVisualLease Begin(ArtifactAbilityVisualContext context, double now, float duration);
}

/// <summary>
/// 一份仅存在于当前运行期的视觉租约。持续视觉和短时信号使用同一套清理协议。
/// </summary>
public interface IArtifactVisualLease
{
    bool IsAlive { get; }

    void Refresh(ArtifactAbilityVisualContext context, double now, float duration);

    void Update(ArtifactAbilityVisualContext context, double now);

    void End();
}

/// <summary>
/// 从权威能力运行状态推导的一条持续视觉声明。
/// </summary>
public sealed class ArtifactAbilityVisualLoop
{
    public string channel;
    public string stack_group;
    public ArtifactVisualStackPolicy stack_policy;
    public IArtifactVisualCue cue;
    public Func<ArtifactAbilityVisualContext, bool> IsActive;
    public Func<ArtifactAbilityVisualContext, float> ResolveIntensity;
}

/// <summary>
/// 某个语义信号对应的短时视觉声明。信号名使用字符串，新增幻想能力无需扩充中心枚举。
/// </summary>
public sealed class ArtifactAbilityVisualSignal
{
    public string channel;
    public string stack_group;
    public ArtifactVisualStackPolicy stack_policy;
    public IArtifactVisualCue cue;
    public float duration = 0.5f;
}

/// <summary>
/// 法器能力可选的视觉档案。它只声明状态到 cue 的映射，不持有运行时对象。
/// </summary>
public sealed class ArtifactAbilityVisualProfile
{
    private readonly List<ArtifactAbilityVisualLoop> loops = new();
    private readonly Dictionary<string, ArtifactAbilityVisualSignal> signals = new(StringComparer.Ordinal);

    public ArtifactVisualTheme? explicit_theme;
    public ArtifactVisualTheme? fallback_theme;
    public IReadOnlyList<ArtifactAbilityVisualLoop> Loops => loops;

    public ArtifactAbilityVisualProfile Loop(
        string channel,
        IArtifactVisualCue cue,
        Func<ArtifactAbilityVisualContext, bool> isActive,
        string stackGroup = null,
        ArtifactVisualStackPolicy stackPolicy = ArtifactVisualStackPolicy.Independent,
        Func<ArtifactAbilityVisualContext, float> resolveIntensity = null)
    {
        if (string.IsNullOrEmpty(channel)) throw new ArgumentException("持续视觉缺少通道", nameof(channel));
        loops.Add(new ArtifactAbilityVisualLoop
        {
            channel = channel,
            stack_group = string.IsNullOrEmpty(stackGroup) ? channel : stackGroup,
            stack_policy = stackPolicy,
            cue = cue ?? throw new ArgumentNullException(nameof(cue)),
            IsActive = isActive ?? throw new ArgumentNullException(nameof(isActive)),
            ResolveIntensity = resolveIntensity,
        });
        return this;
    }

    public ArtifactAbilityVisualProfile Signal(
        string channel,
        IArtifactVisualCue cue,
        float duration,
        string stackGroup = null,
        ArtifactVisualStackPolicy stackPolicy = ArtifactVisualStackPolicy.Independent)
    {
        if (string.IsNullOrEmpty(channel)) throw new ArgumentException("短时视觉缺少信号通道", nameof(channel));
        signals[channel] = new ArtifactAbilityVisualSignal
        {
            channel = channel,
            stack_group = string.IsNullOrEmpty(stackGroup) ? channel : stackGroup,
            stack_policy = stackPolicy,
            cue = cue ?? throw new ArgumentNullException(nameof(cue)),
            duration = Mathf.Max(0.01f, duration),
        };
        return this;
    }

    public bool TryGetSignal(string channel, out ArtifactAbilityVisualSignal signal)
    {
        return signals.TryGetValue(channel, out signal);
    }
}
