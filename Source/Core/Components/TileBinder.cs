using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct TileBinder(int idx) : IComponent
{
    public int       tile_idx = idx;
    public WorldTile Tile => World.world.tilesList[tile_idx];
}