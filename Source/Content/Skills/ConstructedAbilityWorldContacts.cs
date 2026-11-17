using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Effects;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一项世界操作在真实地块接触处提交后的事实。</summary>
public readonly struct ConstructedWorldContact
{
    /// <summary>施放、原来源及当前归属。</summary>
    public readonly long CastId, SourceId, OwnerId;

    /// <summary>构造与实际操作编号。</summary>
    public readonly string AbilityId, Action;

    /// <summary>实际部位及地块坐标。</summary>
    public readonly int Node, X, Y;

    /// <summary>实际共同世界时点及施放年龄。</summary>
    public readonly double WorldTime;

    /// <summary>施放年龄。</summary>
    public readonly float Age;

    /// <summary>实际世界结果，失败尝试也记录，不能用显示代替成功。</summary>
    public readonly SkillEffectResult Result;

    /// <summary>本地块已尝试次数。</summary>
    public readonly int Attempts;

    /// <summary>本次真正交给永久结果的生成体积及其原支付份额，失败为零。</summary>
    public readonly double MaterialVolume, MaterialResource;

    /// <summary>只在主线程已经提交后创建。</summary>
    /// <param name="runningId">施放编号。</param><param name="source">原来源。</param><param name="owner">当前归属。</param>
    /// <param name="ability">构造编号。</param><param name="action">实际操作。</param><param name="node">实际部位。</param>
    /// <param name="x">地块横坐标。</param><param name="y">地块纵坐标。</param><param name="time">世界时间。</param>
    /// <param name="age">施放年龄。</param><param name="result">实际结果。</param><param name="attempts">已尝试次数。</param>
    /// <param name="materialVolume">真正交出的体积。</param><param name="materialResource">随材料退出的支付份额。</param>
    internal ConstructedWorldContact(long runningId, long source, long owner, string ability, string action,
        int node, int x, int y, double time, float age, SkillEffectResult result, int attempts,
        double materialVolume, double materialResource)
    {
        CastId = runningId;
        SourceId = source;
        OwnerId = owner;
        AbilityId = ability;
        Action = action;
        Node = node;
        X = x;
        Y = y;
        WorldTime = time;
        Age = age;
        Result = result;
        Attempts = attempts;
        MaterialVolume = materialVolume;
        MaterialResource = materialResource;
    }
}

