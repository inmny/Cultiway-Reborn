using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class TileExtendManager : ExtendComponentManager<TileExtend>
{
    public readonly EntityStore  World;
    private         TileExtend[] _tile_extends;

    internal TileExtendManager()
    {
        World = new EntityStore();
    }

    public TileExtend Get(int tile_id)
    {
        return _tile_extends[tile_id];
    }

    public bool Ready()
    {
        return _tile_extends != null && _tile_extends.Length == global::World.world.tiles_list.Length;
    }

    internal void FitNewWorld()
    {
        World.Query<TileBinder>().ForEachEntity((ref TileBinder _, Entity e) => e.DeleteEntity());
        var tiles = global::World.world.tiles_list;
        _tile_extends = new TileExtend[tiles.Length];

        for (int i = 0; i < tiles.Length; i++)
        {
            _tile_extends[i] = new(World.CreateEntity(new TileBinder(i)));
        }
    }
}