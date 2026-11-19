using System;
using System.Linq;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;

namespace Cultiway.Content.Railways;

/// <summary>居民许可、军令批次和候车先后顺序。</summary>
internal static class RailwayPassengerRules
{
    /// <summary>候车先后顺序，随人物保存。</summary>
    internal const string QueueKey = "cw.railway.queue";
    /// <summary>开始候车时的军队编号。</summary>
    private const string ArmyKey = "cw.railway.army";
    /// <summary>开始候车时的命令目标区域。</summary>
    private const string CommandKey = "cw.railway.command";
    /// <summary>当时是否为进攻命令。</summary>
    private const string AttackKey = "cw.railway.attack";
    /// <summary>人物加入的真实命令批次。</summary>
    private const string GenerationKey = "cw.railway.generation";
    /// <summary>零为已结束，一为候车，二为实际登车。</summary>
    private const string StageKey = "cw.railway.batch.stage";
    /// <summary>本世界递增候车顺序。</summary>
    private static long nextQueue;

    /// <summary>乘客具有城市归属且可中断当前步行时才允许申请。</summary>
    /// <param name="actor">申请人。</param>
    /// <param name="start">上车站。</param>
    /// <param name="target">下车站。</param>
    internal static bool CanRequest(Actor actor, Portal start, Portal target)
        => actor != null && actor.isAlive() && actor.hasCity() && actor.isSapient() &&
           !actor.is_inside_boat && !actor.isFighting() && !ControllableUnit.isControllingUnit(actor) &&
           RailTrackRoutePlanner.HasPermission(actor.kingdom, start.building.kingdom) &&
           RailTrackRoutePlanner.HasPermission(actor.kingdom, target.building.kingdom) &&
           !float.IsPositiveInfinity(TrainTransportSystem.Instance.EstimatePickup(start.building.id, actor.kingdom.id)) &&
           (actor.army == null || (start.building.kingdom == actor.kingdom && target.building.kingdom == actor.kingdom));

    /// <summary>同国且同一军令批次才能合并。</summary>
    /// <param name="actor">候车者。</param>
    internal static string GroupKey(Actor actor)
        => actor.kingdom.id + ":" + (actor.army?.id ?? 0) + ":" + Command(actor) + ":" + Generation(actor);

    /// <summary>混合贸易列车可服务的同国居民分组。</summary>
    /// <param name="country">车辆国家。</param>
    internal static string CivilianGroup(long country) => country + ":0:-1:0";

    /// <summary>原版命令目标或进攻状态改变后递增批次，重复目标也不会复用已取消批次。</summary>
    /// <param name="actor">军队成员。</param>
    private static long Generation(Actor actor)
    {
        if (actor.army == null) return 0;
        var data = actor.army.data;
        data.get("cw.railway.command.generation", out long generation, 0);
        data.get("cw.railway.command.last", out int previous, int.MinValue);
        data.get("cw.railway.command.attack", out bool previousAttack, false);
        int command = Command(actor); bool attack = actor.city?.target_attack_zone != null;
        if (previous != command || previousAttack != attack)
        {
            generation++; data.set("cw.railway.command.generation", generation);
            data.set("cw.railway.command.last", command); data.set("cw.railway.command.attack", attack);
        }
        return generation;
    }

    /// <summary>记录候车顺序和当时军令；重建请求不改变已有顺序。</summary>
    /// <param name="actor">已经登记的候车者。</param>
    internal static void Remember(Actor actor)
    {
        long generation = Generation(actor);
        actor.data.get(ArmyKey, out long previousArmy, 0);
        actor.data.get(GenerationKey, out long previousGeneration, 0);
        actor.data.get(StageKey, out int stage, 0);
        if (stage > 0 && (previousArmy != (actor.army?.id ?? 0) || previousGeneration != generation))
        { EndJourney(actor, false); stage = 0; }
        actor.data.get(QueueKey, out long order, 0);
        if (order == 0) actor.data.set(QueueKey, ++nextQueue);
        else nextQueue = Math.Max(nextQueue, order);
        actor.data.set(ArmyKey, actor.army?.id ?? 0);
        actor.data.set(CommandKey, Command(actor));
        actor.data.set(AttackKey, actor.city?.target_attack_zone != null);
        actor.data.set(GenerationKey, generation);
        if (actor.army != null && stage == 0)
        {
            var batch = RailwayArmyBatchProgress.Read(actor.army, generation);
            batch.Total++; batch.Waiting++; batch.Save(actor.army, generation);
            actor.data.set(StageKey, 1);
        }
    }