/// <summary>世界操作与人物、交锋共用首次接触时间，不另设按画面更新的法术时钟。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>预估的剩余地块查询预算；负一表示正式运行，不限制正式结果。</summary>
    private int worldQueryBudget = -1;

    /// <summary>已经提交的实际世界接触，供玩法和验证观察。</summary>
    public static event Action<ConstructedWorldContact> WorldContactConfirmed;

    /// <summary>本部位一项操作的独立目标数量及总次数，不与人物接触混用编号。</summary>
    private sealed class WorldContactTotals
    {
        /// <summary>已经接受的不同地块及总尝试次数。</summary>
        internal int Tiles, Attempts;
    }

    /// <summary>真实地块表面薄箱，不把模型高空投影当作地面接触。</summary>
    /// <param name="tile">实际地块。</param><param name="centreOnly">是否只采样地块中心。</param>
    private AbilityCapsule WorldTileShape(WorldTile tile, bool centreOnly = false)
    {
        float x = tile.pos.x + .5f, y = tile.pos.y + .5f;
        var point = new Vector3(x, y, terrain?.Sample(x, y) ?? 0f);
        return new AbilityCapsule(-1, point, point, 0, "physical",
            halfExtents: new Vector3(centreOnly ? 0f : .5f, centreOnly ? 0f : .5f, .015f));
    }

    /// <summary>只在真正声明世界操作的作用面附近找地块，频率与几何都使用共同预测段。</summary>
    /// <param name="shape">实际前后作用面。</param><param name="step">共同预测世界步长。</param>
    /// <param name="include">与其他接触一起选择最早结果。</param>
    /// <param name="bodyStage">指定身体作用，负一沿用当前实例。</param>
    /// <param name="oldScope">原作用只接受边界前的真实路径。</param>
    /// <param name="endpointOnly">新作用只接受边界处接触。</param>
    private void FindWorldContacts(in ShapeMotion shape, float step, Action<ContactChoice> include,
        int bodyStage = -1, bool oldScope = false, bool endpointOnly = false)
    {
        RunningCast running = shape.Running;
        if (running.Cast.Role(shape.To.Node) == ConstructionContactRole.Decoration) return;
        int stage = bodyStage < 0 ? running.Cast.BodyStage : bodyStage;
        if (!running.Cast.ContactInterval(shape.To.Node, stage, out float from, out float until)) return;
        IReadOnlyList<CompiledConstructionWorldEffect> effects = running.Cast.ImpactAtStage(shape.To.Node, stage).WorldEffects;
        float ready = running.Cast.WorldContactReadyAt(shape.To.Node);
        bool signal = ready <= running.Cast.Age;
        if (effects.Count == 0 && !signal) return;
        int left = Mathf.Max(0, Mathf.FloorToInt(shape.Bounds.min.x));
        int bottom = Mathf.Max(0, Mathf.FloorToInt(shape.Bounds.min.y));
        int right = Mathf.Min(MapBox.width - 1, Mathf.FloorToInt(shape.Bounds.max.x));
        int top = Mathf.Min(MapBox.height - 1, Mathf.FloorToInt(shape.Bounds.max.y));
        for (int y = bottom; y <= top; y++)
        for (int x = left; x <= right; x++)
        {
            WorldTile tile = World.world.GetTileSimple(x, y);
            if (signal)
            {
                float earliest = !shape.Continuous || endpointOnly ? 1f :
                    Mathf.Max(from, ready > running.Cast.Age - step ?
                        Mathf.Clamp01((ready - (running.Cast.Age - step)) / step) : 0f);
                AbilityCapsule surface = WorldTileShape(tile);
                float at; Vector3 point, normal;
                bool found;
                if (shape.To.Node < 0 && running.Cast.Plan.Body.Part.IsJoint)
                {
                    BodySurfacePose end = running.Cast.BodyPose.Value.Joint.Contact;
                    BodySurfacePose start = BodySurfacePose.Lerp(running.BeforeJoint.Contact, end, earliest);
                    found = BodySurfacePose.Find(start, end, surface, surface, "surface", out at, out var anchor, out normal);
                    point = found ? anchor.Point(BodySurfacePose.Lerp(start, end, at)) : default;
                    normal = -normal;
                }
                else
                    found = AbilityPairSweep.Find(Slice(shape.From, shape.To, earliest), shape.To,
                        surface, surface, out at, out point, out normal);
                float fraction = Mathf.Lerp(earliest, 1f, at);
                if (found && fraction <= until && !(oldScope && fraction >= 1f))
                    include(new ContactChoice(shape, default, null, fraction, point, normal, step,
                        tile: tile, worldEffect: -1));
            }
            for (int effect = 0; effect < effects.Count; effect++)
            {
                if (worldQueryBudget == 0) return;
                if (worldQueryBudget > 0) worldQueryBudget--;
                CompiledConstructionWorldEffect rule = effects[effect];
                float earliest = Mathf.Max(from, WorldContactFraction(running, shape.To.Node, effect, x + y * MapBox.width, rule, step, stage, from));
                if (earliest > 1 || earliest > until) continue;
                // 中心作用仍经过真实扫掠；粗筛先排除作用范围外的邻格，已作用过的地块也不再读取地面。
                if (rule.TileCentreOnly && (x + .5f < shape.Bounds.min.x || x + .5f > shape.Bounds.max.x ||
                    y + .5f < shape.Bounds.min.y || y + .5f > shape.Bounds.max.y)) continue;
                if (!rule.Action.CanApply(tile) || !WorldOperationAllowed(running, shape.To.Node, shape.To.Shape, rule, tile)) continue;
                AbilityCapsule surface = WorldTileShape(tile, rule.TileCentreOnly);
                bool pulse = rule.Contact.Mode == ConstructionContactMode.Pulse;
                if (pulse && (!shape.Continuous || endpointOnly) && earliest < 1f - .000001f) continue;
                if (!shape.Continuous || endpointOnly) earliest = 1;
                AbilityCapsule start = Slice(shape.From, shape.To, earliest);
                if (!AbilityPairSweep.Find(start, pulse ? start : shape.To, surface, surface, out float at,
                        out Vector3 point, out Vector3 normal)) continue;
                float fraction = Mathf.Lerp(earliest, 1, at);
                if (fraction > until || oldScope && fraction >= 1f) continue;
                include(new ContactChoice(shape, default, null, fraction, point, normal,
                    step, tile: tile, worldEffect: effect));
            }
        }
    }

    /// <summary>世界接触单独记忆地块和操作，分件形状仍去重到同一部位。</summary>
    /// <param name="running">当前施放。</param><param name="node">实际部位。</param>
    /// <param name="effect">该作用面的操作序号。</param><param name="tile">稳定地块编号。</param>
    /// <param name="rule">实际频率和容量。</param><param name="step">共同预测步长。</param>
    /// <param name="bodyStage">预测中需要确认的身体作用实例。</param>
    /// <param name="earliest">作用窗口内最早允许的路径进度。</param>
    private float WorldContactFraction(RunningCast running, int node, int effect, int tile,
        CompiledConstructionWorldEffect rule, float step, int bodyStage, float earliest)
    {
        long owner = running.Owner(node).Id;
        int reflection = running.Cast.Reflections(node);
        int stage = node < 0 ? bodyStage : -1;
        var key = (node, stage, effect, owner, reflection);
        running.WorldTotals.TryGetValue(key, out WorldContactTotals totals);
        if (rule.Contact.MaximumTotal > 0 && totals?.Attempts >= rule.Contact.MaximumTotal)
            return float.PositiveInfinity;
        running.WorldHits.TryGetValue((node, stage, effect, tile, owner, reflection), out ContactMemory memory);
        if (memory == null && totals?.Tiles >= rule.MaximumTiles) return float.PositiveInfinity;
        if (rule.Contact.MaximumPerTarget > 0 && memory?.Count >= rule.Contact.MaximumPerTarget) return float.PositiveInfinity;
        if (rule.Contact.Mode == ConstructionContactMode.Pulse)
            return PulseFraction(rule.Contact.Interval, memory, running.Cast.Age - step, step, earliest);
        if (memory == null) return 0;
        return rule.Contact.Mode switch
        {
            ConstructionContactMode.Once => float.PositiveInfinity,
            ConstructionContactMode.Interval => Mathf.Max(0,
                (memory.Age + rule.Contact.Interval - (running.Cast.Age - step)) / step),
            ConstructionContactMode.Reentry => WorldReentryFraction(running, node, effect, tile, memory, bodyStage),
            _ => throw new InvalidOperationException("已编译世界接触规则无效")
        };
    }

    /// <summary>同段内离开又进入也合并全部分件区间，不依赖画面帧数。</summary>
    /// <param name="running">实际施放。</param><param name="node">实际部位。</param>
    /// <param name="effect">当前世界原理索引。</param><param name="tile">稳定地块编号。</param>
    /// <param name="memory">此地块接触记忆。</param>
    /// <param name="bodyStage">预测中需要确认的身体作用实例。</param>
    private float WorldReentryFraction(RunningCast running, int node, int effect, int tile, ContactMemory memory, int bodyStage)
    {
        if (!memory.Touching) return 0;
        if (memory.Evaluation != contactEvaluation)
        {
            AbilityCapsule surface =
                WorldTileShape(World.world.GetTileSimple(tile % MapBox.width, tile / MapBox.width),
                    running.Cast.ImpactAtStage(node, bodyStage).WorldEffects[effect].TileCentreOnly);
            memory.Fraction = NextReentry(running, default, surface, surface, node, bodyStage);
            memory.Evaluation = contactEvaluation;
        }

        return memory.Fraction;
    }

    /// <summary>在共同起点检查真实离开，不能将下一帧当作重新进入。</summary>
    /// <param name="running">实际施放。</param>
    private void RefreshWorldReentry(RunningCast running)
    {
        if (!running.Cast.Plan.HasWorldReentry) return;
        foreach (var pair in running.WorldHits)
        {
            var key = pair.Key;
            // 原阶段可能声明另一组原理；旧记忆不能拿新阶段的操作索引访问或重复生效。
            if (key.Node < 0 && key.Stage != running.Cast.BodyStage) continue;
            ContactMemory memory = pair.Value;
            if (!memory.Touching || running.Cast.Impact(key.Node).WorldEffects[key.Effect].Contact.Mode !=
                ConstructionContactMode.Reentry) continue;
            memory.Touching = false;
            WorldTile tile = World.world.GetTileSimple(key.Tile % MapBox.width, key.Tile / MapBox.width);
            AbilityCapsule surface =
                WorldTileShape(tile, running.Cast.Impact(key.Node).WorldEffects[key.Effect].TileCentreOnly);
            foreach (AbilityCapsule shape in running.Before)
                if (shape.Node == key.Node &&
                    AbilityPairSweep.Find(shape, shape, surface, surface, out _, out _, out _))
                {
                    memory.Touching = true;
                    break;
                }
        }
    }

    /// <summary>只在回到首次接触时点后修改原版世界，失败尝试也遵守真实频率。</summary>
    /// <param name="hit">与其他作用共同排序的接触。</param>
    private static void ResolveWorldContact(in ContactChoice hit)
    {
        RunningCast running = hit.Left.Running;
        int node = hit.Left.To.Node, effect = hit.WorldEffect;
        if (effect < 0)
        {
            running.Cast.ConfirmWorldContact(node, hit.Point);
            return;
        }
        CompiledConstructionWorldEffect rule = running.Cast.Impact(node).WorldEffects[effect];
        if (!rule.Action.CanApply(hit.Tile) || !WorldOperationAllowed(running, node, hit.Left.To.Shape, rule, hit.Tile)) return;
        long owner = running.Owner(node).Id;
        ContactMemory memory = RecordWorldAttempt(running, node, effect, hit.Tile);
        long generation = worldGeneration;
        TileType previousMain = hit.Tile.main_type;
        TopTileType previousTop = hit.Tile.top_type;
        int previousHeight = hit.Tile.Height;
        SkillEffectResult result =
            rule.Action.Apply(new ConstructionWorldContext(hit.Tile, owner, running.Cast.Plan.Id, rule.Duration,
                LogicTime));
        if (generation != worldGeneration) return;
        double materialVolume = 0, materialResource = 0;
        if (result.Changed)
            materialVolume = running.Cast.UseWorldMaterial(node, hit.Left.To.Shape, rule.MaterialVolume,
                hit.Point, out materialResource);
        if (previousMain != hit.Tile.main_type || previousTop != hit.Tile.top_type || previousHeight != hit.Tile.Height)
            simulation.RefreshTerrainAt(hit.Tile);
        if (rule.MaterialVolume == 0) running.Cast.ConfirmWorldEffect(node, result.Changed);
        running.Cast.RecordActionContact(node, hit.Point, AbilityActionContactKind.World);
        if (result.Changed) running.WorldChanges++;
        WorldContactConfirmed?.Invoke(new ConstructedWorldContact(running.Id, running.SourceId, owner,
            running.Cast.Plan.Id, rule.Action.Id, node, hit.Tile.pos.x, hit.Tile.pos.y,
            LogicTime, running.Cast.Age, result, memory.Count, materialVolume, materialResource));
    }

    /// <summary>预估与正式提交共用同一接触次数、间隔和不同地块预算，不在这里改变世界。</summary>
    /// <param name="running">实际或独立预估施放。</param><param name="node">部位。</param>
    /// <param name="effect">操作序号。</param><param name="target">真实地块。</param>
    private static ContactMemory RecordWorldAttempt(RunningCast running, int node, int effect, WorldTile target)
    {
        long owner = running.Owner(node).Id;
        int reflection = running.Cast.Reflections(node);
        int tile = target.pos.x + target.pos.y * MapBox.width;
        int stage = node < 0 ? running.Cast.BodyStage : -1;
        var key = (node, stage, effect, tile, owner, reflection);
        if (!running.WorldHits.TryGetValue(key, out ContactMemory memory))
            running.WorldHits.Add(key, memory = new ContactMemory());
        var totalKey = (node, stage, effect, owner, reflection);
        if (!running.WorldTotals.TryGetValue(totalKey, out WorldContactTotals totals))
            running.WorldTotals.Add(totalKey, totals = new WorldContactTotals());
        if (memory.Count == 0) totals.Tiles++;
        totals.Attempts++;
        memory.Count++;
        memory.Age = running.Cast.Age;
        memory.Touching = true;
        return memory;
    }

    /// <summary>查询和实际提交均检查当前任务，前一个结果可能已经改变城市或保护条件。</summary>
    /// <param name="running">本次施放。</param><param name="node">真实部位。</param><param name="shape">真实接触形状。</param>
    /// <param name="rule">实际原理。</param>
    /// <param name="tile">当前地块。</param>
    private static bool WorldOperationAllowed(RunningCast running, int node, int shape, CompiledConstructionWorldEffect rule, WorldTile tile)
        => running.Cast.CanUseWorldMaterial(node, shape, rule.MaterialVolume) &&
           (running.WorldScope == null || running.WorldScope.IsValid() && running.WorldScope.Allows(rule.Action.Id, tile));
}
