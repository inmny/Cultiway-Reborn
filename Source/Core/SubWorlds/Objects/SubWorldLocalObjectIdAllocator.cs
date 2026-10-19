using System;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>为一个 Runtime 确定性分配不复用的 Building LocalObjectId。</summary>
internal sealed class SubWorldLocalObjectIdAllocator
{
    private int nextValue = 1;

    /// <summary>保存时保留已删除对象占用过的编号。</summary>
    internal int NextValue => nextValue;

    /// <summary>读取已核对的分配进度，保留已经删除对象占用过的编号。</summary>
    /// <param name="value">当前值。</param>
    internal void RestoreNextValue(int value) => nextValue = value;

    internal LocalObjectId Allocate()
    {
        if (nextValue == int.MaxValue) throw new InvalidOperationException("SubWorld LocalObjectId 已耗尽");
        return new LocalObjectId(nextValue++);
    }

    internal void Reserve(LocalObjectId localObjectId)
    {
        if (!localObjectId.IsValid) throw new ArgumentException("LocalObjectId 无效", nameof(localObjectId));
        if (localObjectId.Value < nextValue) return;
        if (localObjectId.Value == int.MaxValue) throw new InvalidOperationException("SubWorld LocalObjectId 已耗尽");
        nextValue = localObjectId.Value + 1;
    }
}
