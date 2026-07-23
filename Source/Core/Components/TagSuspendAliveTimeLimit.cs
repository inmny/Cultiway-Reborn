using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 临时禁止 AliveTimeLimit 触发回收，但不暂停 AliveTimer 本身。
/// </summary>
public struct TagSuspendAliveTimeLimit : ITag
{
}
