using System;

namespace Cultiway.Core.SubWorlds.Model;

[Serializable]
internal sealed class SubWorldMapData
{
    public int Width = 0;
    public int Height = 0;
    public SubWorldTile[] Tiles = [];
    public int[] EntryTileIndices = [];
    public int[] ExitTileIndices = [];
}
