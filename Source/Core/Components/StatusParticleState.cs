using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct StatusParticleState : IComponent
{
    /// <summary>下次从生效状态发射的世界时间；读档时重新安排。</summary>
    public double NextAt;
}
