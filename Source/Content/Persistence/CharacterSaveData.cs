using System.Collections.Generic;
using Cultiway.Core.Components;

namespace Cultiway.Content.Persistence;

/// <summary>人物基础的正式存档记录；属性缓存由恢复后重新计算。</summary>
public sealed class CharacterSaveData
{
    /// <summary>灵根实际八维数值，没有灵根时为空。</summary>
    public float[] ElementRoot;
    /// <summary>已经产生的先天神识资质。</summary>
    public float? DivineSense;
    /// <summary>气运上限与当前气运。</summary>
    public Qiyun? Qiyun;
    /// <summary>各玩法共同使用的当前力量层次。</summary>
    public PowerLevel? PowerLevel;
    /// <summary>丹药等来源产生的永久属性，键为稳定属性编号。</summary>
    public Dictionary<string, float> PermanentStats;
}
