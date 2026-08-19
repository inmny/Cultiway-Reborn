using Cultiway.Abstract;
using Cultiway.Const;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>按少量地块逐步把洁净灵气向邻格摊开。</summary>
public class WakanSpreadSystem : BaseSystem, IWorldStateClearable
{
    private const int CheckTileCountPerFrame = 64;

    private int[] checkTileIds;
    private int lastCheckIndex;
    private int totalTileCount;

    protected override void OnUpdateGroup()
    {
        if (!GeneralSettings.EnableWakanSpread || MapGenerator._tilesMap != null ||
            World.world?.tiles_list == null || !WorldWakanService.IsInitialized)
        {
            return;
        }

        if (checkTileIds == null || checkTileIds.Length != World.world.tiles_list.Length)
        {
            RegenerateCheckIds(true);
        }

        int count = Mathf.Min(CheckTileCountPerFrame, checkTileIds.Length);
        for (int i = 0; i < count; i++)
        {
            int checkIndex = lastCheckIndex + 1;
            if (checkIndex >= checkTileIds.Length)
            {
                RegenerateCheckIds(false);
                checkIndex = 0;
            }

            WorldTile tile = World.world.tiles_list[checkTileIds[checkIndex]];
            if (tile != null) CheckSingleTile(tile);
            lastCheckIndex = checkIndex;
        }
    }

    private void RegenerateCheckIds(bool newArray)
    {
        if (newArray)
        {
            checkTileIds = new int[World.world.tiles_list.Length];
            for (int i = 0; i < checkTileIds.Length; i++) checkTileIds[i] = i;
            lastCheckIndex = -1;
        }

        checkTileIds.Shuffle();
        totalTileCount = checkTileIds.Length;
    }

    [Hotfixable]
    private void CheckSingleTile(WorldTile tile)
    {
        if (tile.neighbours == null) return;
        int tileId = tile.data.tile_id;
        foreach (WorldTile neighbor in tile.neighbours)
        {
            if (neighbor?.data == null) continue;
            float tileValue = WorldWakanService.GetClean(tileId);
            float neighborValue = WorldWakanService.GetClean(neighbor.data.tile_id);
            float delta = tileValue - neighborValue;
            float flow = Mathf.Sign(delta) * Mathf.Abs(delta) *
                Mathf.Clamp(Mathf.Log10(Mathf.Max(1f, totalTileCount / (float)CheckTileCountPerFrame)) * 0.1f,
                    0f, 1f);
            if (flow > 0f)
                WorldWakanService.TransferClean(tileId, neighbor.data.tile_id, flow);
            else if (flow < 0f)
                WorldWakanService.TransferClean(neighbor.data.tile_id, tileId, -flow);
        }
    }

    public void ClearWorldState()
    {
        checkTileIds = null;
        lastCheckIndex = 0;
        totalTileCount = 0;
    }
}
