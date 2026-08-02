using System;
using System.Collections.Generic;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

/// <summary>
/// 陆块分类与名称后缀随实际连通陆地大小实时更新。
/// 轮询 Landmass 层地区，分帧 flood fill 统计当前连通陆地数与是否贴边，
/// 跨过岛/洲/大陆阈值时自动更换分类并更新名称后缀（仅自动命名地区）。
/// </summary>
public class GeoRegionLandmassLiveUpdateSystem : BaseSystem
{
    /// <summary>单帧 flood fill 预算，超过则分帧续跑，避免大洲重算造成卡顿。</summary>
    private const int MaxTilesPerTick = 40_000;

    private readonly List<int> _queue = new();
    private bool[] _visited = Array.Empty<bool>();
    private WorldTile[] _tiles = Array.Empty<WorldTile>();
    private GeoRegion _current;
    private int _width;
    private int _height;
    private int _head;
    private int _tail;
    private int _cursor;
    private bool _touchesEdge;
    private bool _active;

    private static readonly string[] IslandTypeWords =
        SplitTypeWords(WorldboxGame.NameGenerators.LandmassIslandTypes);
    private static readonly string[] ContinentTypeWords =
        SplitTypeWords(WorldboxGame.NameGenerators.LandmassContinentTypes);
    private static readonly string[] MainlandTypeWords =
        SplitTypeWords(WorldboxGame.NameGenerators.LandmassMainlandTypes);
    /// <summary>全部通名按长度降序，用于从名字尾部剥离通名。</summary>
    private static readonly string[] AllTypeWords = BuildAllTypeWords();

    private static string[] SplitTypeWords(string words)
    {
        if (string.IsNullOrEmpty(words)) return Array.Empty<string>();

        string[] parts = words.Split(',');
        var result = new List<string>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            string trimmed = parts[i].Trim();
            if (trimmed.Length > 0) result.Add(trimmed);
        }

