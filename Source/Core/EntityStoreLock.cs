namespace Cultiway.Core;

/// <summary>
/// 全局锁用于同步所有对EntityStore的写操作
/// 确保在多线程环境下，所有对EntityStore的操作都是序列化的
/// </summary>
public static class EntityStoreLock
{
    public static readonly object GlobalLock = new();
}
