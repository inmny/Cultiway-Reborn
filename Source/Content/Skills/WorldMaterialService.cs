using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Performance;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>独立维护及保存生成材料，不占用长期施放容器。</summary>
public sealed partial class WorldMaterialService : BaseSystem, ICooperativeSystemStep
{
    /// <summary>记录和水格预算，在扣费前保留，不能静默挤掉已有材料。</summary>
    internal const int MaximumRecords = 1024, MaximumCells = 262144;

    /// <summary>唯一事实集合，按稳定世界编号维护。</summary>
    private static readonly Dictionary<long, WorldMaterialRecord> rows = new();

    /// <summary>下一个稳定编号，不以运行实体编号替代。</summary>
    private static long nextId = 1;

    /// <summary>已经接受施放但尚未转交的额度。</summary>
    private static int reservedRows, reservedCells, cells;

    /// <summary>实际系统与清世界代数。</summary>
    private static WorldMaterialService instance;

    /// <summary>世界变化后旧计算不得继续发布或入账。</summary>
    private long generation;

    /// <summary>等待现有工作池的唯一票据。</summary>
    private SimulationWorkerPool.WorkTicket? job;

    /// <summary>可分帧继续的本轮过程。</summary>
    private IEnumerator<bool> pending;

    /// <summary>已经采集的共同地表。</summary>
    private AbilityHeightField terrain;

    /// <summary>实际形状的空间筛选，查询不检查所有水格。</summary>
    private readonly AbilityContactIndex index = new();

    /// <summary>索引与事实对应，以及可复用查询、格子和形状缓冲。</summary>
    private readonly List<WorldMaterialRecord> indexed = new();

    /// <summary>当前筛选结果。</summary>
    private readonly List<int> nearby = new(), picked = new();

    /// <summary>实际物理形状缓冲。</summary>
    private readonly List<AbilityCapsule> shapes = new();

    /// <summary>世界材料发生变化后才重建索引。</summary>
    private bool indexDirty = true;

    /// <summary>当前独立世界材料数量。</summary>
    public static int Count => rows.Count;

    /// <summary>材料已经被正式接受、开始回收或实际入账后发布。</summary>
    public static event Action<WorldMaterialChange> Changed;

    /// <summary>现有调度器使用的阶段名称。</summary>
    string ICooperativeSystemStep.CooperativePhaseName => "world.material";

