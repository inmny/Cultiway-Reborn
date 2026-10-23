using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>同一接触组再次作用于同一人物的条件。</summary>
public enum ConstructionContactMode
{
    /// <summary>每次控制权交接期间，每人只作用一次。</summary>
    Once = 0,

    /// <summary>实际仍接触时，按世界秒数间隔再次作用。</summary>
    Interval = 1,

    /// <summary>离开全部同组攻击作用面后，再次进入才能重新作用。</summary>
    Reentry = 2
}

/// <summary>可保存的接触频率与次数限制；接触组内只保留一份规则。</summary>
public sealed class ConstructionContactPolicy
{
    /// <summary>再次作用需要满足的条件，缺省为每人一次。</summary>
    public ConstructionContactMode Mode;
    /// <summary>间隔模式的世界秒数，最小一百二十分之一秒。</summary>
    public float Interval = .25f;
    /// <summary>每次控制权交接期间，对每人的最多次数；零表示不另设上限。</summary>
    public int MaximumPerTarget;
    /// <summary>整次施放内该组的总次数；零不限制，达到后结束该组。</summary>
    public int MaximumTotal;
}

/// <summary>只读接触规则；运行期间不读取可编辑资料。</summary>
internal sealed class CompiledContactPolicy
{
    /// <summary>旧正式资料默认每人一次。</summary>
    internal static readonly CompiledContactPolicy Default = new(null, "默认接触");
    /// <summary>再次作用条件。</summary>
    internal readonly ConstructionContactMode Mode;
    /// <summary>间隔世界秒数，其他模式为零。</summary>
    internal readonly float Interval;
    /// <summary>每人次数及整组总次数。</summary>
    internal readonly int MaximumPerTarget, MaximumTotal;

    /// <summary>在资料边界检查并复制实际规则。</summary>
    /// <param name="source">可编辑规则，空值采用原有每人一次。</param>
    /// <param name="path">错误的实际位置。</param>
    internal CompiledContactPolicy(ConstructionContactPolicy source, string path)
    {
        if (source == null) return;
        if (!Enum.IsDefined(typeof(ConstructionContactMode), source.Mode) || float.IsNaN(source.Interval) ||
            float.IsInfinity(source.Interval) || source.Mode == ConstructionContactMode.Interval && source.Interval < 1f / 120f ||
            source.MaximumPerTarget < 0 || source.MaximumTotal < 0)
            throw new InvalidDataException(path + "：接触方式、间隔或次数无效");
        Mode = source.Mode; Interval = Mode == ConstructionContactMode.Interval ? source.Interval : 0f;
        MaximumPerTarget = source.MaximumPerTarget; MaximumTotal = source.MaximumTotal;
    }

    /// <summary>共享接触组不能混用不同次数或时间规则。</summary>
    /// <param name="other">同组的明确规则。</param>
    internal bool Matches(CompiledContactPolicy other) => Mode == other.Mode && Interval == other.Interval &&
        MaximumPerTarget == other.MaximumPerTarget && MaximumTotal == other.MaximumTotal;
}
