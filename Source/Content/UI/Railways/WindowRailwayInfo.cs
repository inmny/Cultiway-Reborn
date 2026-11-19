using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.MapModeVisuals;
using Cultiway.Content.Railways;
using Cultiway.Core.Pathfinding;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Railways;

/// <summary>从列车与城市原有入口查看铁路，提供定位和原版绘轨工具入口。</summary>
public sealed class WindowRailwayInfo : AbstractWideWindow<WindowRailwayInfo>
{
    /// <summary>原版窗口编号。</summary>
    public const string Id = "Cultiway.UI.WindowRailwayInfo";
    /// <summary>窗口尺寸。</summary>
    public static readonly Vector2 WindowSize = new(600, 360);
    /// <summary>已创建的窗口。</summary>
    private static WindowRailwayInfo instance;
    /// <summary>当前列车；其他查看方式为零。</summary>
    private static long actorId;
    /// <summary>当前车站；城市总览为零。</summary>
    private static long stationId;
    /// <summary>当前城市。</summary>
    private static long cityId;
    /// <summary>窗口摘要。</summary>
    private Text heading;
    /// <summary>地图入口。</summary>
    private Button map;
    /// <summary>绘轨快捷入口。</summary>
    private Button paint;
    /// <summary>擦轨快捷入口。</summary>
    private Button erase;
    /// <summary>概况、车辆、货运、线路的固定页签。</summary>
    private UiSegmentedTabs tabs;
    /// <summary>语言切换时更新标签。</summary>
    private readonly Button[] tabButtons = new Button[4];
    /// <summary>当前页签，零为概况。</summary>
    private int page;
    /// <summary>切换页签时回到列表顶部。</summary>
    private ScrollRect scroll;
    /// <summary>线路页顶部的真实轨迹简图。</summary>
    private RailwayRoutePreview routePreview;
    /// <summary>复用条目及回调。</summary>
    private MonoObjPool<RailwayInfoRow> rows;
    /// <summary>失效选择和空城市复用公共空状态控件。</summary>
    private UiEmptyState empty;
    /// <summary>下次动态刷新时刻，关闭时不会扫描世界。</summary>
    private float nextRefresh;

    /// <summary>查看列车。</summary>
    /// <param name="actor">选中的车头。</param>
    public static void Open(Actor actor) { actorId = actor?.id ?? 0; stationId = cityId = 0; instance?.SelectPage(0); ShowWindow(); }

    /// <summary>查看车站。</summary>
    /// <param name="station">选中的车站。</param>
    public static void OpenStation(Building station) { stationId = station?.id ?? 0; actorId = cityId = 0; instance?.SelectPage(0); ShowWindow(); }

    /// <summary>从原版城市详情进入车站列表。</summary>
    /// <param name="city">选中的城市。</param>
    public static void OpenCity(City city) { cityId = city?.id ?? 0; actorId = stationId = 0; instance?.SelectPage(0); ShowWindow(); }

    /// <summary>同一窗口内切换对象也立即刷新。</summary>
    private static void ShowWindow() { ScrollWindow.showWindow(Id); instance?.Refresh(); }

