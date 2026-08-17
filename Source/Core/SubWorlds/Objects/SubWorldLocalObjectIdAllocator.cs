using System;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>为一个 Runtime 确定性分配不复用的 Building LocalObjectId。</summary>
internal sealed class SubWorldLocalObjectIdAllocator
{
    private int nextValue = 1;

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
