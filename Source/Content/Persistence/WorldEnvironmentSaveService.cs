using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Persistence;
using Cultiway.Utils.Extension;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Persistence;

/// <summary>保存宗门、地区与世界灵气，派生索引在读取时重新建立。</summary>
internal static class WorldEnvironmentSaveService
{
    /// <summary>原版资料使用原版的字段与属性规则，保留编号、名称和历史资料。</summary>
    private static readonly JsonSerializer NativeJson = JsonSerializer.Create(StableSaveJson.NativeSettings);

    /// <summary>登记世界中独立升级的部分。</summary>
    internal static void Initialize()
    {
        var world = ModClass.I.WorldSaves.World;
        world.Register(new SaveSection<MapBox, WorldHistorySaveData>("world-history", "世界历史与灵气潮汐", 1,
            _ => new WorldHistorySaveData
            {
                Keys = ModClass.I.WorldRecord.CaptureKeys(),
                Tide = ModClass.I.WorldRecord.E.TryGetComponent(out WakanTideStatus tide) ? tide : null
            },
            (_, data) =>
            {
                ModClass.I.WorldRecord.RestoreKeys(data.Keys);
                if (data.Tide.HasValue) ModClass.I.WorldRecord.E.AddComponent(data.Tide.Value);
            }) { BeforeActors = true });
        world.Register(new SaveSection<MapBox, KnightFamilySaveData>("knight-family", "骑士血统谱系", 1,
            _ => new KnightFamilySaveData { Parents = KnightBloodline.CaptureWorldSave() },
            (_, data) => KnightBloodline.RestoreWorldSave(data.Parents)) { BeforeActors = true });
        world.Register(new SaveSection<MapBox, YaoBeasts.YaoDigestionService.WorldSaveState>("yao-essence", "尸体精华", 1,
            _ => YaoBeasts.YaoDigestionService.CaptureWorldSave(), (_, data) => YaoBeasts.YaoDigestionService.RestoreWorldSave(data)));
        world.Register(new SaveSection<MapBox, YaoBeasts.YaoTribulationService.WorldSaveState>("yao-tribulation", "妖兽渡劫记录", 1,
            _ => YaoBeasts.YaoTribulationService.CaptureWorldSave(), (_, data) => YaoBeasts.YaoTribulationService.RestoreWorldSave(data)));
        world.Register(new SaveSection<MapBox, BalefulWindTribulationSkillService.WorldSaveState>("baleful-tribulation", "煞风劫", 1,
            _ => BalefulWindTribulationSkillService.CaptureWorldSave(), (_, data) => BalefulWindTribulationSkillService.RestoreWorldSave(data)));
        world.Register(new SaveSection<MapBox, SectWorldSaveData>("sects", "宗门", 1, CaptureSects, RestoreSects));
        world.Register(new SaveSection<MapBox, GeographyWorldSaveData>("geography", "地理地区", 1, CaptureGeography, RestoreGeography));
        world.Register(new SaveSection<MapBox, SpiritVeins.SpiritVeinWorldSaveData>("spirit-veins", "天地灵脉", 1,
            _ => WorldboxGame.I.SpiritVeins.CaptureWorldSave(), (_, data) => WorldboxGame.I.SpiritVeins.RestoreWorldSave(data)));
        world.Register(new SaveSection<MapBox, WakanWorldSaveData>("wakan", "天地灵气与浊气", 1, CaptureWakan, RestoreWakan, ValidateWakan));
        world.Register(new SaveSection<MapBox, Core.SubWorlds.SubWorldSaveData>("subworlds", "小世界", 1,
            _ => ModClass.I.SubWorldManager.CaptureWorldSave(),
            (_, data) => ModClass.I.SubWorldManager.RestoreWorldSave(data, ModClass.I.WorldSaves.LastReport)));
        world.Register(new SaveSection<MapBox, TrainNetworkSaveData>("railways", "铁路与修复进度", 1,
            _ => TrainTrackRepairSystem.CaptureWorldSave(),
            (_, data) => TrainTrackRepairSystem.RestoreWorldSave(data, ModClass.I.WorldSaves.LastReport)));
        world.Register(new SaveSection<MapBox, TrainTransportSaveData>("train-rides", "列车行程与旅客", 1,
            _ => TrainTransportSystem.CaptureWorldSave(),
            (_, data) => TrainTransportSystem.RestoreWorldSave(data, ModClass.I.WorldSaves.LastReport)));
        CollectiveProjectSaveContent.Initialize();
    }

