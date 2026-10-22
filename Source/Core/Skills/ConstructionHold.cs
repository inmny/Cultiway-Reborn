using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>在原动作时钟的指定姿态维持若干世界秒，其他阶段按原速度继续。</summary>
public sealed class ConstructionHold
{
    /// <summary>原动作时钟中的维持时点。</summary>
    public float At;
    /// <summary>额外维持的世界秒数。</summary>
    public float Seconds;
}

/// <summary>编译后的只读维持参数，身体和形变共用，不持有游戏对象。</summary>
internal readonly struct CompiledConstructionHold
{
    /// <summary>原动作中的时点及额外秒数，零秒表示没有维持。</summary>
    internal readonly float At, Seconds;

    /// <summary>在资料入口检查有限时点和正数时长。</summary>
    /// <param name="source">可选维持资料。</param>
    /// <param name="lifetime">能力总时长。</param>
    /// <param name="path">实际资料位置。</param>
    internal CompiledConstructionHold(ConstructionHold source, float lifetime, string path)
    {
        At = source?.At ?? 0f; Seconds = source?.Seconds ?? 0f;
        if (source != null && (float.IsNaN(At) || float.IsInfinity(At) || At < 0f ||
            float.IsNaN(Seconds) || float.IsInfinity(Seconds) || Seconds <= 0f || At + Seconds > lifetime))
            throw new InvalidDataException(path + "：维持需要有效时点和正数秒数，不能超过能力寿命");
    }

    /// <summary>身体从完整世界时间回到原动作时钟，维持期间停留在同一姿态。</summary>
    /// <param name="age">完整世界秒数。</param>
    internal float Local(float age) => age - Math.Min(Seconds, Math.Max(0f, age - At));

    /// <summary>推进形变时钟；物态定型或接触驻留期间，由调用者暂停本过程。</summary>
    /// <param name="age">当前原动作时钟。</param>
    /// <param name="used">已完成的维持世界秒数，随施放快照一起回退。</param>
    /// <param name="step">本次数值步。</param>
    internal float Advance(float age, ref float used, float step)
    {
        float held = Math.Min(Math.Max(0f, Seconds - used), Math.Max(0f, step - Math.Max(0f, At - age)));
        used += held; return age + step - held;
    }
}
