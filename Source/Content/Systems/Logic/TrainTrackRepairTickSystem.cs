using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content
{
    public class TrainTrackRepairSystem : BaseSystem
    {
        private const float RepairInterval = 2f;
        private const float RetryDelay = 5f;
        private const float Cooldown = 10f;
        private const int MaxPerTick = 3;
        private const int MaxFail = 3;

        private static readonly Dictionary<LinkKey, LinkInfo> Links = new();
        private static readonly Dictionary<int, HashSet<LinkKey>> TileMap = new();
        private static readonly HashSet<LinkKey> Pending = new();
        private static float _nextTickTime;

        internal static void RegisterLink(Building stationA, Building stationB, IReadOnlyList<WorldTile> tiles)
        {
            if (stationA == null || stationB == null || tiles == null || tiles.Count == 0)
            {
                return;
            }

            var key = LinkKey.Create(stationA.id, stationB.id);
            if (Links.TryGetValue(key, out var old))
            {
                RemoveTileMap(old);
            }

            var info = new LinkInfo(key, stationA.id, stationB.id, new List<WorldTile>(tiles))
            {
                Status = LinkStatus.Normal,
                NextRepairTime = 0f,
                FailCount = 0
            };
            Links[key] = info;
            AddTileMap(info);
        }

        internal static void MarkTileDamaged(WorldTile tile)
        {
            if (tile == null)
            {
                return;
            }

            if (!TileMap.TryGetValue(tile.tile_id, out var keys))
            {
                return;
            }

            foreach (var key in keys)
            {
                if (!Links.TryGetValue(key, out var link))
                {
                    continue;
                }

                if (link.Status == LinkStatus.Disabled)
                {
                    continue;
                }

                link.Status = LinkStatus.Pending;
                link.NextRepairTime = Time.time;
                Pending.Add(key);
            }
        }

        internal static void MarkStationDisabled(Building station)
        {
            if (station == null)
            {
                return;
            }

            long stationId = station.id;
            foreach (var pair in Links)
            {
                if (!pair.Key.Contains(stationId))
                {
                    continue;
                }

                pair.Value.Status = LinkStatus.Disabled;
                RemoveTileMap(pair.Value);
                Pending.Remove(pair.Key);
            }
        }

        private static bool TryRepair(LinkInfo link)
        {
            var stationA = World.world.buildings.get(link.StationAId);
            var stationB = World.world.buildings.get(link.StationBId);
            if (!IsStationValid(stationA) || !IsStationValid(stationB))
            {
                link.Status = LinkStatus.Disabled;
                return false;
            }

            if (TryRefill(link))
            {
                return true;
            }

            return TryRebuild(link, stationA, stationB);
        }

        private static bool TryRefill(LinkInfo link)
        {
            if (link.Tiles.Count == 0)
            {
                return false;
            }

            foreach (var tile in link.Tiles)
            {
                if (tile == null || tile.Type == null)
                {
                    return false;
                }

                if (tile.top_type != TopTileTypes.TrainTrack)
                {
                    MapAction.terraformTop(tile, TopTileTypes.TrainTrack, Terraforms.TrainTrack, false);
                }
            }

            return link.Tiles.All(t => t != null && t.top_type == TopTileTypes.TrainTrack);
        }

        private static bool TryRebuild(LinkInfo link, Building stationA, Building stationB)
        {
            var sourceTile = stationA.current_tile;
            var targetTile = stationB.current_tile;
            if (sourceTile == null || targetTile == null)
            {
                return false;
            }

            var path = TrainTrackPathHelper.BuildPath(sourceTile, targetTile);
            if (path.Count == 0)
            {
                return false;
            }

            foreach (var tile in path)
            {
                MapAction.terraformTop(tile, TopTileTypes.TrainTrack, Terraforms.TrainTrack, false);
            }

            RemoveTileMap(link);
            link.Tiles.Clear();
            link.Tiles.AddRange(path);
            AddTileMap(link);
            return true;
        }

        private static bool IsStationValid(Building station)
        {
            if (station == null)
            {
                return false;
            }

            if (station.asset == null || station.asset.id != Buildings.TrainStation.id)
            {
                return false;
            }

            return station.isNormal() && !station.isRemoved() && !station.isRuin();
        }

        private static void AddTileMap(LinkInfo info)
        {
            foreach (var tile in info.Tiles)
            {
                if (tile == null)
                {
                    continue;
                }

                if (!TileMap.TryGetValue(tile.tile_id, out var set))
                {
                    set = new HashSet<LinkKey>();
                    TileMap[tile.tile_id] = set;
                }
                set.Add(info.Key);
            }
        }

        private static void RemoveTileMap(LinkInfo info)
        {
            foreach (var tile in info.Tiles)
            {
                if (tile == null)
                {
                    continue;
                }

                if (!TileMap.TryGetValue(tile.tile_id, out var set))
                {
                    continue;
                }

                set.Remove(info.Key);
                if (set.Count == 0)
                {
                    TileMap.Remove(tile.tile_id);
                }
            }
        }

        private sealed class LinkInfo
        {
            public LinkInfo(LinkKey key, long stationAId, long stationBId, List<WorldTile> tiles)
            {
                Key = key;
                StationAId = stationAId;
                StationBId = stationBId;
                Tiles = tiles;
            }

            public LinkKey Key { get; }
            public long StationAId { get; }
            public long StationBId { get; }
            public List<WorldTile> Tiles { get; }
            public float NextRepairTime { get; set; }
            public int FailCount { get; set; }
            public LinkStatus Status { get; set; }
        }

        private readonly struct LinkKey : IEquatable<LinkKey>
        {
            public LinkKey(long a, long b)
            {
                A = a;
                B = b;
            }

            public long A { get; }
            public long B { get; }

            public static LinkKey Create(long a, long b)
            {
                return a <= b ? new LinkKey(a, b) : new LinkKey(b, a);
            }

            public bool Contains(long id)
            {
                return id == A || id == B;
            }

            public bool Equals(LinkKey other)
            {
                return A == other.A && B == other.B;
            }

            public override bool Equals(object obj)
            {
                return obj is LinkKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A.GetHashCode() * 397) ^ B.GetHashCode();
                }
            }
        }

        private enum LinkStatus
        {
            Normal,
            Pending,
            Disabled
        }

        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();

            if (Time.time < _nextTickTime)
            {
                return;
            }

            _nextTickTime = Time.time + RepairInterval;
            int handled = 0;

            foreach (var key in Pending.ToList())
            {
                if (handled >= MaxPerTick)
                {
                    break;
                }

                if (!Links.TryGetValue(key, out var link))
                {
                    Pending.Remove(key);
                    continue;
                }

                if (link.Status == LinkStatus.Disabled)
                {
                    Pending.Remove(key);
                    continue;
                }

                if (link.NextRepairTime > Time.time)
                {
                    continue;
                }

                bool repaired = TryRepair(link);
                if (repaired)
                {
                    link.Status = LinkStatus.Normal;
                    link.FailCount = 0;
                    link.NextRepairTime = Time.time + Cooldown;
                    Pending.Remove(key);
                }
                else
                {
                    link.FailCount++;
                    link.Status = LinkStatus.Pending;
                    link.NextRepairTime = Time.time + RetryDelay;
                    if (link.FailCount >= MaxFail)
                    {
                        link.Status = LinkStatus.Disabled;
                        Pending.Remove(key);
                        ModClass.LogWarning("TrainTrack 修复失败次数过多，暂时禁用该链路");
                    }
                }

                handled++;
            }
        }
    }
}