    /// <summary>只采集当前存活宗门，并保留成员编号。</summary>
    /// <param name="world">当前主世界。</param>
    private static SectWorldSaveData CaptureSects(MapBox world)
    {
        var data = new SectWorldSaveData();
        foreach (Sect sect in WorldboxGame.I.Sects)
        {
            if (!sect.isAlive()) continue;
            sect.save();
            data.Sects.Add(JObject.FromObject(sect.data, NativeJson));
        }
        foreach (Actor actor in world.units.units_only_alive)
            if (actor.TryGetExtend(out var extend) && extend.sect?.isAlive() == true)
                data.Members.Add(actor.data.id, extend.sect.getID());
        return data;
    }

    /// <summary>恢复宗门和成员，不走创立、加入奖励或赠送书册流程。</summary>
    /// <param name="world">当前主世界。</param>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void RestoreSects(MapBox world, SectWorldSaveData data)
    {
        var records = new List<SectData>();
        foreach (JObject entry in data.Sects)
        {
            SectData record = entry.ToObject<SectData>(NativeJson);
            if (record == null || record.id < 0 || records.Any(item => item.id == record.id))
                throw new InvalidDataException("宗门编号缺失或重复");
            records.Add(record);
        }
        WorldboxGame.I.Sects.loadFromSave(records);
        foreach (var entry in data.Members)
        {
            Actor actor = world.units.get(entry.Key);
            Sect sect = WorldboxGame.I.Sects.get(entry.Value);
            if (actor == null || sect == null)
            {
                ModClass.I.WorldSaves.LastReport.Skip("宗门", "成员 " + entry.Key, "人物或宗门未能恢复");
                continue;
            }
            actor.GetExtend().SetSect(sect);
        }
        WorldboxGame.I.Sects.beginChecksUnits();
        WorldboxGame.I.Sects.beginChecksBuildings();
    }

    /// <summary>保存每个地区当前名字、分类和实际包含的地块。</summary>
    /// <param name="world">当前主世界。</param>
    private static GeographyWorldSaveData CaptureGeography(MapBox world)
    {
        WorldGeneratedPartitionGeoRegionsEventSystem.DrainPendingWork();
        var manager = WorldboxGame.I.GeoRegions;
        if (!manager.IsMembershipReady) return null;
        GeoRegionMembershipSnapshot membership = manager.GetMembershipForReconciliation(manager.MembershipRevision);
        var data = new GeographyWorldSaveData { Width = MapBox.width, Height = MapBox.height };
        for (int i = 0; i < membership.RegionCount; i++)
        {
            GeoRegion region = membership.GetRegionBySlot(i);
            data.Regions.Add(new RegionWorldSaveData
            {
                Data = JObject.FromObject(region.data, NativeJson), Tiles = membership.GetTileIds(region).ToArray()
            });
        }
        return data;
    }

    /// <summary>核对地块覆盖后安装完整归属表，读取不会重新随机起名。</summary>
    /// <param name="world">当前主世界。</param>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void RestoreGeography(MapBox world, GeographyWorldSaveData data)
    {
        if (data.Width != MapBox.width || data.Height != MapBox.height) throw new InvalidDataException("地区地图尺寸不符");
        int count = checked(world.tiles_list.Length * GeoRegionMembershipSnapshot.LayerCount);
        int[] slots = Enumerable.Repeat(-1, count).ToArray();
        int[] positions = Enumerable.Repeat(-1, count).ToArray();
        var records = new List<GeoRegionData>();
        for (int slot = 0; slot < data.Regions.Count; slot++)
        {
            RegionWorldSaveData entry = data.Regions[slot];
            GeoRegionData record = entry.Data.ToObject<GeoRegionData>(NativeJson);
            if (record == null || record.id < 0 || records.Any(item => item.id == record.id) ||
                (uint)record.Layer >= GeoRegionMembershipSnapshot.LayerCount || ModClass.L.GeoRegionLibrary.getSimple(record.CategoryId) == null)
                throw new InvalidDataException("地区编号、层级或分类无效");
            records.Add(record);
            for (int position = 0; position < entry.Tiles.Length; position++)
            {
                int tile = entry.Tiles[position];
                if ((uint)tile >= (uint)world.tiles_list.Length) throw new InvalidDataException("地区地块越界");
                int index = tile * GeoRegionMembershipSnapshot.LayerCount + (int)record.Layer;
                if (slots[index] != -1) throw new InvalidDataException("同层地区地块重复");
                slots[index] = slot;
                positions[index] = position;
            }
        }
        WorldGeneratedPartitionGeoRegionsEventSystem.PrepareSavedMembership();
        GeoRegionManager manager = WorldboxGame.I.GeoRegions;
        manager.clear();
        manager.loadFromSave(records);
        var entries = records.Select((record, index) => new GeoRegionMembershipEntry(manager.get(record.id), record.Layer, data.Regions[index].Tiles)).ToList();
        manager.InstallInitialMembership(new GeoRegionMembershipSnapshot(1, world.tiles_list, slots, positions, entries));
        EventSystemHub.Publish(new GeoRegionsReadyEvent { WorldSeedId = MapBox.current_world_seed_id, Width = MapBox.width, Height = MapBox.height });
    }

