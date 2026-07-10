using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 记录宗门所有物品当前的借用者。
/// </summary>
public struct SectTreasureLoan : IComponent
{
    public long SectId;
    public long BorrowerActorId;
}
