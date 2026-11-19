using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cultiway.Content.Territories;

/// <summary>管理绘制、村庄名额和扩张边界，只在编辑及领地变更时工作。</summary>
internal static class TerritoryService
{
    /// <summary>世界自定义数据中的保存键。</summary>
    internal const string SaveKey = "cw.territories";
    /// <summary>地区名称允许的最大字符数。</summary>
    internal const int MaxNameLength = 40;
    /// <summary>当前数据所属的世界，原版读档会替换这个对象。</summary>
    private static MapStats world;
    /// <summary>当前世界的地区数据。</summary>
    private static TerritorySaveData data = new();
    /// <summary>按原版格子编号查找定制地区。</summary>
    private static CustomTerritory[] byZone = Array.Empty<CustomTerritory>();
    /// <summary>按村庄编号查找唯一所属地区。</summary>
    private static readonly Dictionary<long, CustomTerritory> byCity = new();
    /// <summary>同一次笔刷批量修改后只发布一次显示数据。</summary>
    private static bool displayDirty;
    /// <summary>正在绘制的地区。</summary>
    internal static CustomTerritory Selected { get; private set; }
    /// <summary>供名称图层读取当前世界的地区。</summary>
    internal static IReadOnlyList<CustomTerritory> Regions => data.Regions;

    /// <summary>清理换世界前的引用，不改动地图。</summary>
    internal static void Clear()
    {
        world = null;
        data = new TerritorySaveData();
        byZone = Array.Empty<CustomTerritory>();
        byCity.Clear();
        Selected = null;
        displayDirty = false;
        TerritoryMapOverlay.Clear();
    }

    /// <summary>首次访问当前世界时读取范围并建立索引。</summary>
    private static void EnsureWorld()
    {
        if (world == World.world.map_stats) return;
        Clear();
        world = World.world.map_stats;
        byZone = new CustomTerritory[World.world.zone_calculator.zones.Count];
        string json = null;
        world.custom_data?.get(SaveKey, out json, null);
        if (json != null) Restore(JsonConvert.DeserializeObject<TerritorySaveData>(json));
        TerritoryMapOverlay.Publish(data.Regions, Selected, byZone.Length);
    }

    /// <summary>世界加载结束后恢复颜色，使暂停状态下打开地图也能立即看见地区。</summary>
    internal static void Load() => EnsureWorld();

    /// <summary>在反序列化边界检查编号、格子和一村一地关系。</summary>
    /// <param name="saved">从当前世界读出的记录。</param>
    private static void Restore(TerritorySaveData saved)
    {
        if (saved?.Regions == null || saved.NextId < 1)
            throw new InvalidDataException("定制地区存档缺少区域或编号。");
        var ids = new HashSet<int>();
        foreach (CustomTerritory region in saved.Regions)
        {
            if (region == null || region.Id <= 0 || region.Id >= saved.NextId || !ids.Add(region.Id)
                || region.CityId < 0 || region.Zones == null || region.Zones.Count == 0
                || !string.IsNullOrEmpty(region.Name) && !IsValidName(region.Name))
                throw new InvalidDataException("定制地区存档中的区域编号或范围无效。");
            foreach (int zone in region.Zones)
            {
                if ((uint)zone >= (uint)byZone.Length || byZone[zone] != null)
                    throw new InvalidDataException("定制地区存档中存在越界或重叠格子。");
                byZone[zone] = region;
            }
            if (region.CityId == 0) continue;
            if (byCity.ContainsKey(region.CityId))
                throw new InvalidDataException("定制地区存档中一个村庄属于多块地区。");
            byCity.Add(region.CityId, region);
        }
        data = saved;
    }

    /// <summary>原版开始保存世界时写入一次，不在连续绘制时反复序列化。</summary>
    internal static void Save()
    {
        EnsureWorld();
        foreach (CustomTerritory region in data.Regions) GetOwner(region);
        world.custom_data ??= new SaveCustomData();
        world.custom_data.set(SaveKey, JsonConvert.SerializeObject(data));
    }

    /// <summary>按原版格子查找定制地区。</summary>
    /// <param name="zone">当前世界中的格子。</param>
    /// <returns>未划定时为空。</returns>
    internal static CustomTerritory Get(TileZone zone)
    {
        EnsureWorld();
        return byZone[zone.id];
    }

