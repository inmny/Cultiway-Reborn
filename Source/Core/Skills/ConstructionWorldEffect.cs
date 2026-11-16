using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Core.SkillLibV3.Effects;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>实际部位接触地块时调用的普通世界操作，不包含完整招式或额外范围。</summary>
public sealed class ConstructionWorldEffect
{
    /// <summary>已经登记的世界操作稳定编号。</summary>
    public string Action;

    /// <summary>区域规则只采样地块中心，普通实体默认接触整块地面。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool TileCentreOnly;

    /// <summary>针对同一地块的频率及次数；空值表示每块一次。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactPolicy Contact;

    /// <summary>操作产生过程的实际持续秒数；空值使用该原理的默认值。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? Duration;

    /// <summary>本部位此操作最多接受的不同地块数，达到后不再接受新地块。</summary>
    public int MaximumTiles = 4096;
}

/// <summary>已经确认接触的实际世界操作，只在主线程提交，不进入长期资料。</summary>
public readonly struct ConstructionWorldContext
{
    /// <summary>已经确认接触的地块。</summary>
    public readonly WorldTile Tile;

    /// <summary>本次实际来源及构造编号，不把进程内施放编号用作长期身份。</summary>
    public readonly long SourceId;

    /// <summary>产生此作用的实际构造。</summary>
    public readonly string AbilityId;

    /// <summary>实际确认接触的共同世界时间，不能借用整轮末端时间。</summary>
    public readonly double WorldTime;

    /// <summary>已经解析的持续秒数，零表示该原理没有持续参数。</summary>
    public readonly float Duration;

    /// <summary>在实际接触处提供原理需要的来源和参数。</summary>
    /// <param name="tile">实际地块。</param><param name="sourceId">实际来源。</param>
    /// <param name="abilityId">实际构造。</param><param name="duration">已检查的持续秒数。</param>
    /// <param name="worldTime">实际接触时点。</param>
    internal ConstructionWorldContext(WorldTile tile, long sourceId, string abilityId, float duration, double worldTime)
    {
        Tile = tile;
        SourceId = sourceId;
        AbilityId = abilityId;
        Duration = duration;
        WorldTime = worldTime;
    }
}

/// <summary>在主线程调用原版世界规则的已登记操作。</summary>
public sealed class ConstructionWorldAction
{
    /// <summary>稳定编号，不根据图形或材料猜测玩法。</summary>
    public string Id { get; }

    /// <summary>实际地块是否适用。</summary>
    internal readonly Func<WorldTile, bool> CanApply;

    /// <summary>实际提交结果，数值计算线程不调用。</summary>
    internal readonly Func<ConstructionWorldContext, SkillEffectResult> Apply;

    /// <summary>原理默认的持续秒数；零表示不接受持续参数。</summary>
    public readonly float DefaultDuration;

    /// <summary>登记一个实际操作，不登记整招。</summary>
    /// <param name="id">稳定编号。</param><param name="canApply">只读实际预检。</param>
    /// <param name="apply">主线程世界提交。</param>
    public ConstructionWorldAction(string id, Func<WorldTile, bool> canApply, Func<WorldTile, SkillEffectResult> apply)
        : this(id, canApply, apply == null ? null : context => apply(context.Tile), 0)
    {
    }

    /// <summary>登记需要来源或持续参数的实际原理，不保存运行中的人物对象。</summary>
    /// <param name="id">稳定编号。</param><param name="canApply">只读地块预检。</param>
    /// <param name="apply">主线程世界提交。</param><param name="defaultDuration">默认持续秒数，零表示没有持续参数。</param>
    public ConstructionWorldAction(string id, Func<WorldTile, bool> canApply,
        Func<ConstructionWorldContext, SkillEffectResult> apply, float defaultDuration)
    {
        if (string.IsNullOrWhiteSpace(id) || canApply == null || apply == null ||
            float.IsNaN(defaultDuration) || float.IsInfinity(defaultDuration) || defaultDuration < 0 ||
            defaultDuration > 3600)
            throw new ArgumentException("世界操作需要编号、实际预检、提交和有效默认持续时间");
        Id = id;
        CanApply = canApply;
        Apply = apply;
        DefaultDuration = defaultDuration;
    }
}

/// <summary>世界操作在内容初始化时登记，构造入口解析实际规则。</summary>
public static class ConstructionWorldActions
{
    /// <summary>进程内的稳定操作目录，世界清理不删除规则。</summary>
    private static readonly Dictionary<string, ConstructionWorldAction> actions = new(StringComparer.Ordinal);

    /// <summary>只读登记目录，供编辑和受理时采集稳定输入。</summary>
    public static IEnumerable<ConstructionWorldAction> All => actions.Values;

    /// <summary>新增一种原理供任意构造组合，不替换已经运行的规则。</summary>
    /// <param name="action">实际规则。</param>
    public static void Register(ConstructionWorldAction action)
    {
        if (action == null || actions.ContainsKey(action.Id)) throw new ArgumentException("世界操作为空或编号重复");
        actions.Add(action.Id, action);
    }

    /// <summary>在资料边界拒绝未知规则，保留具体错误位置。</summary>
    /// <param name="id">稳定编号。</param><param name="path">当前资料位置。</param>
    internal static ConstructionWorldAction Resolve(string id, string path) =>
        id != null && actions.TryGetValue(id, out var action)
            ? action
            : throw new InvalidDataException(path + "：尚未登记世界操作：" + id);
}

/// <summary>已经复制和解析的地块接触规则。</summary>
public sealed class CompiledConstructionWorldEffect
{
    /// <summary>已登记的实际世界操作。</summary>
    public ConstructionWorldAction Action { get; }

    /// <summary>不同地块的显式选择上限。</summary>
    public int MaximumTiles { get; }

    /// <summary>是否以地块中心判断区域成员，而非碰到地块边沿即生效。</summary>
    public bool TileCentreOnly { get; }

    /// <summary>从原资料或原理默认值解析的持续秒数。</summary>
    public float Duration { get; }

    /// <summary>沿用共同接触规则的数值副本。</summary>
    internal readonly CompiledContactPolicy Contact;

    /// <summary>只在资料入口解析和检查。</summary>
    /// <param name="source">外部规则。</param><param name="path">资料位置。</param>
    internal CompiledConstructionWorldEffect(ConstructionWorldEffect source, string path)
    {
        if (source == null || source.MaximumTiles < 1 || source.MaximumTiles > 1048576)
            throw new InvalidDataException(path + "：世界接触需要1至1048576个明确地块上限");
        Action = ConstructionWorldActions.Resolve(source.Action, path);
        if (source.Duration.HasValue && (Action.DefaultDuration == 0 ||
                                         float.IsNaN(source.Duration.Value) ||
                                         float.IsInfinity(source.Duration.Value) ||
                                         source.Duration.Value <= 0 || source.Duration.Value > 3600))
            throw new InvalidDataException(path + "：此原理不接受持续参数，或持续时间不是大于零且不超过3600秒");
        Duration = source.Duration ?? Action.DefaultDuration;
        MaximumTiles = source.MaximumTiles;
        TileCentreOnly = source.TileCentreOnly;
        Contact = new CompiledContactPolicy(source.Contact, path + "/Contact");
    }
}