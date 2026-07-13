using System;
using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content;

[Dependency(typeof(PlotCategories))]
public class Plots : ExtendLibrary<PlotAsset, Plots>
{
    [CloneSource(S_Plot.new_book)]
    public static PlotAsset NewCultibook { get; private set; }

    [CloneSource(S_Plot.new_language)]
    public static PlotAsset NewSect { get; private set; }

    [CloneSource(S_Plot.alliance_create)]
    public static PlotAsset BuildTrainStation { get; private set; }

    [CloneSource(S_Plot.alliance_create)]
    public static PlotAsset BuildTrainTrack { get; private set; }

    // 城市领袖/国王自主谋划：围绕城市修筑一圈城墙（方形/圆形，预留出口）
    [CloneSource(S_Plot.new_book)]
    public static PlotAsset BuildCityWall { get; private set; }

    // 召唤恶魔谋划：依据发起者特质，有几率召唤对应的混沌魔塔
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonKhorne { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonSlaanesh { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonTzeentch { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonNurgle { get; private set; }

    // 召唤恶魔谋划的触发阈值与召唤几率
    private const int   DEMON_KILLS_THRESHOLD = 20;    // 杀人数门槛 -> 恐虐
    private const int   DEMON_CHARM_THRESHOLD = 15;    // 魅力(外交)门槛 -> 色孽
    private const int   DEMON_INT_THRESHOLD   = 15;    // 智力门槛 -> 奸奇
    private const float DEMON_TOWER_CHANCE    = 0.01f; // 邪神恩赐：谋划成功后生成魔塔的概率

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        NewCultibook.path_icon = "books/custom_book_covers/cultibook/31";
        NewCultibook.check_is_possible = (Actor actor) =>
            actor.hasCity()
            && actor.hasCulture()
            && actor.hasLanguage()
            && actor.city.hasBookSlots()
            && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.check_should_continue = (Actor actor) =>
            actor.hasCity()
            && actor.hasCulture()
            && actor.hasLanguage()
            && actor.city.hasBookSlots()
            && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.action = (Actor actor) =>
        {
            if (!actor.hasCity())
            {
                return false;
            }

            if (!actor.city.hasBookSlots())
            {
                return false;
            }

            var book = World.world.books.CreateNewCultibook(actor);
            return book != null && World.world.books.TryStoreBookInCity(actor, book);
        };

        NewSect.path_icon = "books/custom_book_covers/cultibook/31";
        NewSect.group_id = PlotCategories.Sect.id;
        NewSect.check_is_possible = SectRules.CanFoundSect;
        NewSect.check_should_continue = SectRules.CanFoundSect;
        NewSect.action = a =>
        {
            return WorldboxGame.I.Sects.BuildSect(a) != null;
        };

        BuildTrainStation.path_icon = "cultiway/icons/plots/iconTrainStation";
        BuildTrainStation.group_id = PlotCategories.Others.id;
        BuildTrainStation.can_be_done_by_king = false;
        BuildTrainStation.can_be_done_by_leader = true;
        BuildTrainStation.requires_diplomacy = false;
        BuildTrainStation.check_is_possible = delegate(Actor a)
        {
            return a.hasCity()
                && !World.world.plots.isPlotTypeAlreadyRunning(a, BuildTrainStation)
                && !a.city.hasBuildingType(Buildings.TrainStation.type)
                && FindTileForTrainStation(a.city) != null;
        };
        BuildTrainStation.try_to_start_advanced = delegate(Actor a, PlotAsset asset, bool force)
        {
            var plot = World.world.plots.newPlot(a, asset, force);
            if (plot == null || !plot.checkInitiatorAndTargets())
            {
                return false;
            }
            var targets = GetTrainTargets(a.city);
            if (targets.Count == 0)
            {
                if (FindTileForTrainStation(a.city) == null)
                {
                    return false;
                }
            }
            foreach (var target_city in targets)
            {
                if (target_city.leader.isRekt())
                    continue;
                target_city.leader.setPlot(plot);
            }
            return true;
        };
        BuildTrainStation.check_should_continue = a =>
        {
            foreach (var aa in a.plot.units)
            {
                if (aa.city.isRekt())
                {
                    return false;
                }
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom))
                {
                    return false;
                }
            }
            return true;
        };
        BuildTrainStation.check_can_be_forced = a =>
        {
            return a.hasCity() && FindTileForTrainStation(a.city) != null;
        };
        BuildTrainStation.action = a =>
        {
            var plot = a.plot;

            var cities = new HashSet<City>();
            foreach (var aa in plot.units)
            {
                if (aa.city.isRekt())
                    continue;
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom))
                    continue;
                if (aa != aa.city.leader)
                    continue;
                cities.Add(aa.city);
            }

