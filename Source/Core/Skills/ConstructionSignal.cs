using System;
using System.IO;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Skills;

/// <summary>可以由普通构造送达的已登记讯号。</summary>
public enum ConstructionSignalKind
{
    /// <summary>通知收件人附近有危险。</summary>
    Warning = 0,
    /// <summary>通知收件人集结。</summary>
    Rally = 1
}

/// <summary>发送给真实收件人的讯号。</summary>
public sealed class ConstructionSignal
{
    /// <summary>讯号内容种类。</summary>
    public ConstructionSignalKind Kind;
    /// <summary>完整强度下的有效世界秒数。</summary>
    public float Duration = 3f;
}

/// <summary>编译完成的讯号，使用共享状态维护收件和有效时间。</summary>
public sealed class CompiledConstructionSignal
{
    /// <summary>明确送达的内容种类。</summary>
    public ConstructionSignalKind Kind { get; }
    /// <summary>实际收件人登记的共享状态。</summary>
    public StatusEffectAsset Asset { get; }
    /// <summary>完整强度下的有效世界秒数。</summary>
    public float Duration { get; }

    /// <summary>只在构造编译时检查内容种类与时长。</summary>
    /// <param name="source">可保存的讯号声明。</param><param name="path">错误位置。</param>
    internal CompiledConstructionSignal(ConstructionSignal source, string path)
    {
        string id = source.Kind switch
        {
            ConstructionSignalKind.Warning => nameof(Content.StatusEffects.SignalWarning),
            ConstructionSignalKind.Rally => nameof(Content.StatusEffects.SignalRally),
            _ => null
        };
        if (id == null || (Asset = ModClass.L.StatusEffectLibrary.get(id)) == null ||
            float.IsNaN(source.Duration) || source.Duration <= 0 || source.Duration > 3600)
            throw new InvalidDataException(path + "：需要有效的讯号种类和大于零、至多3600秒的有效时间");
        Kind = source.Kind;
        Duration = source.Duration;
    }
}