    /// <summary>清世界时释放选中编号和池内回调。</summary>
    internal static void ClearWorldState()
    {
        actorId = stationId = cityId = 0;
        RailwayMapOverlay.Clear();
        if (instance == null) return;
        instance.rows?.Clear();
        instance.routePreview?.Hide();
        if (instance.gameObject.activeInHierarchy) instance.GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>使用现有窗口壳和滚动容器创建一次布局。</summary>
    protected override void Init()
    {
        instance = this;
        var context = UiWindowContext.Bind(BackgroundTransform);
        var m = UiTheme.Current.Metrics;
        var root = UiLayout.Create(BackgroundTransform, "RailwayRoot", false, 520, 318, m.SpacingSm);
        root.transform.localPosition = new Vector3(0, -8);
        var toolbar = UiLayout.Create(root.transform, "Toolbar", true, 520, m.ControlMedium, m.SpacingSm);
        heading = UiElements.CreateText(toolbar.transform, "Heading", "", 250, m.ControlMedium, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        paint = UiElements.CreateButton(toolbar.transform, "Paint", T("Draw"), 56, m.ControlSmall, () => SelectTool(false));
        erase = UiElements.CreateButton(toolbar.transform, "Erase", T("Erase"), 56, m.ControlSmall, () => SelectTool(true));
        UiTooltip.Set(paint.gameObject, "cultiway_draw_rails", "cultiway_draw_rails_description");
        UiTooltip.Set(erase.gameObject, "cultiway_erase_rails", "cultiway_erase_rails_description");
        map = UiElements.CreateIconTextButton(toolbar.transform, "Map", UiIcons.World, T("Map"), 140, m.ControlSmall, ShowMap);
        UiTooltip.Set(map.gameObject, "Cultiway.Railways.Map", "Cultiway.Railways.MapTip");
        var tabBar = UiLayout.Create(root.transform, "Pages", true, 520, m.ControlSmall, m.SpacingSm);
        tabs = new UiSegmentedTabs();
        string[] keys = { "Overview", "Vehicles", "Freight", "Lines" };
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i] = UiElements.CreateButton(tabBar.transform, "Page" + i, T(keys[i]), 126, m.ControlSmall, () => SelectPage(index));
            tabs.Add(tabButtons[i]);
        }
        tabs.SetSelected(page);
        var pane = UiScrollPane.CreateVertical(root.transform, "RailwayList", 520, 258);
        pane.AttachOriginalScrollbar(context.ScrollbarTemplate); pane.SetWindowFrame(m.SpacingMd);
        scroll = pane.Content.GetComponentInParent<ScrollRect>();
        routePreview = RailwayRoutePreview.Create(pane.Content);
        rows = new MonoObjPool<RailwayInfoRow>(RailwayInfoRow.Prefab, pane.Content, deactive_action: r => r.Unbind());
        empty = new UiEmptyState(pane.Viewport, "", 400, 80);
        empty.SetVisible(false);
    }

    /// <summary>开启时刷新一次。</summary>
    public override void OnNormalEnable() => Refresh();

