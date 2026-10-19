using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cultiway.Core.Persistence;

namespace Cultiway.Content.SpiritVeins;

/// <summary>把已有龙脉状态随世界保存，读取不重新生成或补满灵气。</summary>
public sealed partial class SpiritVeinManager
{
    /// <summary>原版资料排除空容器，保持原版编号、名称和颜色属性。</summary>
    private static readonly JsonSerializer NativeJson = JsonSerializer.Create(StableSaveJson.NativeSettings);
    /// <summary>保存前完成已经开始的地势重流，等待中的长期变化仍按原月份保留。</summary>
    internal SpiritVeinWorldSaveData CaptureWorldSave()
    {
        if (!IsReady) return null;
        if (reflowTask != null)
        {
            reflowTask.GetAwaiter().GetResult();
            UpdateRerouteTask();
        }
        return new SpiritVeinWorldSaveData
        {
            Width = width, Height = height, NextId = nextId,
            Veins = veins.Select(vein => JObject.FromObject(vein.data, NativeJson)).ToList(),
            Branches = branches.Select(SpiritVeinBranchSaveData.Capture).ToList(),
            Sections = sections.Select(SpiritVeinSectionSaveData.Capture).ToList(),
            Grounds = grounds.Select(GatheringGroundSaveData.Capture).ToList(),
            Eyes = eyes.Select(SpiritVeinEyeSaveData.Capture).ToList(),
            Field = SpiritVeinFieldSaveData.Capture(field), PendingTiles = pendingTerrainTileIds.ToArray(),
            PendingMonths = pendingTerrainMonths, SupplyCursor = supplyCursor
        };
    }

    /// <summary>核对整张布局后重建身份、查询索引和运行资源。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    internal void RestoreWorldSave(SpiritVeinWorldSaveData data)
    {
        int count = checked(MapBox.width * MapBox.height);
        if (data.Width != MapBox.width || data.Height != MapBox.height || data.Field == null ||
            data.Field.Width != data.Width || data.Field.Height != data.Height || data.NextId <= 0 ||
            data.Field.PrimaryVeinByTile?.Length != count || data.Field.SecondaryVeinByTile?.Length != count ||
            data.Field.SectionByTile?.Length != count || data.Field.SecondarySectionByTile?.Length != count ||
            data.Field.GroundByTile?.Length != count || data.Field.FieldStrength?.Length != count ||
            data.Field.SecondaryStrength?.Length != count || data.Field.FlowX?.Length != count ||
            data.Field.FlowY?.Length != count || data.Field.Convergence?.Length != count ||
            data.Field.Shelter?.Length != count || data.Field.Leakage?.Length != count)
            throw new InvalidDataException("龙脉地图尺寸或资料不完整");
        var veinData = data.Veins.Select(entry => entry.ToObject<SpiritVeinData>(NativeJson)).ToList();
        if (veinData.Any(vein => vein.id < 0) || veinData.Select(vein => vein.id).Distinct().Count() != veinData.Count ||
            data.Sections.Select(section => section.Id).Distinct().Count() != data.Sections.Count ||
            data.Sections.Any(section => section.TileIds == null || section.TileIds.Any(tile => (uint)tile >= (uint)count) ||
                section.CurrentAmount < 0 || float.IsNaN(section.CurrentAmount)))
            throw new InvalidDataException("龙脉编号、地块或储量无效");
        // 所有对象先在局部构造，检查失败时不会留下半套查询表。
        var restoredBranches = data.Branches.Select(entry => entry.Restore()).ToList();
        var restoredSections = data.Sections.Select(entry => entry.Restore()).ToList();
        var restoredGrounds = data.Grounds.Select(entry => entry.Restore()).ToList();
        var restoredEyes = data.Eyes.Select(entry => entry.Restore()).ToList();
        SpiritVeinFieldSnapshot restoredField = data.Field.Restore();
        clear();
        loadFromSave(veinData);
        veins.AddRange(veinData.Select(vein => get(vein.id)));
        branches.AddRange(restoredBranches);
        sections.AddRange(restoredSections);
        grounds.AddRange(restoredGrounds);
        eyes.AddRange(restoredEyes);
        field = restoredField;
        worldSeedId = MapBox.current_world_seed_id;
        width = data.Width;
        height = data.Height;
        terrain = SpiritVeinTerrainSnapshot.CaptureCurrentWorld(worldSeedId, width, height);
        nextId = Math.Max(data.NextId, veinData.Select(vein => vein.id).DefaultIfEmpty(0).Max() + 1);
        pendingTerrainTileIds.UnionWith(data.PendingTiles ?? Array.Empty<int>());
        pendingTerrainMonths = data.PendingMonths;
        RebuildDictionaries();
        RebuildFieldTileIds();
        supplyCursor = fieldTileIds.Length == 0 ? 0 : data.SupplyCursor % fieldTileIds.Length;
        ready = true;
        topologyRevision = NextRevision(topologyRevision);
        displayRevision = NextRevision(displayRevision);
        ModClass.I.TileExtendManager.CompleteWorldInitialization(World.world.tiles_list);
    }
}