        return result.ToArray();
    }

    private static string[] BuildAllTypeWords()
    {
        var all = new List<string>(IslandTypeWords.Length + ContinentTypeWords.Length + MainlandTypeWords.Length);
        all.AddRange(IslandTypeWords);
        all.AddRange(ContinentTypeWords);
        all.AddRange(MainlandTypeWords);
        all.Sort((a, b) => b.Length.CompareTo(a.Length));
        return all.ToArray();
    }

    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();

        try
        {
            UpdateInternal();
        }
        catch (Exception e)
        {
            // 单帧重算失败不中断整个模拟循环，重置内部状态后下帧继续。
            ModClass.LogError($"[GeoRegion] 陆块实时更新失败: {e}");
            ResetState();
        }
    }

    private void UpdateInternal()
    {
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager == null || !manager.IsMembershipReady) return;

        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null || tiles.Length == 0) return;

        EnsureScratch(tiles);

        if (!_active || _current == null || _current.isRekt() || _current.data == null)
        {
            AbandonFillIfNeeded();
            if (!PickNextRegion(manager)) return;
        }

        FillStep();

        if (_head >= _tail)
        {
            FinishRegion();
            _active = false;
        }
    }

    private void ResetState()
    {
        if (_tail > 0 && _queue.Count > 0)
        {
            int count = Math.Min(_tail, _queue.Count);
            for (int i = 0; i < count; i++)
            {
                _visited[_queue[i]] = false;
            }
        }

        _queue.Clear();
        _head = 0;
        _tail = 0;
        _current = null;
        _active = false;
    }

    /// <summary>
    /// 世界（tiles_list 引用）变化时重建暂存缓冲，并丢弃进行中的 fill 状态。
    /// </summary>
    private void EnsureScratch(WorldTile[] tiles)
    {
        if (ReferenceEquals(_tiles, tiles)) return;

        _tiles = tiles;
        _width = MapBox.width;
        _height = MapBox.height;
        _visited = new bool[tiles.Length];
        _queue.Clear();
        _head = 0;
        _tail = 0;
        _current = null;
        _active = false;
    }

    /// <summary>
    /// 进行中的 fill 被中断（地区失效/换图）时，归还 visited 标记并清空队列。
    /// </summary>
    private void AbandonFillIfNeeded()
    {
        if (_tail == 0 || _queue.Count == 0) return;

        int count = Math.Min(_tail, _queue.Count);
        for (int i = 0; i < count; i++)
        {
            _visited[_queue[i]] = false;
        }
        _queue.Clear();
        _head = 0;
        _tail = 0;
    }

    /// <summary>
    /// 轮询下一个未完成的 Landmass 地区，找到陆地种子点后开始 fill。
    /// </summary>
    private bool PickNextRegion(GeoRegionManager manager)
    {
        List<GeoRegion> list = manager.list;
        int count = list.Count;
        if (count == 0) return false;

        int start = _cursor % count;
        for (int i = 0; i < count; i++)
        {
            int index = (start + i) % count;
            GeoRegion region = list[index];
            if (region?.data == null || region.isRekt() || region.E.IsNull) continue;
            if (region.data.Layer != GeoRegionLayer.Landmass) continue;

            int seed = FindLandSeed(region);
            if (seed < 0) continue;

            _cursor = (index + 1) % count;
            _current = region;
            StartFill(seed);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 优先用地区中心作为种子点，其次退回任一仍为陆地的成员地块。
    /// </summary>
    private int FindLandSeed(GeoRegion region)
    {
        GeoRegionData data = region.data;
        int center = data.CenterY * _width + data.CenterX;
        if ((uint)center < (uint)_tiles.Length && IsLandTile(_tiles[center]))
        {
            return center;
        }

        IReadOnlyList<int> tileIds = WorldboxGame.I.GeoRegions.GetRegionTileIds(region);
        for (int i = 0; i < tileIds.Count; i++)
        {
            int tileId = tileIds[i];
            if ((uint)tileId >= (uint)_tiles.Length) continue;
            if (IsLandTile(_tiles[tileId])) return tileId;
        }

        return -1;
    }

    private void StartFill(int seed)
    {
        _width = MapBox.width;
        _height = MapBox.height;
        _queue.Clear();
        _head = 0;
        _tail = 0;
        _touchesEdge = false;
        TryEnqueue(seed);
        _active = true;
    }

    /// <summary>
    /// 从队列消费最多 MaxTilesPerTick 个地块，剩余部分留到下一帧。
    /// </summary>
    private void FillStep()
    {
        int budget = Math.Max(1, MaxTilesPerTick);
        while (_head < _tail && budget > 0)
        {
            int idx = _queue[_head++];
            WorldTile tile = _tiles[idx];
            int x = tile.x;
            int y = tile.y;

            if (x == 0 || y == 0 || x == _width - 1 || y == _height - 1)
            {
                _touchesEdge = true;
            }

            if (x > 0) TryEnqueue(idx - 1);
            if (x < _width - 1) TryEnqueue(idx + 1);
            if (y > 0) TryEnqueue(idx - _width);
            if (y < _height - 1) TryEnqueue(idx + _width);

            budget--;
        }
    }

    private void TryEnqueue(int tileId)
    {
        if (_visited[tileId]) return;
        if (!IsLandTile(_tiles[tileId])) return;

        _visited[tileId] = true;
        _queue.Add(tileId);
        _tail++;
    }

    /// <summary>
    /// fill 完成后仅更新面积，并依据当前大小重新判定岛/洲/大陆分类与名称。
    /// 不写回 CenterX/CenterY：种子始终取分区器写入的中心，避免自我污染导致分类反复横跳。
    /// 连通陆地低于岛的最低阈值时直接丢弃地区，不再命名。
    /// 跨档重命名时若组件内还有其他地区（合并），新名取面积最大贡献者的前缀（平票随机），
    /// 并同步到组件内所有地区，避免同一片陆地显示多个名字。
    /// </summary>
    private void FinishRegion()
    {
        int newCount = _tail;

        GeoRegion region = _current;
        GeoRegionLibrary lib = ModClass.L?.GeoRegionLibrary;

        int minTiles = lib?.LandmassIsland?.MinTiles ?? 0;
        if (minTiles > 0 && newCount < minTiles)
        {
            DiscardRegion(region, newCount);
            return;
        }

        GeoRegionAsset newCategory = lib?.ResolveLandmass(_touchesEdge, newCount);
        bool canRename = region?.data != null && newCategory != null &&
                         !string.Equals(region.data.CategoryId, newCategory.id, StringComparison.Ordinal);

        // 合并检测：仅在跨档重命名时收集组件内各地区的实际贡献面积（此时队列尚未清空）。
        Dictionary<GeoRegion, int> contributors = null;
        if (canRename && !region.data.custom_name)
        {
            contributors = CollectContributors();
        }

        for (int i = 0; i < newCount; i++)
        {
            _visited[_queue[i]] = false;
        }
        _queue.Clear();
        _head = 0;
        _tail = 0;

        if (region?.data == null) return;

        GeoRegionData data = region.data;
        int oldCount = data.TileCount;
        data.TileCount = newCount;

        if (!canRename) return;

        GeoRegionAsset oldCategory = string.IsNullOrEmpty(data.CategoryId)
            ? null
            : lib.getSimple(data.CategoryId);
        data.CategoryId = newCategory.id;

        // 自定义命名不覆盖，仅更新分类。
        if (data.custom_name) return;

        GeoRegion dominant = contributors == null || contributors.Count <= 1 ? null : FindDominant(contributors);
        string mergedName = dominant == null ? null : BuildMergedName(lib, newCategory, dominant);
        if (mergedName != null)
        {
            data.name = mergedName;
        }
        else
        {
            string oldName = data.name;
            string[] oldWords = oldCategory == null ? null : GetTypeWords(lib, oldCategory);
            string prefix = oldWords == null ? null : StripTypeSuffix(oldName, oldWords);
            if (string.IsNullOrEmpty(prefix) && oldCategory != null)
            {
                prefix = StripTypeSuffix(oldName, AllTypeWords);
            }

            string[] newWords = GetTypeWords(lib, newCategory);
            if (!string.IsNullOrEmpty(prefix) && newWords != null && newWords.Length > 0)
            {
                data.name = prefix + newWords[UnityEngine.Random.Range(0, newWords.Length)];
            }
            else
            {
                data.name = GenerateFallbackName(newCategory, data.CenterX, data.CenterY);
            }
        }

        // 把新名/分类/面积同步到组件内其他地区。
        if (contributors != null && contributors.Count > 1)
        {
            foreach (KeyValuePair<GeoRegion, int> kv in contributors)
            {
                GeoRegion other = kv.Key;
                if (other == region || other?.data == null || other.isRekt()) continue;
                if (other.data.Layer != GeoRegionLayer.Landmass) continue;
                other.data.CategoryId = newCategory.id;
                other.data.TileCount = newCount;
                if (other.data.custom_name) continue;
                other.data.name = data.name;
            }
        }

        ModClass.LogInfo(
            $"[GeoRegion] 陆块分类更新: id={region.getID()} tiles={oldCount}->{newCount}, " +
            $"{oldCategory?.DisplayName ?? "?"}->{newCategory.DisplayName}, name={data.name}" +
            (dominant != null && dominant != region ? $", 主源=id{dominant.getID()}" : ""));
    }

    /// <summary>
    /// 统计组件内每个 Landmass 地区实际覆盖的陆地格数（调用时队列必须尚未清空）。
    /// </summary>
    private Dictionary<GeoRegion, int> CollectContributors()
    {
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager == null || !manager.IsMembershipReady) return null;

        var contributions = new Dictionary<GeoRegion, int>();
        for (int i = 0; i < _queue.Count; i++)
        {
            GeoRegion r = manager.GetRegionForTile(_queue[i], GeoRegionLayer.Landmass);
            if (r?.data == null || r.isRekt()) continue;
            if (r.data.Layer != GeoRegionLayer.Landmass) continue;
            contributions.TryGetValue(r, out int count);
            contributions[r] = count + 1;
        }

        return contributions.Count > 0 ? contributions : null;
    }

    /// <summary>
    /// 面积最大的贡献者；多个并列最大时随机取一个。
    /// </summary>
    private static GeoRegion FindDominant(Dictionary<GeoRegion, int> contributions)
    {
        int max = 0;
        var tied = new List<GeoRegion>();
        foreach (KeyValuePair<GeoRegion, int> kv in contributions)
        {
            if (kv.Value > max)
            {
                max = kv.Value;
                tied.Clear();
                tied.Add(kv.Key);
            }
            else if (kv.Value == max)
            {
                tied.Add(kv.Key);
            }
        }

        if (tied.Count <= 1) return tied.Count == 1 ? tied[0] : null;
        return tied[UnityEngine.Random.Range(0, tied.Count)];
    }

    /// <summary>
    /// 合并命名：主导地区已是新档位时沿用其名；否则剥掉其原通名取前缀，加新档位随机通名。
    /// 前缀无法剥离（如自定义名不含通名）时返回 null，交由旧逻辑处理。
    /// </summary>
    private string BuildMergedName(GeoRegionLibrary lib, GeoRegionAsset newCategory, GeoRegion dominant)
    {
        GeoRegionData domData = dominant.data;
        if (domData == null || string.IsNullOrEmpty(domData.name)) return null;

        if (string.Equals(domData.CategoryId, newCategory.id, StringComparison.Ordinal))
        {
            return domData.name;
        }

        string prefix = StripTypeSuffix(domData.name, AllTypeWords);
        if (string.IsNullOrEmpty(prefix)) return null;

        string[] words = GetTypeWords(lib, newCategory);
        if (words == null || words.Length == 0) return null;
        return prefix + words[UnityEngine.Random.Range(0, words.Length)];
    }

    /// <summary>
    /// 从名字尾部剥掉通名（按长度降序匹配，两字通名如“大陆”优先）。
    /// </summary>
    private static string StripTypeSuffix(string name, string[] typeWords)
    {
        foreach (string word in typeWords)
        {
            if (word.Length > 0 && name.Length > word.Length && name.EndsWith(word, StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - word.Length);
            }
        }

        return null;
    }

    private static string[] GetTypeWords(GeoRegionLibrary lib, GeoRegionAsset category)
    {
        if (ReferenceEquals(category, lib.LandmassMainland)) return MainlandTypeWords;
        if (ReferenceEquals(category, lib.LandmassContinent)) return ContinentTypeWords;
        return IslandTypeWords;
    }

    /// <summary>
    /// 连通陆地低于岛的最低阈值（默认 21 格）时丢弃地区：
    /// 先把它在成员索引中的全部瓦片解绑（含已被挖成水的瓦片），再删除地区对象，
    /// 使碎块不参与命名与遮罩显示，与分区器“小岛不生成地区”的行为一致。
    /// </summary>
    private void DiscardRegion(GeoRegion region, int count)
    {
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager != null && manager.IsMembershipReady && region?.data != null)
        {
            List<int> ids = new List<int>(manager.GetRegionTileIds(region));
            for (int i = 0; i < ids.Count; i++)
            {
                int tileId = ids[i];
                if ((uint)tileId >= (uint)_tiles.Length) continue;
                manager.RemoveTileFromRegion(_tiles[tileId], GeoRegionLayer.Landmass);
            }
        }

        for (int i = 0; i < count; i++)
        {
            _visited[_queue[i]] = false;
        }
        _queue.Clear();
        _head = 0;
        _tail = 0;

        if (region == null || region.isRekt()) return;
        ModClass.LogInfo($"[GeoRegion] 陆块面积过小({count} 格)丢弃: id={region.getID()}");
        manager?.removeObject(region);
    }

    private static string GenerateFallbackName(GeoRegionAsset category, int centerX, int centerY)
    {
        try
        {
            return category.GenerateName(centerX, centerY, MapBox.width, MapBox.height);
        }
        catch (Exception e)
        {
            ModClass.LogError($"[GeoRegion] 重新生成地区名称失败，使用分类显示名: {e.Message}");
            return category.GetDisplayName();
        }
    }

    /// <summary>
    /// 与分区器 BuildBaseArrays 的 isLand 判定保持一致：
    /// 仅 Block/Ground 层或 tile 带 block 标记视为陆地（不包含 mountains/edge_mountains 标记）。
    /// </summary>
    private static bool IsLandTile(WorldTile tile)
    {
        TileTypeBase tileType = tile.Type;
        if (tileType == null) return false;

        TileLayerType layerType = tileType.layer_type;
        var isLava = layerType == TileLayerType.Lava || tileType.lava;
        var isGoo = layerType == TileLayerType.Goo || tileType.grey_goo;
        if ((layerType == TileLayerType.Ocean || tileType.ocean) && !isLava && !isGoo) return false;
        if (isLava) return false;
        if (isGoo) return false;

        return layerType == TileLayerType.Ground ||
               layerType == TileLayerType.Block ||
               tileType.block;
    }
}