    /// <summary>军队、命令或外交改变后，待乘批次不再沿用旧目标。</summary>
    /// <param name="actor">候车或已登车者。</param>
    internal static bool IsCommandCurrent(Actor actor)
    {
        actor.data.get(ArmyKey, out long army, 0);
        if (army == 0) return true;
        actor.data.get(CommandKey, out int command, -1);
        actor.data.get(AttackKey, out bool attack, false);
        actor.data.get(GenerationKey, out long generation, 0);
        long currentGeneration = Generation(actor);
        if (actor.army?.id != army || Command(actor) != command || currentGeneration != generation) return false;
        var target = actor.city?.target_attack_zone;
        if (attack && target == null) return false;
        return !attack || target.city == null || target.city.kingdom == actor.kingdom ||
               actor.kingdom.isEnemy(target.city.kingdom);
    }

    /// <summary>一次军令以原版攻击区域为准，其余移动取将军保存的目标。</summary>
    /// <param name="actor">军队成员。</param>
    private static int Command(Actor actor)
    {
        if (actor.army == null) return -1;
        if (actor.city?.target_attack_zone is { } target) return target.id;
        var captain = actor.army.getCaptain();
        if (captain == null) return -1;
        captain.data.get(TrainPassengerService.GoalKey, out int goal, -1);
        return goal >= 0 && goal < World.world.tiles_list.Length
            ? World.world.tiles_list[goal].zone.id : captain.tile_target?.zone.id ?? -1;
    }

    /// <summary>军令运输优先；队列公平性由派车时的连续次数限制。</summary>
    /// <param name="request">待分配请求。</param>
    internal static bool IsMilitary(PortalRequest request)
        => request.Portals.SelectMany(p => p.ToLoad.Concat(p.ToUnload)).Any(p => p.army != null);

    /// <summary>取得最早仍在等候的顺序。</summary>
    /// <param name="request">未派车请求。</param>
    internal static long QueueOrder(PortalRequest request)
        => request.Portals.SelectMany(p => p.ToLoad).Select(p =>
        { p.data.get(QueueKey, out long order, long.MaxValue); return order; }).DefaultIfEmpty(long.MaxValue).Min();

    /// <summary>取得本批乘客的实际国家。</summary>
    /// <param name="request">客运请求。</param>
    internal static long Country(PortalRequest request)
        => request.Portals.SelectMany(p => p.ToLoad.Concat(p.ToUnload)).FirstOrDefault()?.kingdom?.id ?? 0;

    /// <summary>军令变化后从实际下车点执行新命令。</summary>
    /// <param name="actor">已经解除乘坐关系的人。</param>
    /// <param name="savedGoal">原行程的步行目标。</param>
    internal static int ResumeGoal(Actor actor, int savedGoal)
    {
        actor.data.get(ArmyKey, out long army, 0);
        return army != 0 && !IsCommandCurrent(actor)
            ? (actor.city?.target_attack_zone?.centerTile ?? actor.city?.getTile() ?? actor.current_tile).tile_id
            : savedGoal;
    }

    /// <summary>最终下车或取消后清除本次批次记录。</summary>
    /// <param name="actor">结束旅程的人。</param>
    internal static void ClearJourney(Actor actor)
    {
        actor.data.removeLong(QueueKey); actor.data.removeLong(ArmyKey);
        actor.data.removeInt(CommandKey); actor.data.removeBool(AttackKey);
        actor.data.removeLong(GenerationKey); actor.data.removeInt(StageKey);
    }

    /// <summary>实际登车后，把本批候车人数转为车内人数。</summary>
    /// <param name="actor">已建立原版乘坐关系的人。</param>
    internal static void Boarded(Actor actor)
    {
        actor.data.get(StageKey, out int stage, 0);
        actor.data.get(ArmyKey, out long id, 0); actor.data.get(GenerationKey, out long generation, 0);
        var army = World.world.armies.get(id);
        if (stage != 1 || army == null) return;
        var batch = RailwayArmyBatchProgress.Read(army, generation);
        batch.Waiting--; batch.Onboard++; batch.Save(army, generation); actor.data.set(StageKey, 2);
    }

    /// <summary>下车、取消和阵亡都只结算一次本批人数。</summary>
    /// <param name="actor">结束本次旅程的人。</param>
    /// <param name="completed">是否正常完成原定下车。</param>
    internal static void EndJourney(Actor actor, bool completed)
    {
        actor.data.get(StageKey, out int stage, 0);
        if (stage == 0) return;
        actor.data.get(ArmyKey, out long id, 0); actor.data.get(GenerationKey, out long generation, 0);
        var army = World.world.armies.get(id);
        if (army != null)
        {
            var batch = RailwayArmyBatchProgress.Read(army, generation);
            if (stage == 1) batch.Waiting--; else batch.Onboard--;
            if (completed) batch.Completed++; else batch.Cancelled++;
            batch.Save(army, generation);
        }
        actor.data.set(StageKey, 0);
    }

    /// <summary>切换世界后从人物记录重建顺序。</summary>
    internal static void Clear() => nextQueue = 0;
}
