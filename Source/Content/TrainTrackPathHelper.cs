using System;
using System.Collections.Generic;

namespace Cultiway.Content
{
    internal static class TrainTrackPathHelper
    {
        public static List<WorldTile> BuildPath(WorldTile source_tile, WorldTile target_tile)
        {
            var path = new List<WorldTile>();
            if (source_tile == null || target_tile == null)
            {
                return path;
            }

            int x0 = source_tile.x;
            int y0 = source_tile.y;
            int x1 = target_tile.x;
            int y1 = target_tile.y;

            int dx = x1 - x0;
            int dy = y1 - y0;

            int signX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int signY = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            int px = x0;
            int py = y0;
            int absDx = Math.Abs(dx);
            int absDy = Math.Abs(dy);

            int totalSteps = Math.Max(absDx, absDy);
            int diagonalRate = 2;
            int diagonalLeft = Math.Min(absDx, absDy);

            for (int step = 0; px != x1 || py != y1;)
            {
                int nx = px;
                int ny = py;

                bool canDiagonal = (px != x1) && (py != y1) && diagonalLeft > 0;
                bool shouldDiagonal = canDiagonal && ((step % diagonalRate == 0) || diagonalLeft >= totalSteps - step);

                if (shouldDiagonal)
                {
                    var tile_middle = World.world.GetTile(px + signX, py);
                    if (tile_middle == null)
                    {
                        tile_middle = World.world.GetTile(px, py + signY);
                        if (tile_middle == null)
                        {
                            break;
                        }
                    }
                    path.Add(tile_middle);

                    nx = px + signX;
                    ny = py + signY;
                    diagonalLeft--;
                }
                else if (px != x1)
                {
                    nx = px + signX;
                    ny = py;
                }
                else
                {
                    nx = px;
                    ny = py + signY;
                }

                WorldTile nextTile = World.world.GetTile(nx, ny);
                if (nextTile == null)
                {
                    break;
                }
                path.Add(nextTile);
                px = nx;
                py = ny;
                step++;
            }

            return path;
        }
    }
}

