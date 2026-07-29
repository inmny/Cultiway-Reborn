using System;

namespace Cultiway.Core.Combat;

/// <summary>
/// <see cref="SourcelessDamageLevels"/> 的持久化数据，按 <see cref="AttackType"/> 的整数值索引。
/// </summary>
[Serializable]
internal sealed class SourcelessDamageLevelsData
{
    /// <summary>无来源伤害替代等级，下标对应 <c>(int)AttackType</c>。</summary>
    public float[] Levels = Array.Empty<float>();
}