    /// <summary>登记独立事实、版本区段及现有逻辑调度。</summary>
    internal static void Initialize()
    {
        instance = new WorldMaterialService();
        ModClass.I.GeneralLogicSystems.Add(instance);
        PatchMapBox.RegisterActionOnClearWorld(instance.Clear);
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, Archive>("world_materials", "世界生成材料", 2,
            _ => new Archive
                { Next = nextId, Records = JArray.FromObject(rows.Values.Select(row => row.Capture()).ToArray()),
                RecoveryGroups = JArray.FromObject(recoveryGroups.Values.ToArray()) },
            (_, data) => Restore(data), data =>
            {
                if (data.Next <= 0 || data.Next == long.MaxValue || data.Records == null ||
                    data.Records.Count > MaximumRecords || data.RecoveryGroups == null ||
                    data.RecoveryGroups.Count > MaximumRecords) throw new InvalidDataException("世界材料集合或稳定编号超过保存范围");
            }, migrations: new ISaveMigration[] { new WorldMaterialsV1ToV2() }));
    }

    /// <summary>世界保存的独立容器，条目逐个解释，坏条目不拖垮整个部分。</summary>
    public sealed class Archive
    {
        /// <summary>下一个稳定编号。</summary>
        public long Next;

        /// <summary>可独立报告与丢弃的材料记录。</summary>
        public JArray Records;

        /// <summary>跨内部区域累计的实际回收过程。</summary>
        public JArray RecoveryGroups;
    }

    /// <summary>支付前检查本次全部潜在留置所需额度。</summary>
    /// <param name="plan">已经展开的计划。</param>
    internal static bool CanReserve(CompiledAbility plan) =>
        rows.Count + reservedRows + plan.ResidueCount <= MaximumRecords &&
        cells + reservedCells + plan.ResidueCells <= MaximumCells;

    /// <summary>正式受理后保留额度。</summary>
    /// <param name="count">记录数。</param><param name="waterCells">水格数。</param>
    internal static void Reserve(int count, int waterCells)
    {
        reservedRows += count;
        reservedCells += waterCells;
    }

    /// <summary>结束和成功转交各释放自己持有的额度。</summary>
    /// <param name="count">仍保留的记录数。</param><param name="waterCells">仍保留的水格数。</param>
    internal static void Release(int count, int waterCells)
    {
        reservedRows -= count;
        reservedCells -= waterCells;
    }

    /// <summary>接受真实材料后才生成稳定记录，原来源随后清零。</summary>
    /// <param name="material">独立当前材料。</param><param name="provider">原支付者。</param>
    /// <param name="controller">当前控制者。</param><param name="resource">原资源。</param>
    /// <param name="lifetime">独立存在秒数，零表示持续保留。</param><param name="clock">真实接受时点。</param>
    internal static long Accept(AbilityMaterial material, long provider, long controller, string resource,
        float lifetime, double clock)
    {
        var row = new WorldMaterialRecord
        {
            Id = nextId++,
            Provider = provider,
            Controller = controller,
            Resource = resource,
            Material = material,
            Clock = clock,
            Expires = lifetime > 0 ? clock + lifetime : 0
        };
        row.Lineage = row.Id;
        rows.Add(row.Id, row);
        cells += material.Cells * material.Cells;
        instance.indexDirty = true;
        return row.Id;
    }

    /// <summary>源部位已完成转交后才发布，事件不能把同一份材料领取两次。</summary>
    /// <param name="id">已确认的世界编号。</param>
    /// <param name="castId">只用于此次确认的原施放编号。</param>
    /// <param name="abilityId">此次正式构造编号。</param>
    internal static void PublishAccepted(long id, long castId, string abilityId)
    {
        if (rows.TryGetValue(id, out WorldMaterialRecord row))
            Changed?.Invoke(new WorldMaterialChange(row, "leave", castId: castId, abilityId: abilityId));
    }

    /// <summary>从实际选择区域取得属于当前控制者的材料。</summary>
    /// <param name="controller">发出本次选择的真实控制者。</param><param name="selection">同一姿态的真实形状。</param>
    /// <param name="recovery">实际收束参数。</param><param name="clock">已经确认的世界时间。</param>
    internal static void Recover(long controller, IReadOnlyList<AbilityCapsule> selection, CompiledRecovery recovery,
        double clock)
    {
        instance.RebuildIndex();
        var targets = new HashSet<long>();
        foreach (AbilityCapsule shape in selection)
        {
            instance.index.Query(AbilityContactIndex.Swept(shape, shape), instance.nearby);
            foreach (int i in instance.nearby) targets.Add(instance.indexed[i].Id);
        }

        long epoch = instance.generation;
        var groups = new Dictionary<string, long>();
        foreach (long id in targets.OrderBy(id => id))
        {
            if (!rows.TryGetValue(id, out WorldMaterialRecord row) || row.Provider != controller || row.Recovering ||
                row.Finished) continue;
            if (!(row.WorldGrid
                    ? row.Material.SelectShared(selection, instance.picked, instance.waterColumns[WaterKey(row)])
                    : row.Material.Select(selection, instance.picked))) continue;
            bool shared = row.WorldGrid;
            if (row.Material.Flow != null)
            {
                if (rows.Count + reservedRows >= MaximumRecords ||
                    cells + reservedCells + row.Material.Cells * row.Material.Cells > MaximumCells) continue;
                AbilityMaterial taken = row.Material.Take(instance.picked);
                var selected = new WorldMaterialRecord
                {
                    Id = nextId++, Lineage = row.Lineage, WorldGrid = false,
                    Provider = row.Provider,
                    Controller = row.Controller,
                    Resource = row.Resource,
                    Material = taken,
                    Clock = clock,
                    Expires = row.Expires
                };
                rows.Add(selected.Id, selected);
                cells += taken.Cells * taken.Cells;
                row = selected;
            }

            row.Begin(recovery);
            if (shared)
            {
                if (!groups.TryGetValue(row.Resource, out long group))
                {
                    group = nextId++;
                    groups.Add(row.Resource, group);
                    recoveryGroups.Add(group,
                        new WorldMaterialRecoveryGroup
                            { Id = group, Provider = row.Provider, Resource = row.Resource });
                }

                row.RecoveryGroup = group;
            }

            row.Clock = clock;
            instance.indexDirty = true;
            Changed?.Invoke(new WorldMaterialChange(row, "recover"));
            if (epoch != instance.generation) return;
        }
    }

    /// <summary>只在材料或范围变化后建立空间筛选。</summary>
    private void RebuildIndex()
    {
        if (!indexDirty) return;
        indexDirty = false;
        index.Clear();
        indexed.Clear();
        UpdateWaterColumns();
        foreach (WorldMaterialRecord row in rows.Values)
        {
            if (row.Recovering || row.Finished) continue;
            Bounds bounds = BoundsOf(row);
            indexed.Add(row);
            index.Add(bounds);
        }
    }

    /// <summary>真实格子或姿态形状合成当前空间边界。</summary>
    /// <param name="row">实际记录。</param>
    private Bounds BoundsOf(WorldMaterialRecord row)
    {
        shapes.Clear();
        if (row.WorldGrid && !row.Recovering && waterColumns.TryGetValue(WaterKey(row), out var heights))
            row.Material.SharedContacts(shapes, heights);
        else row.Material.Contacts(shapes);
        Bounds bounds = new(row.Material.Transform.GetColumn(3), Vector3.zero);
        bool first = true;
        foreach (AbilityCapsule shape in shapes)
        {
            Bounds part = AbilityContactIndex.Swept(shape, shape);
            if (first)
            {
                bounds = part;
                first = false;
            }
            else bounds.Encapsulate(part);
        }

        return bounds;
    }

    /// <summary>沿用普通同步入口，正式调度从可分帧接口继续。</summary>
    protected override void OnUpdateGroup()
    {
        while (!Step())
        {
        }
    }

    /// <summary>向现有调度让出主线程，不另建线程池。</summary>
    bool ICooperativeSystemStep.StepCooperatively() => Step();

    /// <summary>继续同一个已确认活跃的数值过程。</summary>
    private bool Step()
    {
        var current = pending ??= Simulate(ConstructedAbilityService.ConfirmedWorldTime, true);
        if (current.MoveNext()) return false;
        current.Dispose();
        if (ReferenceEquals(pending, current)) pending = null;
        return true;
    }

    /// <summary>按法术已经确认的共同时间推进材料，复用同一个维护者和工作池。</summary>
    /// <param name="clock">实际确认时点。</param>
    internal static IEnumerator<bool> AdvanceTo(double clock) => instance.Simulate(clock, false);

    /// <summary>推进至明确的共同时间，后续选择不能读取未来或上一帧的材料。</summary>
    /// <param name="now">已经确认的目标时间。</param>
    /// <param name="publish">只在完整周期或恢复时发布，时间片不重复复制绘制资料。</param>
    private IEnumerator<bool> Simulate(double now, bool publish, bool restoring = false)
    {
        // 先取得当前事实和共同地表，再推进每个有限时间片，最后确认入账并发布。
        long epoch = generation;
        if (!restoring) ConvertOpenWater();
        WorldMaterialRecord[] batch = rows.Values.ToArray();
        if (batch.Length > 0)
        {
            Rect area = default;
            bool first = true;
            foreach (WorldMaterialRecord row in batch)
            {
                Bounds bounds = BoundsOf(row);
                if (first)
                {
                    area = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
                    first = false;
                }
                else
                {
                    area.xMin = Mathf.Min(area.xMin, bounds.min.x);
                    area.yMin = Mathf.Min(area.yMin, bounds.min.y);
                    area.xMax = Mathf.Max(area.xMax, bounds.max.x);
                    area.yMax = Mathf.Max(area.yMax, bounds.max.y);
                }
            }

            terrain = ConstructedAbilityService.GroundProvider?.Invoke(area);
            foreach (WorldMaterialRecord row in batch) row.Material.RefreshGround(terrain);
            ApplySolidGround(batch.Where(row =>
                row.WorldGrid && !row.Finished && !row.Recovering && !row.Material.Matter.HoldsShape).ToArray());
            while (rows.Values.Any(row => !row.Finished && row.Clock < now))
            {
                double clock = rows.Values.Where(row => !row.Finished && row.Clock < now).Min(row => row.Clock);
                double until = Math.Min(now, clock + 1d / 30);
                foreach (var row in rows.Values)
                {
                    if (row.Clock > clock) until = Math.Min(until, row.Clock);
                    if (row.Expires > clock) until = Math.Min(until, row.Expires);
                }

                var ordinary = rows.Values
                    .Where(row =>
                        !row.Finished && row.Clock <= clock &&
                        (!row.WorldGrid || row.Recovering || row.Material.Matter.HoldsShape)).ToArray();
                int work = ordinary.Sum(row => Math.Max(1, row.Material.Cells * row.Material.Cells));
                using (var calculation = MaterialTasks(ordinary.Length, work,
                        i => ordinary[i].Step(until, terrain)))
                    while (calculation.MoveNext())
                    {
                        if (epoch != generation) yield break;
                        yield return false;
                    }

                if (epoch != generation) yield break;
                foreach (var row in ordinary)
                        row.Clock = until;
                ConfirmWorldCredits();

                if (epoch != generation) yield break;
                using (var water = AdvanceWater(clock, until))
                    while (water.MoveNext())
                {
                        if (epoch != generation) yield break;
                        yield return false;
                    }

                if (epoch != generation) yield break;
                indexDirty = true;
                yield return false;
            }

            PruneWater();
            batch = rows.Values.ToArray();
        }

        foreach (WorldMaterialRecord row in batch)
        {
            if (row.Finished)
            {
                rows.Remove(row.Id);
                RetireRecovery(row);
                cells -= row.Material.Cells * row.Material.Cells;
                Changed?.Invoke(new WorldMaterialChange(row, "end"));
                if (epoch != generation) yield break;
            }
        }

        if (publish) Publish();
    }

    /// <summary>发布当前完整事实，绘制不推进或回写世界。</summary>
    private void Publish() => FormVisualScene.Publish("world.materials", WaterVisuals());

    /// <summary>只在数值任务完成后返还原支付者，保存已确认累计避免读档重发。</summary>
    /// <param name="row">已经完成本次数值片的记录。</param>
    private static void Credit(WorldMaterialRecord row)
    {
        if (!row.Recovering) return;
        double total = row.InitialResource * row.Efficiency * row.Progress;
        if (row.Resource is "mana" or "stamina") total = Math.Floor(total + 1e-8);
        double request = Math.Max(0, total - row.Confirmed);
        row.Confirmed = total;
        double available = request + row.Fraction;
        row.Fraction = 0;
        double granted = 0;
        Actor actor = World.world.units.get(row.Provider);
        if (available > 0 && actor?.data?.id == row.Provider && actor.isAlive() && actor.hasHealth())
            granted = MaterialRecoveryCredit.Grant(actor.GetExtend(), row.Resource, available, out row.Fraction);
        if (request > 0) Changed?.Invoke(new WorldMaterialChange(row, "credit", granted));
    }

    /// <summary>正常完成或清世界时让现有工作池恢复空闲，任务不写入人物和新世界。</summary>
    private void CompleteJob()
    {
        if (!job.HasValue) return;
        var ticket = job.Value;
        SimulationWorkerPool.Instance.Wait(ticket);
        SimulationWorkerPool.Instance.Complete(ticket);
        job = null;
    }

    /// <summary>清理唯一事实、额度、数值任务与最后发布。</summary>
    private void Clear()
    {
        generation++;
        CompleteJob();
        pending?.Dispose();
        pending = null;
        rows.Clear();
        recoveryGroups.Clear();
        reservedRows = reservedCells = cells = 0;
        nextId = 1;
        terrain = null;
        waterOwners.Clear();
        waterColumns.Clear();
        waterBeds.Clear();
        conductionPulse.Clear();
        independentPulse.Clear();
        indexDirty = true;
        index.Clear();
        indexed.Clear();
        FormVisualScene.Publish("world.materials", Array.Empty<FormVisualInstance>());
    }

    /// <summary>逐条恢复，不因为一个未知形体丢掉其他材料。</summary>
    /// <param name="archive">外部世界资料。</param>
    private static void Restore(Archive archive)
    {
        nextId = archive.Next;
        double now = World.world.getCurWorldTime();
        foreach (JToken token in archive.RecoveryGroups)
        {
            try
            {
                RestoreRecoveryGroup(token.ToObject<WorldMaterialRecoveryGroup>(), archive.Next);
            }
            catch (Exception error)when (error is InvalidDataException or JsonException or ArgumentException)
            {
                ModClass.I.WorldSaves.LastReport.Skip("世界水层回收", "世界生成材料", error.Message);
            }
        }

        foreach (JToken token in archive.Records)
        {
            try
            {
                var d = token.ToObject<WorldMaterialData>();
                bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
                if (d == null || d.Id <= 0 || d.Id >= archive.Next || rows.ContainsKey(d.Id) || d.Provider <= 0 ||
                    d.Controller <= 0 || !Finite(d.Remaining) || d.Remaining < 0 ||
                    !Finite(d.Clock) || d.Resource is not ("none" or "wakan" or "vigor" or "mana" or "stamina") ||
                    !Finite(d.InitialResource) || d.InitialResource < 0 || !Finite(d.InitialVolume) ||
                    d.InitialVolume < 0 || !Finite(d.Confirmed) || d.Confirmed < 0 ||
                    !Finite(d.Fraction) || d.Fraction < 0 || !Finite(d.Progress) || d.Progress < 0 || d.Progress > 1 ||
                    !Finite(d.Efficiency) || d.Efficiency < 0 || d.Efficiency > 1 ||
                    d.Fraction > d.Confirmed || d.Confirmed > d.InitialResource * d.Efficiency * d.Progress + 1e-7 ||
                    d.Recovering && (!Finite(d.Seconds) || d.Seconds <= 0))
                    throw new InvalidDataException("世界材料编号、归属或独立过程无效");
                if (d.RecoveryGroup != 0 && (!d.Recovering ||
                                             !recoveryGroups.TryGetValue(d.RecoveryGroup, out var group) ||
                                             group.Provider != d.Provider ||
                                             group.Resource != d.Resource))
                    throw new InvalidDataException("世界材料共同回收归属缺失或不符");
                AbilityMaterial material = AbilityMaterial.Restore(d.Material);
                if (d.Lineage <= 0) throw new InvalidDataException("世界材料缺少稳定来源链");
                if (d.WorldGrid && (material.Flow == null || material.Flow.Budget == null ||
                                    material.Cells != AbilityWorldWater.Side ||
                                    Math.Abs(material.Flow.Cell - WorldCell) > .00001f ||
                                    Math.Abs(material.Flow.Origin.x / (WorldCell * AbilityWorldWater.Side) -
                                             Math.Round(material.Flow.Origin.x /
                                                        (WorldCell * AbilityWorldWater.Side))) > .00001 ||
                                    Math.Abs(material.Flow.Origin.y / (WorldCell * AbilityWorldWater.Side) -
                                             Math.Round(material.Flow.Origin.y /
                                                        (WorldCell * AbilityWorldWater.Side))) > .00001 ||
                                    Math.Abs(material.Flow.Resource - material.Resource) >
                                    Math.Max(.00001, material.Resource * 1e-7)))
                    throw new InvalidDataException("共同水层坐标或逐格支付余量无效");
                if (cells + material.Cells * material.Cells > MaximumCells)
                    throw new InvalidDataException("世界材料超过水格保存预算");
                if (d.Resource is "mana" or "stamina" && Math.Max(material.Resource, d.InitialResource) > int.MaxValue)
                    throw new InvalidDataException("世界材料的整数支付份额越界");
                var row = new WorldMaterialRecord
                {
                    Id = d.Id, Lineage = d.Lineage, WorldGrid = d.WorldGrid, RecoveryGroup = d.RecoveryGroup,
                    Provider = d.Provider,
                    Controller = d.Controller,
                    Resource = d.Resource,
                    Material = material,
                    Clock = now,
                    Expires = d.Remaining > 0 ? now + d.Remaining : 0,
                    Recovering = d.Recovering,
                    Seconds = d.Seconds,
                    Efficiency = d.Efficiency,
                    Progress = d.Progress,
                    InitialVolume = d.InitialVolume,
                    InitialResource = d.InitialResource,
                    Confirmed = d.Confirmed,
                    Fraction = d.Fraction
                };
                rows.Add(row.Id, row);
                cells += material.Cells * material.Cells;
            }
            catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException
                                              or JsonException)
            {
                ModClass.I.WorldSaves.LastReport.Skip(
                    "世界材料 " + (token is JObject item ? item["Id"]?.ToString() ?? "未知" : "未知"), "世界生成材料", error.Message);
            }
        }

        instance.indexDirty = true;
        using (var restored = instance.Simulate(now, true, true))
            while (restored.MoveNext())
            {
            }
    }
}