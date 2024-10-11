using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components;

public struct SkillInfo : IComponent
{
    public ActorExtend   user;
    public BaseSimObject target;
    public WorldTile     target_tile;
    public float         energy;
}