using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>同一真实接触可以附带的恢复和净化，不读取攻击力，也不自行扩大作用区。</summary>
public sealed class ConstructionRestoration
{
    /// <summary>接触时立即恢复的生命量。</summary>
    public float Health;
    /// <summary>全局同类最强的每秒恢复量；零表示不施加恢复状态。</summary>
    public float HealthPerSecond;
    /// <summary>恢复状态离开作用面后最多保留的世界秒数。</summary>
    public float Duration = 1.25f;
    /// <summary>按共享负状态优先级最多清除的数量。</summary>
    public int CleanseCount;
}

/// <summary>在创建、编辑及保存读回边界检查并复制的恢复资料。</summary>
public sealed class CompiledConstructionRestoration
{
    /// <summary>实际即时恢复量。</summary>
    public float Health { get; }
    /// <summary>实际每秒恢复量。</summary>
    public float HealthPerSecond { get; }
    /// <summary>状态最多保留的世界秒数。</summary>
    public float Duration { get; }
    /// <summary>实际净化数量。</summary>
    public int CleanseCount { get; }
    /// <summary>是否声明了至少一种人物作用。</summary>
    public bool HasEffect => Health > 0f || HealthPerSecond > 0f || CleanseCount > 0;

    /// <summary>恢复量必须有限，持续恢复需要正时长，不替用户修正非法资料。</summary>
    /// <param name="source">可保存资料。</param><param name="path">错误位置。</param>
    internal CompiledConstructionRestoration(ConstructionRestoration source, string path)
    {
        if (!Amount(source.Health) || !Amount(source.HealthPerSecond) ||
            float.IsNaN(source.Duration) || float.IsInfinity(source.Duration) || source.Duration < 0f ||
            source.Duration > 3600f || source.HealthPerSecond > 0f && source.Duration <= 0f ||
            source.CleanseCount < 0 || source.CleanseCount > 1024)
            throw new InvalidDataException(path + "：恢复量需要0至一百万的有限数值，持续恢复需0至3600内的正时长，净化数量需0至1024");
        Health = source.Health;
        HealthPerSecond = source.HealthPerSecond;
        Duration = source.Duration;
        CleanseCount = source.CleanseCount;
    }

    /// <summary>只在编译边界检查恢复量。</summary>
    /// <param name="value">实际资料数值。</param>
    private static bool Amount(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1000000f;
}