            foreach (var aa in plot.units)
            {
                aa.leavePlot();
            }
            if (cities.Count == 0)
            {
                return false;
            }
            var actually_built = 0;
            foreach (var city in cities)
            {
                if (city.hasBuildingType(Buildings.TrainStation.type))
                {
                    continue;
                }
                var tile = FindTileForTrainStation(city);
                if (tile == null)
                {
                    continue;
                }
                World.world.buildings.addBuilding(Buildings.TrainStation.id, tile);
                actually_built++;
            }
            return actually_built > 0;
        };

        BuildTrainTrack.path_icon = "cultiway/icons/plots/iconTrainNet";
        BuildTrainTrack.group_id = PlotCategories.Others.id;
        BuildTrainTrack.can_be_done_by_king = false;
        BuildTrainTrack.can_be_done_by_leader = true;
        BuildTrainTrack.requires_diplomacy = false;
        BuildTrainTrack.check_is_possible = delegate(Actor a)
        {
            return a.hasCity() && GetTrainTrackTargets(a.city).Count > 0;
        };
        BuildTrainTrack.check_can_be_forced = delegate(Actor a)
        {
            return a.hasCity() && GetTrainTrackTargets(a.city).Count > 0;
        };
        BuildTrainTrack.try_to_start_advanced = delegate(Actor a, PlotAsset asset, bool force)
        {
            var targets = GetTrainTrackTargets(a.city);
            if (targets.Count == 0)
            {
                return false;
            }

            var plot = World.world.plots.newPlot(a, asset, force);
            if (plot == null || !plot.checkInitiatorAndTargets())
            {
                return false;
            }
            foreach (var target_city in targets)
            {
                target_city.leader.setPlot(plot);
            }
            return true;
        };
        BuildTrainTrack.check_should_continue = delegate(Actor a)
        {
            foreach (var aa in a.plot.units)
            {
                if (aa.city.isRekt())
                    return false;
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom))
                    return false;
                if (aa != aa.city.leader)
                    return false;
                if (!aa.city.hasBuildingType(Buildings.TrainStation.type))
                    return false;
            }
            return true;
        };
        BuildTrainTrack.action = delegate(Actor a)
        {
            var source_city = a.city;
            var targets = new List<City>();
            foreach (var aa in a.plot.units)
            {
                if (aa == a)
                    continue;
                targets.Add(aa.city);
                aa.leavePlot();
            }
            if (targets.Count == 0)
            {
                return false;
            }
            var source_station = source_city.getBuildingOfType(Buildings.TrainStation.type);
            var any_built = false;
            foreach (var target_city in targets)
            {
                var target_station = target_city.getBuildingOfType(Buildings.TrainStation.type);
                if (target_station == null)
                    continue;
                var source_tile = source_station.current_tile;
                var target_tile = target_station.current_tile;
                if (source_tile == null || target_tile == null)
                    continue;
                var path = GetTrainTrackDirection(source_tile, target_tile);
                if (path.Count == 0)
                    continue;
                foreach (var tile in path)
                {
                    MapAction.terraformTop(tile, TopTileTypes.TrainTrack, Terraforms.TrainTrack, false);
                }
                TrainTrackRepairSystem.RegisterLink(source_station, target_station, path);
                any_built = true;
            }
            return any_built;
        };

        // 召唤恶魔：单位依据自身特质，有几率召唤对应的混沌魔塔、扭转纪元，化身为对应大魔，并把同村凡人(无灵根)化为该系列随机恶魔
        SetupDemonSummonPlot(SummonKhorne,   "cultiway/icons/Khorne",
            a => a.data.kills >= DEMON_KILLS_THRESHOLD, Buildings.KhorneTower,   Actors.Bloodthirster,
            new[] { Actors.BloodletterKhorne, Actors.FleshHoundKhorne, Actors.BloodcrusherKhorne, Actors.MinotaurKhorne, Actors.SkullCannonKhorne },
            "age_chaos");
        SetupDemonSummonPlot(SummonSlaanesh, "cultiway/icons/Slaanesh",
            a => a.diplomacy >= DEMON_CHARM_THRESHOLD,  Buildings.SlaaneshTower, Actors.KeeperSecrets,
            new[] { Actors.Daemonette, Actors.Hellflayer, Actors.SlaaneshSeeker, Actors.SlaaneshMistress, Actors.SlaaneshFiend },
            "age_moon");
        SetupDemonSummonPlot(SummonTzeentch, "cultiway/icons/Tzeentch",
            a => a.intelligence >= DEMON_INT_THRESHOLD, Buildings.TzeentchTower, Actors.LordChange,
            new[] { Actors.PinkHorrorTzeentch, Actors.BlueHorrorTzeentch, Actors.IridescentHorrorTzeentch, Actors.FlamerTzeentch, Actors.ScreamersTzeentch },
            "age_wonders");
        SetupDemonSummonPlot(SummonNurgle,   "cultiway/icons/Nurgle",
            a => a.hasTrait(S_Trait.plague),            Buildings.NurgleTower,   Actors.GreatUncleanOneButcher,
            new[] { Actors.UncleanCreature, Actors.NurgleSpirit, Actors.NurgleDiseaseCarrier, Actors.PlagueBringer, Actors.PlagueToad },
            "age_ash");

        // 修建城墙：城市领袖/国王在城市达到一定规模后自主谋划，围绕城市筑一圈城墙
        SetupBuildCityWallPlot(BuildCityWall, "cultiway/icons/Tower");
    }

    private static List<WorldTile> GetTrainTrackDirection(
        WorldTile source_tile,
        WorldTile target_tile
    )
    {
        return TrainTrackPathHelper.BuildPath(source_tile, target_tile);
    }

    private static List<City> GetTrainTrackTargets(City city)
    {
        if (!city.hasBuildingType(Buildings.TrainStation.type))
            return new List<City>();
        var targets = new List<City>();
        foreach (var target_city in city.neighbours_cities)
        {
            if (
                target_city.kingdom != city.kingdom
                && !target_city.kingdom.isOpinionTowardsKingdomGood(city.kingdom)
            )
                continue;
            if (!target_city.hasBuildingType(Buildings.TrainStation.type))
                continue;
            targets.Add(target_city);
        }
        return targets;
    }

    private static WorldTile FindTileForTrainStation(City city)
    {
        var zones = new List<TileZone>();
        CityBehBuild.fillPossibleZones(Buildings.TrainStation, city, zones);
        if (zones.Count == 0)
            return null;
        return CityBehBuild.tryToBuildInZones(zones, Buildings.TrainStation, city, false);
    }

    /// <summary>
    /// 配置一个召唤恶魔的谋划：满足 <paramref name="condition"/> 的城市领袖/国王发起后，
    /// 强制把当前纪元转变为 <paramref name="eraId"/>，
    /// 把凡人(无灵根)随机转化为 <paramref name="daemonSeries"/> 中的恶魔
    /// (领主限所属城市，国王及整个国家)，
    /// 发起人自身强制转变为对应的大魔 <paramref name="transformInto"/>（化魔，原身消散），
    /// 并有极低概率(邪神恩赐 <see cref="DEMON_TOWER_CHANCE"/>)在城内生成 <paramref name="tower"/>。
    /// </summary>
    private static void SetupDemonSummonPlot(
        PlotAsset plot, string icon, Func<Actor, bool> condition,
        BuildingAsset tower, ActorAsset transformInto, ActorAsset[] daemonSeries,
        string eraId)
    {
        plot.path_icon = icon;
        plot.group_id = PlotCategories.Others.id;
        plot.is_basic_plot = true;          // 允许 AI 单位自主发动
        plot.can_be_done_by_king = true;
        plot.can_be_done_by_leader = true;
        plot.requires_diplomacy = false;
        plot.check_is_possible = a => a.hasCity()
                                     && condition(a)
                                     && !World.world.plots.isPlotTypeAlreadyRunning(a, plot);
        plot.check_can_be_forced = plot.check_is_possible;
        plot.check_should_continue = a => a.hasCity();
        plot.action = a =>
        {
            if (!a.hasCity())
                return false;
            // 读取发起人境界(无修炼则视作 0)，供大魔继承、化魔者降一阶使用
            var initAe = a.GetExtend();
            int initiatorLevel = (initAe != null && initAe.HasCultisys<Xian>()) ? initAe.GetCultisys<Xian>().CurrLevel : 0;
            // 谋划成功：强制把当前纪元转变为对应纪元
            var age = AssetManager.era_library.get(eraId);
            if (age != null)
            {
                World.world.era_manager.setCurrentAge(age);
            }
            // 凡人化魔：领主 -> 所属城市；国王 -> 整个国家；化魔者境界比发起人低一阶
            TransformMortals(a, daemonSeries, initiatorLevel);
            // 邪神恩赐：极低概率在城内生成对应魔塔
            if (!a.city.hasBuildingType(tower.type) && Randy.randomChance(DEMON_TOWER_CHANCE))
            {
                var tile = FindTileForTower(a.city, tower);
                if (tile != null)
                {
                    World.world.buildings.addBuilding(tower.id, tile);
                }
            }
            // 发起人化身为对应的大魔（在原位生成大魔，原身以”蜕变”方式消散）
            var daemon = World.world.units.spawnNewUnit(transformInto.id, a.current_tile, true, pSpawnHeight: 0);
            // 大魔继承发起人的修为/境界等修炼数据
            InheritCultivation(a, daemon);
            // 界面提示：发起人化魔
            WorldLogUtils.LogDemonAscension(a, daemon);
            a.removeByMetamorphosis();
            return true;
        };
    }

    /// <summary>
    /// 把凡人(无灵根、未化形)随机转变为对应系列中的一种恶魔。
    /// 发起人为领主时转化其所属城市，为国王时转化整个国家；有灵根的修士不会被转化；
    /// 发起人自身另行化身为大魔，故在此排除。
    /// 化魔后的恶魔境界为发起人境界低一阶(发起人有修炼时才授予)。
    /// </summary>
    private static void TransformMortals(Actor initiator, ActorAsset[] daemonSeries, int initiatorLevel)
    {
        if (daemonSeries == null || daemonSeries.Length == 0) return;

        // 先收集要转化的凡人，避免在迭代中修改 city.units
        var mortals = new List<Actor>();
        if (initiator.isKing() && initiator.kingdom != null)
        {
            // 国王：整个国家所有城市的凡人
            foreach (var city in initiator.kingdom.cities)
            {
                CollectMortals(city, initiator, mortals);
            }
        }
        else if (initiator.city != null)
        {
            // 领主：所属城市的凡人
            CollectMortals(initiator.city, initiator, mortals);
        }

        // 发起人有修炼时，化魔者境界比其低一阶；否则不授修炼
        int daemonLevel = initiatorLevel > 0 ? Math.Max(0, initiatorLevel - 1) : -1;
        foreach (var mortal in mortals)
        {
            var daemonAsset = daemonSeries.GetRandom();
            if (daemonAsset == null) continue;
            var daemon = World.world.units.spawnNewUnit(daemonAsset.id, mortal.current_tile, false, pSpawnHeight: 0);
            if (daemonLevel >= 0) GrantXianLevel(daemon, daemonLevel);
            mortal.removeByMetamorphosis();
        }
    }

    private static void CollectMortals(City city, Actor initiator, List<Actor> result)
    {
        if (city == null) return;
        foreach (var unit in city.units)
        {
            if (unit == null || unit == initiator || !unit.isAlive()) continue;
            if (unit.isAlreadyTransformed()) continue;
            var ae = unit.GetExtend();
            if (ae == null || ae.HasElementRoot()) continue;   // 有灵根(修士)不转化
            result.Add(unit);
        }
    }

    /// <summary>
    /// 让 <paramref name="target"/> 继承 <paramref name="source"/> 的修炼数据：
    /// 仙(境界 + 修为)、金丹、元婴、筑基。
    /// </summary>
    private static void InheritCultivation(Actor source, Actor target)
    {
        var src = source.GetExtend();
        var dst = target.GetExtend();
        if (src == null || dst == null) return;

        // 仙：境界 + 修为
        if (src.HasCultisys<Xian>())
        {
            var srcXian = src.GetCultisys<Xian>();
            if (!dst.HasCultisys<Xian>()) dst.NewCultisys(Cultisyses.Xian);
            ref var dstXian = ref dst.GetCultisys<Xian>();
            dstXian.level = srcXian.level;
            dstXian.wakan = srcXian.wakan;
        }
        // 金丹 / 元婴 / 筑基
        if (src.TryGetComponent(out Jindan jindan))    { ref var j  = ref dst.GetOrAddComponent<Jindan>();    j  = jindan; }
        if (src.TryGetComponent(out Yuanying yuanying)){ ref var y  = ref dst.GetOrAddComponent<Yuanying>();  y  = yuanying; }
        if (src.TryGetComponent(out XianBase xianBase)){ ref var xb = ref dst.GetOrAddComponent<XianBase>();   xb = xianBase; }

        dst.MarkCultiwayStatsDirty();
    }

    /// <summary>
    /// 授予 <paramref name="actor"/> 仙修，并把境界设为 <paramref name="level"/>。
    /// </summary>
    private static void GrantXianLevel(Actor actor, int level)
    {
        var ae = actor.GetExtend();
        if (ae == null) return;
        if (!ae.HasCultisys<Xian>()) ae.NewCultisys(Cultisyses.Xian);
        ref var xian = ref ae.GetCultisys<Xian>();
        xian.level = level;
        xian.wakan = 0;
        ae.MarkCultiwayStatsDirty();
    }

    // ========== 修建城墙谋划（矩形包围盒，内墙 → 外墙 → 要塞 三阶段渐进） ==========
    private const int   WALL_MIN_ZONES      = 6;    // 城市至少 6 个 zone 才考虑修墙
    private const int   WALL_MARGIN         = 3;    // 内墙在所有建筑之外的余量
    private const int   WALL_SPACING        = 6;    // 外墙比内墙每边大多少
    private const float WALL_REBUILD_RATIO  = 0.3f; // 现存城墙低于完整圈的 30% 视为"被摧毁"，允许重修
    private const int   WALL_TOWER_INTERVAL = 10;   // 沿城墙每隔多少格放置一座防御箭塔

    private const int WALL_STAGE_NONE     = 0; // 无墙
    private const int WALL_STAGE_INNER    = 1; // 仅内墙（木墙，宽1）
    private const int WALL_STAGE_BOTH     = 2; // 内墙（石墙，宽2）+ 外墙（木墙，宽1）
    private const int WALL_STAGE_FORTRESS = 3; // 内墙（石墙，宽2）+ 外墙（石墙，宽2）

    /// <summary>
    /// 配置"修筑城墙"谋划：城市领袖/国王在城市达到一定规模后自主发起，分三阶段修筑。
    /// 城墙为<b>矩形</b>，以城市全部建筑的包围盒为中心、各边外扩 <see cref="WALL_MARGIN"/>，确保包围所有建筑；
    /// 4 条边中点附近各留一个出入口通道（无箭塔）。木墙阶段（初次）记录 bounds，此后<b>固定不随城市扩张变化</b>。
    /// 1) 初次：内墙 = 木墙（宽1，无箭塔）。
    /// 2) 二次：内墙升级为石墙（宽2，内侧间隔箭塔）+ 外墙 = 木墙（宽1，内墙外 <see cref="WALL_SPACING"/>）。
    /// 3) 三次：内墙石墙（宽2）+ 外墙升级为石墙（宽2，内侧间隔箭塔）。
    /// 最终阶段任一城墙被摧毁大半后可重新谋划重建。
    /// </summary>
    private static void SetupBuildCityWallPlot(PlotAsset plot, string icon)
    {
        plot.path_icon             = icon;
        plot.group_id              = PlotCategories.Others.id;
        plot.is_basic_plot         = true;   // 加入 AI 自主发动候选池（PlotsLibrary.basic_plots）
        plot.can_be_done_by_king   = true;
        plot.can_be_done_by_leader = true;
        plot.requires_diplomacy    = false;
        plot.money_cost            = 40;

        // 可行：有城 + 规模达标 + 可推进阶段(未满 或 已满但被摧毁) + 没有同类 plot 在跑
        plot.check_is_possible = a => a.hasCity()
                                     && a.city.zones.Count >= WALL_MIN_ZONES
                                     && CanAdvanceWallStage(a.city)
                                     && !World.world.plots.isPlotTypeAlreadyRunning(a, plot);
        plot.check_can_be_forced   = plot.check_is_possible;
        plot.check_should_continue = a => a.hasCity()
                                         && a.city.zones.Count >= WALL_MIN_ZONES
                                         && CanAdvanceWallStage(a.city);

        plot.action = a =>
        {
            if (!a.hasCity()) return false;
            var city  = a.city;
            int stage = GetWallStage(city);
            // 篝火已重建但不在内墙范围内（城市核心迁到新区域）→ 重置城墙，在新篝火区域重新生成木墙内墙
            var stored = GetInnerBounds(city);
            if (stage > WALL_STAGE_NONE && stored != null
                && city.hasBuildingType("type_bonfire")
                && !BonfireInBounds(city, stored.Value))
            {
                RemoveWallRing(stored.Value); // 拆除旧内墙
                stage = WALL_STAGE_NONE;
                SetWallStage(city, WALL_STAGE_NONE);
                SetInnerBounds(city, default);
            }
            // 木墙阶段（初次）记录内墙 bounds，此后固定不随城市扩张变化
            var inner = EnsureInnerBounds(city);
            var outer = OuterBounds(city);
            if (inner == null || outer == null) return false;
            if (stage == WALL_STAGE_NONE)
            {
                // 初次：内墙 = 木墙（宽1，无箭塔）
                PlaceWallRing(a, inner.Value, 1, TopTileLibrary.wall_wild, 0);
                SetWallStage(city, WALL_STAGE_INNER);
            }
            else if (stage == WALL_STAGE_INNER)
            {
                // 二次：内墙升级石墙（宽2，内侧间隔箭塔）+ 外墙木墙（宽1，拆旧外墙再建）
                PlaceWallRing(a, inner.Value, 2, TopTileLibrary.wall_order, WALL_TOWER_INTERVAL);
                ReplaceOuterWall(a, outer.Value, 1, TopTileLibrary.wall_wild, 0);
                SetWallStage(city, WALL_STAGE_BOTH);
            }
            else
            {
                // 三次(stage 2→3) 或 被毁重建(stage 3)：内墙石墙宽2 + 外墙石墙宽2（拆旧外墙再建）
                PlaceWallRing(a, inner.Value, 2, TopTileLibrary.wall_order, WALL_TOWER_INTERVAL);
                ReplaceOuterWall(a, outer.Value, 2, TopTileLibrary.wall_order, WALL_TOWER_INTERVAL);
                SetWallStage(city, WALL_STAGE_FORTRESS);
            }
            return true;
        };
    }

    /// <summary>
    /// 放置矩形城墙：生成 ring → 直接设置城墙 top_tile（不拆除建筑、不改变城市 zones）。
    /// <b>箭塔只在两格宽石墙(wall_order)的内侧圈、按 towerInterval 间距放置</b>（木墙/单格墙都不放）。
    /// </summary>
    private static void PlaceWallRing(Actor actor, WallShapeHelper.Bounds b, int width, TopTileType wall, int towerInterval)
    {
        var city = actor.city;
        var ring = WallShapeHelper.ComputeWallRing(b, width);
        foreach (var tile in ring)
        {
            // 直接设置城墙 top_tile；不用 MapAction.terraformTop（它会摧毁路径建筑，导致城市 zone 被放弃），
            // 也不主动拆除建筑——城墙与现有建筑共存，不影响城市 zones
            tile.setTopTileType(wall);
        }

        // 箭塔只在"两格宽石墙(wall_order)"的内侧圈、按 towerInterval 间距放置
        if (towerInterval > 0 && width >= 2 && wall == TopTileLibrary.wall_order)
        {
            string tower_id = GetWatchTowerId(city);
            var inner = WallShapeHelper.ComputeWallRing(b, 1); // 内侧圈
            for (int i = 0; i < inner.Count; i += towerInterval)
            {
                var tile = inner[i];
                if (tile.building != null) continue;
                var bb = World.world.buildings.addBuilding(tower_id, tile);
                ModClass.LogInfo($"Place tower(id: {bb.data.id}), its kingdom: null?{bb.kingdom==null}, id?{bb.kingdom?.id}, asset:{bb.kingdom?.asset.id}");
            }
        }
    }

    /// <summary>按城市种族选取箭塔 id（watch_tower_human/orc/elf/dwarf），无对应样式则回退人类。</summary>
    private static string GetWatchTowerId(City city)
    {
        const string fallback = "watch_tower_human";
        string race = city?.data?.original_actor_asset;
        if (string.IsNullOrEmpty(race)) return fallback;
        string id = "watch_tower_" + race;
        return AssetManager.buildings.get(id) != null ? id : fallback;
    }

    /// <summary>是否可以推进城墙阶段：未到 FORTRESS 总可推进；到 FORTRESS 后只有墙被毁才允许重建。</summary>
    private static bool CanAdvanceWallStage(City city)
    {
        if (city == null) return false;
        int stage = GetWallStage(city);
        if (stage < WALL_STAGE_FORTRESS) return true;
        return !WallsIntact(city); // 已建成要塞：任一城墙被摧毁大半即可重建
    }

    /// <summary>内墙与外墙是否都基本完好（现存比例均 ≥ 阈值）。用记录的（固定）bounds 检测。</summary>
    private static bool WallsIntact(City city)
    {
        if (city == null) return false;
        var inner = GetInnerBounds(city);
        var outer = OuterBounds(city);
        if (inner == null || outer == null) return false;
        float innerRatio = WallShapeHelper.ExistingWallRatio(inner.Value, 2);
        float outerRatio = WallShapeHelper.ExistingWallRatio(outer.Value, 2);
        return innerRatio >= WALL_REBUILD_RATIO && outerRatio >= WALL_REBUILD_RATIO;
    }

    private static int GetWallStage(City city)
    {
        if (city?.data == null) return WALL_STAGE_NONE;
        city.data.get(ContentCityDataKeys.CityWallStage_int, out int stage, WALL_STAGE_NONE);
        return stage;
    }

    private static void SetWallStage(City city, int stage)
    {
        if (city?.data == null) return;
        city.data.set(ContentCityDataKeys.CityWallStage_int, stage);
    }

    /// <summary>
    /// 清除城市的全部城墙：拆除内/外墙墙体 tile，清空所有城墙记录（内/外墙 bounds + stage）。
    /// 用于篝火（城市核心）被摧毁时立即重置城墙系统，不等下次谋划。
    /// </summary>
    public static void ClearCityWalls(City city)
    {
        if (city?.data == null) return;
        var inner = GetInnerBounds(city);
        if (inner != null) RemoveWallRing(inner.Value);
        var outer = GetOuterBounds(city);
        if (outer != null) RemoveWallRing(outer.Value);
        SetInnerBounds(city, default);
        SetOuterBounds(city, default);
        SetWallStage(city, WALL_STAGE_NONE);
    }

    /// <summary>取得记录的内墙 bounds（固定，木墙阶段建墙时记录）；无记录返回 null。</summary>
    private static WallShapeHelper.Bounds? GetInnerBounds(City city)
    {
        if (city?.data == null) return null;
        city.data.get(ContentCityDataKeys.CityWallInnerHX_int, out int hx, 0);
        city.data.get(ContentCityDataKeys.CityWallInnerHY_int, out int hy, 0);
        if (hx <= 0 || hy <= 0) return null;
        city.data.get(ContentCityDataKeys.CityWallInnerCX_int, out int cx, 0);
        city.data.get(ContentCityDataKeys.CityWallInnerCY_int, out int cy, 0);
        return new WallShapeHelper.Bounds { cx = cx, cy = cy, hx = hx, hy = hy };
    }

    /// <summary>记录内墙 bounds 到 city.data。</summary>
    private static void SetInnerBounds(City city, WallShapeHelper.Bounds b)
    {
        if (city?.data == null) return;
        city.data.set(ContentCityDataKeys.CityWallInnerCX_int, b.cx);
        city.data.set(ContentCityDataKeys.CityWallInnerCY_int, b.cy);
        city.data.set(ContentCityDataKeys.CityWallInnerHX_int, b.hx);
        city.data.set(ContentCityDataKeys.CityWallInnerHY_int, b.hy);
    }

    /// <summary>
    /// 确保内墙 bounds 已记录：已记录则返回；否则按当前建筑包围盒 + MARGIN 计算并记录。
    /// <b>篝火（城市核心）被摧毁时取消固定，清除记录以便重新按当前建筑选定范围。</b>
    /// </summary>
    private static WallShapeHelper.Bounds? EnsureInnerBounds(City city)
    {
        // 篝火被摧毁 → 拆除旧内墙墙体、取消固定，重新选定范围
        if (city != null && !city.hasBuildingType("type_bonfire"))
        {
            var old = GetInnerBounds(city);
            if (old != null) RemoveWallRing(old.Value); // 自动清除旧内墙墙体
            SetInnerBounds(city, default);
        }
        var stored = GetInnerBounds(city);
        if (stored != null) return stored;
        var bb = WallShapeHelper.GetBuildingsBounds(city);
        if (bb == null) return null;
        var inner = new WallShapeHelper.Bounds
        {
            cx = bb.Value.cx, cy = bb.Value.cy,
            hx = bb.Value.hx + WALL_MARGIN, hy = bb.Value.hy + WALL_MARGIN
        };
        SetInnerBounds(city, inner);
        return inner;
    }

    /// <summary>篝火（城市核心）是否在内墙 bounds 矩形内。用于检测"篝火迁到新区域"以触发城墙重置。</summary>
    private static bool BonfireInBounds(City city, WallShapeHelper.Bounds b)
    {
        var bonfire = city?.getBuildingOfType("type_bonfire");
        if (bonfire?.current_tile == null) return false;
        var t = bonfire.current_tile;
        return System.Math.Abs(t.x - b.cx) <= b.hx && System.Math.Abs(t.y - b.cy) <= b.hy;
    }

    /// <summary>外墙 bounds：按<b>当前建筑包围盒</b>动态计算（各边外扩 MARGIN+SPACING），与内墙中心可不同。</summary>
    private static WallShapeHelper.Bounds? OuterBounds(City city)
    {
        var bb = WallShapeHelper.GetBuildingsBounds(city);
        if (bb == null) return null;
        return new WallShapeHelper.Bounds
        {
            cx = bb.Value.cx, cy = bb.Value.cy,
            hx = bb.Value.hx + WALL_MARGIN + WALL_SPACING, hy = bb.Value.hy + WALL_MARGIN + WALL_SPACING
        };
    }

    /// <summary>替换外墙：先拆除上一轮记录的旧外墙，再按新 bounds 建外墙，并记录新 bounds。</summary>
    private static void ReplaceOuterWall(Actor actor, WallShapeHelper.Bounds b, int width, TopTileType wall, int towerInterval)
    {
        var city = actor.city;
        var old = GetOuterBounds(city);
        if (old != null) RemoveWallRing(old.Value); // 拆除旧外墙（2 圈覆盖宽1/宽2）
        PlaceWallRing(actor, b, width, wall, towerInterval);
        SetOuterBounds(city, b); // 记录新外墙 bounds
    }

    /// <summary>清除给定 bounds 上的城墙 top_tile（不拆建筑、不动地形）。用于拆除上一轮旧外墙。</summary>
    private static void RemoveWallRing(WallShapeHelper.Bounds b)
    {
        foreach (var tile in WallShapeHelper.ComputeWallRing(b, 2)) // 2 圈覆盖旧外墙宽度
        {
            if (tile != null && tile.top_type != null && tile.top_type.wall)
                tile.setTopTileType(null);
        }
    }

    /// <summary>取得记录的外墙 bounds；无记录返回 null。</summary>
    private static WallShapeHelper.Bounds? GetOuterBounds(City city)
    {
        if (city?.data == null) return null;
        city.data.get(ContentCityDataKeys.CityWallOuterHX_int, out int hx, 0);
        city.data.get(ContentCityDataKeys.CityWallOuterHY_int, out int hy, 0);
        if (hx <= 0 || hy <= 0) return null;
        city.data.get(ContentCityDataKeys.CityWallOuterCX_int, out int cx, 0);
        city.data.get(ContentCityDataKeys.CityWallOuterCY_int, out int cy, 0);
        return new WallShapeHelper.Bounds { cx = cx, cy = cy, hx = hx, hy = hy };
    }

    /// <summary>记录外墙 bounds 到 city.data。</summary>
    private static void SetOuterBounds(City city, WallShapeHelper.Bounds b)
    {
        if (city?.data == null) return;
        city.data.set(ContentCityDataKeys.CityWallOuterCX_int, b.cx);
        city.data.set(ContentCityDataKeys.CityWallOuterCY_int, b.cy);
        city.data.set(ContentCityDataKeys.CityWallOuterHX_int, b.hx);
        city.data.set(ContentCityDataKeys.CityWallOuterHY_int, b.hy);
    }

    private static WorldTile FindTileForTower(City city, BuildingAsset asset)
    {
        var zones = new List<TileZone>();
        CityBehBuild.fillPossibleZones(asset, city, zones);
        if (zones.Count == 0)
            return null;
        return CityBehBuild.tryToBuildInZones(zones, asset, city, false);
    }

    private static List<City> GetTrainTargets(City city)
    {
        var targets = new List<City>();
        foreach (var target_city in city.neighbours_cities)
        {
            if (
                target_city.kingdom != city.kingdom
                && !target_city.kingdom.isOpinionTowardsKingdomGood(city.kingdom)
            )
                continue;

            var tile = FindTileForTrainStation(target_city);
            if (tile == null)
                continue;
            targets.Add(target_city);
        }
        return targets;
    }
}
