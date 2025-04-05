using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

public struct TileBinder(int idx) : IComponent
{
    public int       tile_idx = idx;
    [Ignore]
    public WorldTile Tile => World.world.tiles_list[tile_idx];
}