    /// <summary>取得所属村庄，灭亡后自动释放名额。</summary>
    /// <param name="region">要检查的定制地区。</param>
    /// <returns>尚无村庄或原村庄灭亡时为空。</returns>
    internal static City GetOwner(CustomTerritory region)
    {
        if (region.CityId == 0) return null;
        City city = World.world.cities.get(region.CityId);
        if (city != null && city.isAlive()) return city;
        byCity.Remove(region.CityId);
        region.CityId = 0;
        return null;
    }

    /// <summary>允许空闲定制地区建立第一座村庄，未划定区域沿用原版。</summary>
    /// <param name="zone">候选建村格子。</param>
    /// <returns>定制规则是否允许建村。</returns>
    internal static bool CanFound(TileZone zone)
    {
        CustomTerritory region = Get(zone);
        return region == null || GetOwner(region) == null;
    }

    /// <summary>已绑定村庄只能在本块内扩张，外面的村庄不能吞并定制地区。</summary>
    /// <param name="city">要领取领地的村庄。</param>
    /// <param name="zone">候选领地。</param>
    /// <returns>是否允许领取。</returns>
    internal static bool CanClaim(City city, TileZone zone)
    {
        CustomTerritory target = Get(zone);
        if (byCity.TryGetValue(city.id, out CustomTerritory own)) return target == own;
        if (target == null) return true;
        return GetOwner(target) == city || target.CityId == 0 && city.zones.Count == 0;
    }

    /// <summary>在原版修改村庄列表前检查并占用第一格，阻止初始相邻八格越界。</summary>
    /// <param name="city">原版已创建的村庄。</param>
    /// <param name="zone">即将加入的格子。</param>
    /// <returns>原版是否继续添加。</returns>
    internal static bool PrepareClaim(City city, TileZone zone)
    {
        if (!CanClaim(city, zone)) return false;
        CustomTerritory region = byZone[zone.id];
        if (region != null && region.CityId == 0) Bind(region, city);
        return true;
    }

    /// <summary>在空地开始新区域，已有村庄则以其全部现有领地开始。</summary>
    /// <param name="zone">玩家点击的格子。</param>
    /// <returns>失败提示的本地化后缀，成功为空。</returns>
    internal static string Create(TileZone zone)
    {
        if (Get(zone) != null) return "Overlap";
        var region = new CustomTerritory { Id = data.NextId };
        string error = CheckCity(region, zone.city);
        if (error != null) return error;
        data.NextId++;
        data.Regions.Add(region);
        Selected = region;
        ApplyPaint(region, zone);
        return null;
    }

    /// <summary>选择一个已有区域，不改变范围。</summary>
    /// <param name="zone">玩家点击的格子。</param>
    /// <returns>选中失败时的提示后缀。</returns>
    internal static string Select(TileZone zone)
    {
        CustomTerritory region = Get(zone);
        if (region == null) return "NoRegion";
        Selected = region;
        TerritoryMapOverlay.Publish(data.Regions, Selected, byZone.Length);
        return null;
    }

    /// <summary>显示玩家名称，未命名地区使用本地化编号。</summary>
    /// <param name="region">要显示的地区。</param>
    /// <returns>当前语言中的显示名称。</returns>
    internal static string GetName(CustomTerritory region) => string.IsNullOrEmpty(region.Name)
        ? string.Format(LocalizedTextManager.getText("Cultiway.Territories.DefaultName"), region.Id)
        : region.Name;

    /// <summary>修改打开编辑窗口时指定的地区，换世界或删除后拒绝旧操作。</summary>
    /// <param name="region">本次编辑的地区。</param>
    /// <param name="name">玩家提交的新名称。</param>
    /// <returns>失败提示的后缀，成功为空。</returns>
    internal static string Rename(CustomTerritory region, string name)
    {
        EnsureWorld();
        if (region == null || !data.Regions.Contains(region)) return "NoRegion";
        name = name?.Trim();
        if (!IsValidName(name)) return "InvalidName";
        region.Name = name;
        TerritoryMapOverlay.Publish(data.Regions, Selected, byZone.Length);
        return null;
    }