    /// <summary>复制真实数值，地图显示副本不进入存档。</summary>
    /// <param name="world">当前主世界。</param>
    private static WakanWorldSaveData CaptureWakan(MapBox world)
        => !WorldWakanService.IsInitialized ? null : new WakanWorldSaveData
        {
            Width = WorldWakanService.Width, Height = WorldWakanService.Height,
            Clean = Enumerable.Range(0, WorldWakanService.TileCount).Select(index => WorldWakanService.GetClean(index)).ToArray(),
            Dirty = Enumerable.Range(0, WorldWakanService.TileCount).Select(index => WorldWakanService.GetDirty(index)).ToArray()
        };

    /// <summary>整张资源图核对成功后一次替换。</summary>
    /// <param name="world">当前主世界。</param>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void RestoreWakan(MapBox world, WakanWorldSaveData data)
    {
        WorldWakanService.InitializeWorld(data.Width, data.Height, 0);
        for (int i = 0; i < data.Clean.Length; i++)
        {
            WorldWakanService.SetClean(i, data.Clean[i]);
            WorldWakanService.SetDirty(i, data.Dirty[i]);
        }
        WorldWakanService.MarkSavedDistribution();
        WorldWakanService.PublishDisplayValues(true);
    }

    /// <summary>外部数据只在载入边界核对尺寸和有效数值。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    private static void ValidateWakan(WakanWorldSaveData data)
    {
        if (data.Width != MapBox.width || data.Height != MapBox.height || data.Clean == null || data.Dirty == null ||
            data.Clean.Length != checked(data.Width * data.Height) || data.Dirty.Length != data.Clean.Length ||
            data.Clean.Concat(data.Dirty).Any(value => value < 0 || float.IsNaN(value) || float.IsInfinity(value)))
            throw new InvalidDataException("天地灵气的尺寸或数值无效");
    }
}

/// <summary>宗门资料和人物归属。</summary>
internal sealed class SectWorldSaveData
{
    /// <summary>原版格式的宗门基础资料。</summary>
    public List<JObject> Sects = new();
    /// <summary>人物编号到宗门编号。</summary>
    public Dictionary<long, long> Members = new();
}

/// <summary>同一世界的地区划分。</summary>
internal sealed class GeographyWorldSaveData
{
    /// <summary>地图宽度。</summary>
    public int Width;
    /// <summary>地图高度。</summary>
    public int Height;
    /// <summary>全部分类层的地区。</summary>
    public List<RegionWorldSaveData> Regions = new();
}

/// <summary>一个地区及其地块编号。</summary>
internal sealed class RegionWorldSaveData
{
    /// <summary>原版格式的基础资料。</summary>
    public JObject Data;
    /// <summary>实际地块编号。</summary>
    public int[] Tiles;
}

/// <summary>天地灵气和浊气真实数值。</summary>
internal sealed class WakanWorldSaveData
{
    /// <summary>地图宽度。</summary>
    public int Width;
    /// <summary>地图高度。</summary>
    public int Height;
    /// <summary>洁净灵气。</summary>
    public float[] Clean;
    /// <summary>浊气。</summary>
    public float[] Dirty;
}

/// <summary>骑士祖先谱系，不随祖先死亡删除。</summary>
internal sealed class KnightFamilySaveData
{
    /// <summary>子代编号到两名父母编号。</summary>
    public Dictionary<long, long[]> Parents = new();
}

/// <summary>世界首次事件与潮汐进度。</summary>
internal sealed class WorldHistorySaveData
{
    /// <summary>已经公告过的首次事件。</summary>
    public string[] Keys;
    /// <summary>当前灵气潮汐和距离切换的时间。</summary>
    public WakanTideStatus? Tide;
}