    /// <summary>活动窗口每秒更新位置、候车和预计时间。</summary>
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh || !Config.game_loaded) return;
        Refresh();
    }

    /// <summary>销毁时清除窗口引用。</summary>
    private void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>先解析选择，再整体绑定列表，失效选择显示空状态。</summary>
    private void Refresh()
    {
        if (rows == null || !Config.game_loaded) return;
        nextRefresh = Time.unscaledTime + 1;
        rows.ResetToStart();
        routePreview.Hide();
        empty.SetVisible(false);
        UiElements.SetButtonLabel(map, T("Map"));
        UiElements.SetButtonLabel(paint, T("Draw")); UiElements.SetButtonLabel(erase, T("Erase"));
        string[] pages = { "Overview", "Vehicles", "Freight", "Lines" };
        for (int i = 0; i < tabButtons.Length; i++) UiElements.SetButtonLabel(tabButtons[i], T(pages[i]));
        tabs.SetSelected(page);
        var actor = World.world.units.get(actorId);
        var station = World.world.buildings.get(stationId);
        var city = World.world.cities.get(cityId);
        heading.text = T("Title");
        map.interactable = station?.asset == Buildings.TrainStation || RailwayFleetService.Get(actor)?.Home > 0;
        if (actor?.asset == Actors.Train && actor.isAlive() && RailwayFleetService.Get(actor) is { } vehicle) ShowTrain(vehicle);
        else if (station?.asset == Buildings.TrainStation) ShowStation(station);
        else if (city != null && city.isAlive())
        {
            heading.text = city.name;
            var stations = World.world.buildings.getSimpleList().Where(b => b.asset == Buildings.TrainStation && b.city == city).ToArray();
            if (stations.Length == 0) ShowEmpty(T("NoStation") + "\n" + T("NoStationTip"));
            if (stations.Length == 1)
            {
                stationId = stations[0].id; map.interactable = true; ShowStation(stations[0]);
            }
            else foreach (var item in stations) Add(StationName(item), item.isNormal() ? T("Ready") : T("Incomplete"),
                () => Locate(item.current_tile), () => OpenStation(item));
        }
        else ShowEmpty(T("InvalidSelection") + "\n" + T("InvalidSelectionTip"));
        rows.ClearUnsed();
    }

    /// <summary>按车头、每节车厢、行程和实际乘客依次展示，不扫描全体居民。</summary>
    /// <param name="vehicle">实际车队记录。</param>
    private void ShowTrain(RailwayVehicle vehicle)
    {
        var actor = vehicle.Actor; var body = vehicle.Body;
        var home = World.world.buildings.get(vehicle.Home);
        var data = TrainTransportSystem.Instance.Capture(actor);
        heading.text = actor.getName();
        if (page == 0)
        {
        Add(T("Ownership"), F("OwnershipValue", World.world.kingdoms.get(vehicle.Country)?.name ?? T("None"),
                CityName(vehicle.City)), () => Locate(actor.current_tile),
            home == null ? null : () => OpenStation(home));
        Add(T("Home"), StationName(home));
        Add(T("Locomotive"), F("HealthValue", actor.getHealth(), actor.getMaxHealth()) + " · " + VehicleState(vehicle, data));
        if (data.Vehicle.StrandedElapsed > 0)
            Add(T("Stranded"), F("StrandedValue", Seconds(Math.Max(0, TrainConfig.StrandedTimeout - data.Vehicle.StrandedElapsed))), state: UiControlState.Warning);
        }
        int passengerIndex = 0, cargoIndex = 0, load = RailwayTradeService.CargoLoad(actor);
        if (page == 1) foreach (var car in body.Cars.Select((kind, index) => new { kind, index }))
        {
            int used;
            if (car.kind == TrainCarKind.Passenger)
            {
                int first = passengerIndex++ * 20;
                used = body.Carrier.getPassengers().Count(p => { p.data.get(TrainPassengerService.SeatKey, out int seat, -1); return seat >= first && seat < first + 20; });
            }
            else { used = Math.Min(40, Math.Max(0, load - cargoIndex++ * 40)); }
            Add(F("CarNumber", car.index + 1, T(car.kind == TrainCarKind.Passenger ? "Passenger" : "Cargo")),
                F("CapacityValue", used, car.kind == TrainCarKind.Passenger ? 20 : 40) +
                (car.kind == TrainCarKind.Cargo ? "\n" + CargoInCar(actor, cargoIndex - 1) : ""));
        }
        if (page == 0 || page == 1) Add(T("PassengerTotal"), F("PassengersValue", body.Carrier.countPassengers(), body.PassengerCapacity,
            data.Stops.Sum(s => s.ToLoad.Length)));
        if (page == 0 || page == 2) Add(T("CargoTotal"), F("CapacityValue", load, body.CargoCapacity));
        var next = data.Stops.FirstOrDefault(s => s.Station != vehicle.Station);
        if (page == 0 || page == 3)
        {
        Add(T("Journey"), F("JourneyValue", vehicle.Station == 0 ? T("BetweenStations") : StationName(World.world.buildings.get(vehicle.Station)),
            StationName(World.world.buildings.get(next?.Station ?? 0)), Seconds(RemainingTime(vehicle, data, false))),
            next == null ? null : () => Locate(World.world.buildings.get(next.Station)?.current_tile));
        }
        if (page == 3)
        {
            var stops = new HashSet<long>(data.Stops.Select(s => s.Station)) { data.CurrentStation, vehicle.Station, vehicle.Home };
            var selected = TrainTrackRepairSystem.InspectLines().Where(l =>
                data.Stops.Count == 0 ? l.StationA == vehicle.Home || l.StationB == vehicle.Home :
                stops.Contains(l.StationA) && stops.Contains(l.StationB)).ToList();
            int[] actual = body.Track.Select(t => t.tile_id).ToArray();
            if (actual.Length > 1 && !selected.Any(l => l.Tiles.SequenceEqual(actual) || l.Tiles.Reverse().SequenceEqual(actual)))
                selected.Add(new TrainLinkSaveData { Tiles = actual, State = body.IsBlocked ? RailwayLinkState.Damaged : RailwayLinkState.Normal });
            var atStation = World.world.buildings.get(vehicle.Station);
            routePreview.Bind(selected, atStation == null ? actor.current_position : (Vector2)RailwayStationService.TrackAnchor(atStation).posV3);
            foreach (var stop in data.Stops)
                Add(StationName(World.world.buildings.get(stop.Station)), F("StopPassengers", stop.ToLoad.Length, stop.ToUnload.Length),
                    () => Locate(World.world.buildings.get(stop.Station)?.current_tile), () => OpenStation(World.world.buildings.get(stop.Station)));
        }
        if (page == 2 && body.CargoCapacity > 0)
        {
            ShowFreight(vehicle);
            if (!vehicle.Freight.Active)
            {
            var target = World.world.buildings.get(vehicle.Trade.Target);
            Add(T("Trade"), F("TradeValue", StationName(target), T(vehicle.Trade.Settled ? "TradeSettled" :
                vehicle.Trade.Visited ? "TradeVisited" : vehicle.Trade.Active ? "TradeOutbound" : "TradeIdle"),
                RailwayTradeService.Gold(actor), Seconds(RemainingTime(vehicle, data, true))));
            }
        }
        if (page == 1) foreach (var person in body.Carrier.getPassengers())
            Add(person.getName(), T("Onboard"), () => Locate(person.current_tile));
    }

    /// <summary>展示实际车站、归属车辆、许可可达站与所有相连工程。</summary>
    /// <param name="station">当前车站。</param>
    private void ShowStation(Building station)
    {
        heading.text = StationName(station);
        var tile = RailwayStationService.GetPassengerTile(station);
        if (page == 0) Add(T("Station"), F("StationValue", station.kingdom?.name ?? T("None"),
            T(station.isNormal() ? "Ready" : "Incomplete"), T(tile == null ? "NoEntrance" : "EntranceReady")),
            () => Locate(tile ?? station.current_tile));
        var fleet = RailwayFleetService.Vehicles.Where(v => v.Home == station.id || v.Station == station.id).ToArray();
        int waiting = PortalManager.SnapshotRequests().Where(r => !r.IsCompleted() && r.PortalType == Portals.TrainStation)
            .SelectMany(r => r.Portals.Where(p => p.PortalBuilding == station).SelectMany(p => p.ToLoad)).Distinct().Count();
        var links = TrainTrackRepairSystem.InspectLines().Where(l => l.StationA == station.id || l.StationB == station.id).ToArray();
        string reason = !station.isNormal() ? "Incomplete" : tile == null ? "NoEntrance" :
            links.All(l => l.State != RailwayLinkState.Normal) ? "NoLine" : fleet.Length == 0 ? "NoVehicle" :
            fleet.All(v => v.Mode == RailwayVehicleMode.Repairing) ? "Repairing" : "Ready";
        if (page == 0)
        {
            Add(T("Demand"), F("DemandValue", waiting, T(reason)));
            Add(T("Vehicles"), F("FleetSummary", fleet.Length, fleet.Sum(v => v.Body.PassengerCapacity), fleet.Sum(v => v.Body.CargoCapacity)));
            Add(T("Entrance"), T("EntranceDescription"), () => Locate(RailwayStationService.TrackAnchor(station)));
        }
        if (page == 2 && station.city != null)
        {
            var level = RailwayFreightService.GetTarget(station.city);
            foreach (string resource in RailwayFreightService.Resources)
                Add(ResourceName(resource), F("FreightCityStock", station.city.getResourcesAmount(resource),
                    RailwayFreightService.TargetAmount(level, resource), RailwayFreightService.Reserved(station.city.id, resource),
                    RailwayFreightService.Incoming(station.city.id, resource)));
        }
        if (page == 1) foreach (var vehicle in fleet)
            Add(vehicle.Actor.getName(), F("VehicleValue", T("Mode." + vehicle.Mode),
                StationName(World.world.buildings.get(vehicle.Station)), vehicle.Body.PassengerCapacity, vehicle.Body.CargoCapacity),
                () => Locate(vehicle.Actor.current_tile), () => Open(vehicle.Actor));
        if (page == 1 && fleet.Length == 0) Add(T("NoVehicle"), T("NoVehicleTip"));
        if (page == 2 && station.city != null)
            foreach (var shipment in RailwayFleetService.Vehicles.Where(v => v.Freight.Active &&
                         (v.Freight.SourceCity == station.city.id || v.Freight.TargetCity == station.city.id)))
                Add(shipment.Actor.getName(), CityName(shipment.Freight.SourceCity) + " → " + CityName(shipment.Freight.TargetCity) +
                    "\n" + T("FreightState." + shipment.Freight.State), open: () => Open(shipment.Actor));
        if (page != 3) return;
        routePreview.Bind(links);
        long country = station.kingdom?.id ?? 0;
        var targets = World.world.buildings.getSimpleList().Where(b => b.asset == Buildings.TrainStation && b != station && b.isNormal())
            .Select(b => new { Station = b, Route = RailwayRouteService.Find(station.id, b.id, country, false,
                RailwayFleetService.PassengerLength(country)) }).Where(t => t.Route != null).OrderBy(t => t.Route.Time).ToArray();
        foreach (var target in targets) Add(T("Reachable") + " · " + StationName(target.Station), Seconds(target.Route.Time),
            () => Locate(target.Station.current_tile), () => OpenStation(target.Station));
        if (targets.Length == 0) Add(T("NoReachable"), T("NoReachableTip"));
        foreach (var link in links)
        {
            var a = World.world.buildings.get(link.StationA); var b = World.world.buildings.get(link.StationB);
            var inside = new HashSet<int>(link.Tiles.Skip(1).Take(Math.Max(0, link.Tiles.Length - 2)));
            int occupied = RailwayFleetService.Vehicles.Count(v => v.Station == 0 && v.Body.Track.Any(t => inside.Contains(t.tile_id)));
            var bad = link.Tiles.Select(id => World.world.tiles_list[id]).FirstOrDefault(t => !ActorComponents.Train.IsTrackTile(t));
            Add(StationName(a) + " ↔ " + StationName(b), T(link.PlayerDrawn ? "DrawnLine" : "CityLine") + " · " + F("LineValue", T("Line." + link.State), Reason(link.Reason),
                    Math.Max(0, link.Tiles.Length - 1), Seconds((link.Tiles.Length - 1) / TrainConfig.TrackSpeed),
                    occupied, link.Cursor, link.Tiles.Length), () => Locate(bad ?? a?.current_tile ?? b?.current_tile),
                state: link.State == RailwayLinkState.Normal ? UiControlState.Success :
                    link.State == RailwayLinkState.Damaged ? UiControlState.Error : UiControlState.Warning);
        }
        Add(T("Legend"), T("LegendValue"));
    }

    /// <summary>展示预留与背包的区别、实际来源目标和最近一次入库，不把预留显示成已装货。</summary>
    /// <param name="vehicle">实际车辆。</param>
    private void ShowFreight(RailwayVehicle vehicle)
    {
        var data = vehicle.Freight;
        Add(T("Freight"), T("FreightState." + data.State) + " · " + T("FreightReason." + data.Reason));
        if (data.State == RailwayFreightState.None) return;
        Add(T("FreightSource"), CityName(data.SourceCity));
        Add(T("FreightTarget"), CityName(data.TargetCity));
        Add(T("FreightReceived"), F("FreightReceivedValue", data.Unloaded,
            CityName(data.ReceivedCity)));
        foreach (var amount in data.Amounts)
            Add(ResourceName(amount.Key), F("FreightAmountValue",
                data.State == RailwayFreightState.Reserved ? amount.Value : 0,
                Math.Min(amount.Value, vehicle.Actor.inventory.getResource(amount.Key))));
    }

    /// <summary>按材料编号稳定排列背包，依次填满实际货厢；读档后同一背包得到同一分配。</summary>
    /// <param name="actor">实际车头。</param>
    /// <param name="index">货厢序号，不包含客厢。</param>
    private static string CargoInCar(Actor actor, int index)
    {
        if (!actor.inventory.hasResources()) return T("CargoEmpty");
        int skip = index * ActorComponents.Train.CargoPerCar, free = ActorComponents.Train.CargoPerCar;
        var parts = new List<string>();
        foreach (var resource in actor.inventory.getResources().OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            int ignored = Math.Min(skip, resource.Value.amount); skip -= ignored;
            int amount = Math.Min(free, resource.Value.amount - ignored);
            if (amount <= 0) continue;
            parts.Add(ResourceName(resource.Key) + " " + amount); free -= amount;
            if (free == 0) break;
        }
        return parts.Count == 0 ? T("CargoEmpty") : string.Join(" · ", parts);
    }

    /// <summary>材料名称沿用原版当前语言。</summary>
    /// <param name="resource">原版资源编号。</param>
    private static string ResourceName(string resource)
        => LocalizedTextManager.getText(AssetManager.resources.get(resource).getLocaleID());

    /// <summary>当前状态同时说明区间等待和受损停驶。</summary>
    /// <param name="vehicle">实际列车。</param>
    /// <param name="data">同一时刻的行程快照。</param>
    private static string VehicleState(RailwayVehicle vehicle, TrainSaveData data)
        => vehicle.Body.IsBlocked ? T("Blocked") : data.QueueOrder > 0 ? T("NextOccupied") :
            T("Mode." + vehicle.Mode) + (data.Stops.Count == 0 ? "" : " · " + T(data.AtStation ? "Waiting" : "Travelling"));

    /// <summary>估计剩余行驶与基础停站时间，额外排队不计入估计。</summary>
    /// <param name="vehicle">实际列车。</param>
    /// <param name="data">当前行程。</param>
    /// <param name="home">是否估计到归属站。</param>
    private static float RemainingTime(RailwayVehicle vehicle, TrainSaveData data, bool home)
    {
        if (data.Stops.Count == 0) return vehicle.Station == vehicle.Home || !home ? 0 : float.PositiveInfinity;
        float time = data.AtStation ? data.BaseRemaining : Math.Max(0, vehicle.Body.Track.Count - 1 +
            vehicle.Body.TrainLength - vehicle.Body.Distance) / TrainConfig.TrackSpeed;
        long previous = data.Stops[0].Station;
        int end = home ? data.Stops.Count : Math.Min(data.Stops.Count, data.AtStation ? 2 : 1);
        for (int i = 1; i < end; i++)
        {
            var route = RailwayRouteService.Find(previous, data.Stops[i].Station, vehicle.Country, data.Military, vehicle.Body.TrainLength);
            if (route == null) return float.PositiveInfinity;
            time += route.Time + TrainConfig.StopBaseWait; previous = data.Stops[i].Station;
        }
        if (home && previous != vehicle.Home)
        {
            var route = RailwayRouteService.Find(previous, vehicle.Home, vehicle.Country, false, vehicle.Body.TrainLength);
            if (route == null) return float.PositiveInfinity;
            time += route.Time + TrainConfig.StopBaseWait;
        }
        return time;
    }

    /// <summary>从静态快照绑定一行。</summary>
    /// <param name="title">标题。</param>
    /// <param name="detail">正文。</param>
    /// <param name="locate">定位动作。</param>
    /// <param name="open">查看动作。</param>
    /// <param name="state">状态。</param>
    private void Add(string title, string detail, Action locate = null, Action open = null, UiControlState state = UiControlState.Normal)
        => rows.GetNext().Bind(title, detail, locate, open, state);

    /// <summary>显示无可查看对象的说明，保持与正常列表相同的留白。</summary>
    /// <param name="message">当前空状态。</param>
    private void ShowEmpty(string message) { empty.Text.text = message; empty.SetVisible(true); }

    /// <summary>切换固定页面并回到列表顶部，刷新只绑定当前页的数据。</summary>
    /// <param name="index">概况、车辆、货运或线路。</param>
    private void SelectPage(int index)
    {
        page = index;
        tabs?.SetSelected(index);
        if (scroll != null) scroll.verticalNormalizedPosition = 1;
        Refresh();
    }

    /// <summary>关闭详情并选择底栏的真实铁路笔刷。</summary>
    /// <param name="remove">是否擦除轨道。</param>
    private void SelectTool(bool remove)
    {
        GetComponent<ScrollWindow>().clickHide();
        GodPowers.SelectRailwayTool(remove);
    }

    /// <summary>打开选中站点所在的网络并留出地图视野。</summary>
    private void ShowMap()
    {
        long selected = stationId;
        if (selected == 0) selected = RailwayFleetService.Get(World.world.units.get(actorId))?.Home ?? 0;
        if (selected <= 0) return;
        RailwayMapOverlay.Select(selected);
        GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>关闭详情后使用原版镜头定位。</summary>
    /// <param name="tile">实际地块；目标已失效时不移动。</param>
    private static void Locate(WorldTile tile)
    {
        if (tile == null) return;
        instance.GetComponent<ScrollWindow>().clickHide();
        MoveCamera.instance.focusOn(tile.posV3);
    }

    /// <summary>使用城市名称表达车站，不暴露内部编号。</summary>
    /// <param name="station">实际建筑。</param>
    internal static string StationName(Building station) => station == null ? T("None") :
        F("StationName", CityName(station.city?.id ?? 0));

    /// <summary>限制长城市名在列表中的占位，给状态和下一站留出空间。</summary>
    /// <param name="id">实际城市编号。</param>
    private static string CityName(long id)
    {
        string name = World.world.cities.get(id)?.name ?? T("None");
        return name.Length > 24 ? name.Substring(0, 23) + "…" : name;
    }

    /// <summary>将施工原因映射到本地化键。</summary>
    /// <param name="reason">工程记录的明确原因。</param>
    private static string Reason(string reason) => T(reason switch
    {
        "通行许可" => "NoPermission", "地形受阻" => "TerrainBlocked", "轨道受损" => "Blocked",
        "建设资源不足" => "NoResources", "车站失效" => "StationGone", "工程撤销" => "Revoked",
        "玩家拆除" => "PlayerErased", _ => "NoIssue"
    });

    /// <summary>以模拟秒显示近似耗时。</summary>
    /// <param name="value">预计秒数。</param>
    private static string Seconds(float value) => float.IsInfinity(value) ? T("NoRoute") : F("Seconds", Math.Ceiling(value));
    /// <summary>读取铁路文本。</summary>
    /// <param name="key">铁路键后缀。</param>
    internal static string T(string key) => LocalizedTextManager.getText("Cultiway.Railways." + key);
    /// <summary>格式化铁路文本。</summary>
    /// <param name="key">铁路键后缀。</param>
    /// <param name="values">占位内容。</param>
    private static string F(string key, params object[] values) => string.Format(T(key), values);
}