    /// <summary>名称只允许非空单行文本。</summary>
    /// <param name="name">要验证的名称。</param>
    /// <returns>是否可以保存和显示。</returns>
    private static bool IsValidName(string name) => !string.IsNullOrWhiteSpace(name)
        && name.Length <= MaxNameLength && !name.Any(char.IsControl);

    /// <summary>扩展当前区域；先检查整个村庄，保证冲突时不产生部分修改。</summary>
    /// <param name="zone">笔刷经过的格子。</param>
    /// <returns>失败提示的后缀，成功为空。</returns>
    internal static string Paint(TileZone zone)
    {
        CustomTerritory existing = Get(zone);
        if (Selected == null) return "ChooseRegion";
        if (existing == Selected) return null;
        if (existing != null) return "Overlap";
        string error = CheckCity(Selected, zone.city);
        if (error != null) return error;
        ApplyPaint(Selected, zone);
        return null;
    }

    /// <summary>检查是否会圈入第二座村庄，或把一个村庄分配到两块地区。</summary>
    /// <param name="region">正在扩展的区域。</param>
    /// <param name="city">笔刷圈入的现有村庄。</param>
    /// <returns>冲突提示，允许时为空。</returns>
    private static string CheckCity(CustomTerritory region, City city)
    {
        if (city == null) return null;
        City owner = GetOwner(region);
        if (owner != null && owner != city) return "SecondCity";
        if (byCity.TryGetValue(city.id, out CustomTerritory previous) && previous != region) return "CityAssigned";
        return city.zones.Any(zone => byZone[zone.id] != null && byZone[zone.id] != region) ? "Overlap" : null;
    }

    /// <summary>检查完成后一次写入村庄旧领地与笔刷格子，再发布显示。</summary>
    /// <param name="region">接收格子的区域。</param>
    /// <param name="zone">本次笔刷格子。</param>
    private static void ApplyPaint(CustomTerritory region, TileZone zone)
    {
        if (zone.city != null)
        {
            Bind(region, zone.city);
            foreach (TileZone existing in zone.city.zones)
            {
                region.Zones.Add(existing.id);
                byZone[existing.id] = region;
            }
        }
        region.Zones.Add(zone.id);
        byZone[zone.id] = region;
        Changed();
    }

    /// <summary>记录唯一村庄归属。</summary>
    /// <param name="region">所属地区。</param>
    /// <param name="city">所属村庄。</param>
    private static void Bind(CustomTerritory region, City city)
    {
        region.CityId = city.id;
        byCity[city.id] = region;
    }

    /// <summary>仅擦除当前区域中尚未被村庄占用的部分。</summary>
    /// <param name="zone">笔刷经过的格子。</param>
    /// <returns>无法擦除时的提示后缀。</returns>
    internal static string Erase(TileZone zone)
    {
        CustomTerritory region = Get(zone);
        if (region == null) return null;
        if (region != Selected) return "ChooseRegion";
        if (zone.city != null) return "Occupied";
        region.Zones.Remove(zone.id);
        byZone[zone.id] = null;
        if (region.Zones.Count == 0) Remove(region);
        Changed();
        return null;
    }

    /// <summary>删除整块定制范围，保留村庄与地形。</summary>
    /// <param name="zone">玩家明确点击的区域。</param>
    /// <returns>未点中区域时的提示后缀。</returns>
    internal static string Delete(TileZone zone)
    {
        CustomTerritory region = Get(zone);
        if (region == null) return "NoRegion";
        foreach (int id in region.Zones) byZone[id] = null;
        Remove(region);
        Changed();
        return null;
    }

    /// <summary>从区域列表与村庄索引移除一块地区。</summary>
    /// <param name="region">即将删除的地区。</param>
    private static void Remove(CustomTerritory region)
    {
        byCity.Remove(region.CityId);
        data.Regions.Remove(region);
        if (Selected == region) Selected = null;
    }

    /// <summary>范围变更后更新建村候选和地图快照。</summary>
    private static void Changed()
    {
        displayDirty = true;
    }

    /// <summary>原版处理完一次完整笔刷后更新建村候选和地图，避免每个地块都分配数组。</summary>
    internal static void FlushDisplay()
    {
        if (!displayDirty) return;
        displayDirty = false;
        World.world.city_zone_helper.city_place_finder.setDirty();
        TerritoryMapOverlay.Publish(data.Regions, Selected, byZone.Length);
    }
}
