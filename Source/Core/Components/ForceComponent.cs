using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;
/// <summary>
/// 势力组件(最基础的一些信息，也可当标签用)
/// </summary>
public struct ForceComponent : IComponent
{
    
}
public interface IForceRelation : ILinkRelation
{
    public Entity ForceEntity { get; set; }
}